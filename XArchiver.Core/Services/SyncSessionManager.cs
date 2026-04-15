using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class SyncSessionManager : ISyncSessionManager
{
    private readonly object _syncRoot = new();
    private readonly IArchiveProfileRepository _archiveProfileRepository;
    private readonly ISyncRunner _syncRunner;
    private readonly List<RuntimeSyncSession> _sessions = [];
    private readonly LinkedList<Guid> _queuedSessionIds = [];
    private Guid? _activeSessionId;

    public SyncSessionManager(ISyncRunner syncRunner, IArchiveProfileRepository archiveProfileRepository)
    {
        _syncRunner = syncRunner;
        _archiveProfileRepository = archiveProfileRepository;
    }

    public event EventHandler? SessionsChanged;

    public IReadOnlyList<SyncSessionRecord> GetSessions()
    {
        lock (_syncRoot)
        {
            return _sessions
                .Select(session => session.Record)
                .OrderByDescending(GetDisplayPriority)
                .ThenByDescending(session => session.UpdatedAtUtc)
                .ToList();
        }
    }

    public bool Pause(Guid sessionId)
    {
        RuntimeSyncSession? session = GetRuntimeSession(sessionId);
        if (session is null || session.Record.State is not (SyncSessionState.Running or SyncSessionState.Starting))
        {
            return false;
        }

        session.PauseGate.Pause();
        return true;
    }

    public SyncSessionRecord Queue(ApiSyncRequest request)
    {
        RuntimeSyncSession session = CreateRuntimeSession(CloneRequest(request), SyncSessionState.Queued, "Queued");

        lock (_syncRoot)
        {
            _sessions.Add(session);
            _queuedSessionIds.AddLast(session.Record.SessionId);
        }

        PublishSessionsChanged();
        TryRunNextSession();
        return session.Record;
    }

    public async Task<SyncSessionRecord?> StartAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        RuntimeSyncSession? session = GetRuntimeSession(sessionId);
        if (session is null)
        {
            return null;
        }

        switch (session.Record.State)
        {
            case SyncSessionState.Paused:
                session.PauseGate.ResumeSync();
                PublishSessionsChanged();
                return session.Record;

            case SyncSessionState.Queued:
                PromoteQueuedSession(sessionId);
                PublishSessionsChanged();
                TryRunNextSession();
                return session.Record;

            case SyncSessionState.Stopped:
            case SyncSessionState.Completed:
            case SyncSessionState.Failed:
                ApiSyncRequest restartRequest = await ResolveRestartRequestAsync(session.Record.Request, cancellationToken).ConfigureAwait(false);
                RuntimeSyncSession restartedSession = CreateRuntimeSession(restartRequest, SyncSessionState.Queued, "Queued");

                lock (_syncRoot)
                {
                    _sessions.Add(restartedSession);
                    _queuedSessionIds.AddFirst(restartedSession.Record.SessionId);
                }

                PublishSessionsChanged();
                TryRunNextSession();
                return restartedSession.Record;

            default:
                return session.Record;
        }
    }

    public bool StopSession(Guid sessionId)
    {
        RuntimeSyncSession? session = GetRuntimeSession(sessionId);
        if (session is null)
        {
            return false;
        }

        switch (session.Record.State)
        {
            case SyncSessionState.Queued:
                RemoveQueuedSession(sessionId);
                UpdateSessionRecord(
                    sessionId,
                    record => record with
                    {
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        LatestStatusText = "Stopped before start.",
                        State = SyncSessionState.Stopped,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    });
                return true;

            case SyncSessionState.Starting:
            case SyncSessionState.Running:
            case SyncSessionState.Pausing:
            case SyncSessionState.Paused:
                UpdateSessionRecord(
                    sessionId,
                    record => record with
                    {
                        LatestStatusText = "Stopping sync.",
                        State = SyncSessionState.Stopping,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    });
                session.CancellationTokenSource.Cancel();
                return true;

            default:
                return false;
        }
    }

    private static ApiSyncRequest CloneRequest(ApiSyncRequest request)
    {
        return new ApiSyncRequest
        {
            ArchiveEndUtc = request.ArchiveEndUtc,
            ArchiveStartUtc = request.ArchiveStartUtc,
            Profile = CloneProfile(request.Profile),
        };
    }

    private static ArchiveProfile CloneProfile(ArchiveProfile profile)
    {
        return new ArchiveProfile
        {
            ArchiveRootPath = profile.ArchiveRootPath,
            DownloadImages = profile.DownloadImages,
            DownloadVideos = profile.DownloadVideos,
            IncludeOriginalPosts = profile.IncludeOriginalPosts,
            IncludeQuotes = profile.IncludeQuotes,
            IncludeReplies = profile.IncludeReplies,
            IncludeReposts = profile.IncludeReposts,
            LastSinceId = profile.LastSinceId,
            LastSuccessfulSyncUtc = profile.LastSuccessfulSyncUtc,
            MaxPostsPerWebArchive = profile.MaxPostsPerWebArchive,
            MaxPostsPerSync = profile.MaxPostsPerSync,
            PreferredSource = profile.PreferredSource,
            ProfileId = profile.ProfileId,
            ProfileUrl = profile.ProfileUrl,
            UserId = profile.UserId,
            Username = profile.Username,
        };
    }

    private RuntimeSyncSession CreateRuntimeSession(ApiSyncRequest request, SyncSessionState initialState, string statusText)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RuntimeSyncSession session = new(
            new SyncPauseGate(),
            new CancellationTokenSource(),
            new SyncSessionRecord
            {
                CreatedAtUtc = timestamp,
                LatestStatusText = statusText,
                Profile = CloneProfile(request.Profile),
                Progress = new SyncProgressSnapshot
                {
                    StageText = statusText,
                    TargetPostCount = Math.Max(1, request.Profile.MaxPostsPerSync),
                },
                Request = CloneRequest(request),
                SessionId = Guid.NewGuid(),
                State = initialState,
                UpdatedAtUtc = timestamp,
            });

        session.PauseGate.StateChanged += OnPauseStateChanged;
        return session;
    }

    private RuntimeSyncSession? GetRuntimeSession(Guid sessionId)
    {
        lock (_syncRoot)
        {
            return _sessions.FirstOrDefault(session => session.Record.SessionId == sessionId);
        }
    }

    private static int GetDisplayPriority(SyncSessionRecord record)
    {
        return record.State switch
        {
            SyncSessionState.Running => 8,
            SyncSessionState.Starting => 7,
            SyncSessionState.Pausing => 6,
            SyncSessionState.Paused => 5,
            SyncSessionState.Queued => 4,
            SyncSessionState.Stopping => 3,
            SyncSessionState.Failed => 2,
            SyncSessionState.Completed => 1,
            SyncSessionState.Stopped => 0,
            _ => 0,
        };
    }

    private void OnPauseStateChanged(object? sender, SyncPauseStateChangedEventArgs e)
    {
        if (sender is not SyncPauseGate pauseGate)
        {
            return;
        }

        RuntimeSyncSession? session = null;
        lock (_syncRoot)
        {
            session = _sessions.FirstOrDefault(candidate => ReferenceEquals(candidate.PauseGate, pauseGate));
        }

        if (session is null)
        {
            return;
        }

        UpdateSessionRecord(
            session.Record.SessionId,
            record =>
            {
                SyncSessionState nextState = record.State;
                string nextStatus = record.LatestStatusText;

                if (e.IsPauseRequested && !e.IsPaused)
                {
                    nextState = SyncSessionState.Pausing;
                    nextStatus = "Pausing after current step.";
                }
                else if (e.IsPauseRequested && e.IsPaused)
                {
                    nextState = SyncSessionState.Paused;
                    nextStatus = "Paused.";
                }
                else if (!e.IsPauseRequested && !e.IsPaused && record.State == SyncSessionState.Paused)
                {
                    nextState = SyncSessionState.Running;
                    nextStatus = "Resuming sync.";
                }

                return record with
                {
                    LatestStatusText = nextStatus,
                    Progress = record.Progress with { StageText = nextStatus },
                    State = nextState,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private void PromoteQueuedSession(Guid sessionId)
    {
        lock (_syncRoot)
        {
            LinkedListNode<Guid>? node = _queuedSessionIds.Find(sessionId);
            if (node is null || node == _queuedSessionIds.First)
            {
                return;
            }

            _queuedSessionIds.Remove(node);
            _queuedSessionIds.AddFirst(node);
        }
    }

    private void PublishSessionsChanged()
    {
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveQueuedSession(Guid sessionId)
    {
        lock (_syncRoot)
        {
            LinkedListNode<Guid>? node = _queuedSessionIds.Find(sessionId);
            if (node is not null)
            {
                _queuedSessionIds.Remove(node);
            }
        }
    }

    private async Task<ApiSyncRequest> ResolveRestartRequestAsync(ApiSyncRequest fallbackRequest, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArchiveProfile> profiles = await _archiveProfileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        ArchiveProfile? savedProfile = profiles.FirstOrDefault(profile => profile.ProfileId == fallbackRequest.Profile.ProfileId);
        return new ApiSyncRequest
        {
            ArchiveEndUtc = fallbackRequest.ArchiveEndUtc,
            ArchiveStartUtc = fallbackRequest.ArchiveStartUtc,
            Profile = CloneProfile(savedProfile ?? fallbackRequest.Profile),
        };
    }

    private async Task RunSessionAsync(RuntimeSyncSession session)
    {
        Progress<SyncProgressSnapshot> progress = new(snapshot => OnSessionProgress(session.Record.SessionId, snapshot));

        try
        {
            SyncResult result = await _syncRunner
                .RunAsync(session.Record.Request, progress, session.PauseGate, session.CancellationTokenSource.Token)
                .ConfigureAwait(false);

            UpdateSessionFromResult(session.Record.SessionId, result);
        }
        catch (OperationCanceledException) when (session.CancellationTokenSource.IsCancellationRequested)
        {
            UpdateSessionRecord(
                session.Record.SessionId,
                record => record with
                {
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    LatestStatusText = "Sync stopped.",
                    Progress = record.Progress with { StageText = "Stopped" },
                    State = SyncSessionState.Stopped,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
        }
        catch (Exception exception)
        {
            UpdateSessionRecord(
                session.Record.SessionId,
                record => record with
                {
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    LatestStatusText = exception.Message,
                    Progress = record.Progress with { StageText = "Failed" },
                    Result = new SyncResult
                    {
                        ErrorMessage = exception.Message,
                        Status = SyncStatus.Failed,
                    },
                    State = SyncSessionState.Failed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
        }
        finally
        {
            session.PauseGate.StateChanged -= OnPauseStateChanged;

            lock (_syncRoot)
            {
                if (_activeSessionId == session.Record.SessionId)
                {
                    _activeSessionId = null;
                }
            }

            TryRunNextSession();
        }
    }

    private void TryRunNextSession()
    {
        RuntimeSyncSession? nextSession = null;

        lock (_syncRoot)
        {
            if (_activeSessionId is not null || _queuedSessionIds.First is null)
            {
                return;
            }

            Guid nextSessionId = _queuedSessionIds.First.Value;
            _queuedSessionIds.RemoveFirst();
            nextSession = _sessions.FirstOrDefault(session => session.Record.SessionId == nextSessionId);
            if (nextSession is null)
            {
                return;
            }

            _activeSessionId = nextSession.Record.SessionId;
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;
            nextSession.Record = nextSession.Record with
            {
                LatestStatusText = "Starting sync.",
                Progress = nextSession.Record.Progress with { StageText = "Starting sync" },
                StartedAtUtc = timestamp,
                State = SyncSessionState.Starting,
                UpdatedAtUtc = timestamp,
            };
        }

        PublishSessionsChanged();
        _ = RunSessionAsync(nextSession);
    }

    private void UpdateSessionFromResult(Guid sessionId, SyncResult result)
    {
        UpdateSessionRecord(
            sessionId,
            record =>
            {
                DateTimeOffset timestamp = DateTimeOffset.UtcNow;
                SyncSessionState state = result.Status == SyncStatus.Success ? SyncSessionState.Completed : SyncSessionState.Failed;
                string statusText = GetCompletionStatusText(result);
                string stageText = GetCompletionStageText(result);

                return record with
                {
                    CompletedAtUtc = timestamp,
                    LatestStatusText = statusText,
                    Request = record.Request,
                    Profile = result.UpdatedProfile is null ? record.Profile : CloneProfile(result.UpdatedProfile),
                    Progress = record.Progress with
                    {
                        ArchivedPostCount = result.ArchivedPostCount,
                        DownloadedImageCount = result.DownloadedImageCount,
                        DownloadedVideoCount = result.DownloadedVideoCount,
                        PartialMediaCount = result.PartialMediaCount,
                        ScannedPageCount = result.ScannedPageCount,
                        StageText = stageText,
                    },
                    Result = result,
                    State = state,
                    UpdatedAtUtc = timestamp,
                };
            });
    }

    private static string GetCompletionStageText(SyncResult result)
    {
        return result.Status switch
        {
            SyncStatus.Success when result.ArchivedPostCount > 0 => "Completed",
            SyncStatus.Success => "Up to date",
            SyncStatus.MissingCredential => "Missing credential",
            SyncStatus.Failed => "Failed",
            _ => "Finished",
        };
    }

    private static string GetCompletionStatusText(SyncResult result)
    {
        return result.Status switch
        {
            SyncStatus.Success when result.ArchivedPostCount > 0 => "Sync completed.",
            SyncStatus.Success => "No new posts matched this sync.",
            SyncStatus.MissingCredential => "Missing X credential.",
            SyncStatus.Failed => result.ErrorMessage ?? "Sync failed.",
            _ => "Sync finished.",
        };
    }

    private void UpdateSessionRecord(Guid sessionId, Func<SyncSessionRecord, SyncSessionRecord> updater)
    {
        bool updated = false;

        lock (_syncRoot)
        {
            RuntimeSyncSession? session = _sessions.FirstOrDefault(candidate => candidate.Record.SessionId == sessionId);
            if (session is null)
            {
                return;
            }

            session.Record = updater(session.Record);
            updated = true;
        }

        if (updated)
        {
            PublishSessionsChanged();
        }
    }

    private void OnSessionProgress(Guid sessionId, SyncProgressSnapshot snapshot)
    {
        UpdateSessionRecord(
            sessionId,
            record =>
            {
                SyncSessionState nextState = record.State is SyncSessionState.Starting or SyncSessionState.Running
                    ? SyncSessionState.Running
                    : record.State;

                return record with
                {
                    LatestStatusText = snapshot.StageText,
                    Progress = snapshot,
                    State = nextState,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private sealed class RuntimeSyncSession
    {
        public RuntimeSyncSession(SyncPauseGate pauseGate, CancellationTokenSource cancellationTokenSource, SyncSessionRecord record)
        {
            PauseGate = pauseGate;
            CancellationTokenSource = cancellationTokenSource;
            Record = record;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public SyncPauseGate PauseGate { get; }

        public SyncSessionRecord Record { get; set; }
    }
}
