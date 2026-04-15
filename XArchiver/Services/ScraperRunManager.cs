using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Services;

public sealed class ScraperRunManager : IScraperRunManager
{
    private const int MaximumPreviewPosts = 12;
    private const int MaximumEvents = 250;
    private readonly IScraperBrowserSessionLauncher _scraperBrowserSessionLauncher;
    private readonly object _syncRoot = new();
    private readonly IWebArchiveService _webArchiveService;
    private readonly string _diagnosticsRootDirectory;
    private RuntimeScraperSession? _session;

    public ScraperRunManager(
        IWebArchiveService webArchiveService,
        IScraperBrowserSessionLauncher scraperBrowserSessionLauncher,
        string diagnosticsRootDirectory)
    {
        _webArchiveService = webArchiveService;
        _scraperBrowserSessionLauncher = scraperBrowserSessionLauncher;
        _diagnosticsRootDirectory = diagnosticsRootDirectory;
    }

    public event EventHandler? RunChanged;

    public bool ForceKill()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is ScraperRunState.Completed or ScraperRunState.Failed or ScraperRunState.Stopped)
        {
            return false;
        }

        session.RunControl.RequestForceKill();
        UpdateSnapshot(
            snapshot => snapshot with
            {
                State = ScraperRunState.Stopping,
                StatusText = "Force killing scraper.",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await _scraperBrowserSessionLauncher.TerminateSessionProcessesAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort process termination. The run cancellation below is authoritative.
                }
                finally
                {
                    session.CancellationTokenSource.Cancel();
                }
            });

        return true;
    }

    public ScraperRunSnapshot? GetCurrentRun()
    {
        lock (_syncRoot)
        {
            return _session?.Snapshot;
        }
    }

    public bool OpenLiveBrowser()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is ScraperRunState.Completed or ScraperRunState.Failed or ScraperRunState.Stopped)
        {
            return false;
        }

        session.RunControl.RequestVisibleBrowser();
        UpdateSnapshot(
            snapshot => snapshot with
            {
                StatusText = "Requested visible browser mode.",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        return true;
    }

    public bool Pause()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is not (ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.WaitingForIntervention))
        {
            return false;
        }

        session.PauseGate.Pause();
        return true;
    }

    public bool Resume()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is not (ScraperRunState.Paused or ScraperRunState.WaitingForIntervention))
        {
            return false;
        }

        session.PauseGate.ResumeScrape();
        UpdateSnapshot(
            snapshot => snapshot with
            {
                BlockingReason = string.Empty,
                StatusText = "Resuming scrape.",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        return true;
    }

    public async Task<bool> StartAsync(WebArchiveRequest request, CancellationToken cancellationToken)
    {
        RuntimeScraperSession newSession;

        lock (_syncRoot)
        {
            if (_session is not null &&
                _session.Snapshot.State is (ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.Paused or ScraperRunState.WaitingForIntervention or ScraperRunState.Stopping))
            {
                return false;
            }

            newSession = CreateSession(request);
            _session = newSession;
        }

        PublishRunChanged();
        _ = RunAsync(newSession, cancellationToken);
        return await Task.FromResult(true);
    }

    public bool StopAndSave()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is ScraperRunState.Completed or ScraperRunState.Failed or ScraperRunState.Stopped)
        {
            return false;
        }

        session.RunControl.RequestStopAndSave();
        session.PauseGate.ResumeScrape();
        UpdateSnapshot(
            snapshot => snapshot with
            {
                State = ScraperRunState.Stopping,
                StatusText = "Stopping scrape and saving collected posts.",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        return true;
    }

    public bool Stop()
    {
        RuntimeScraperSession? session = GetSession();
        if (session is null || session.Snapshot.State is ScraperRunState.Completed or ScraperRunState.Failed or ScraperRunState.Stopped)
        {
            return false;
        }

        session.RunControl.RequestForceKill();
        UpdateSnapshot(
            snapshot => snapshot with
            {
                State = ScraperRunState.Stopping,
                StatusText = "Stopping scrape.",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        session.CancellationTokenSource.Cancel();
        return true;
    }

    private RuntimeScraperSession CreateSession(WebArchiveRequest request)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RuntimeScraperSession session = new(
            new ScraperPauseGate(),
            new ScraperRunControl(),
            new CancellationTokenSource(),
            new ScraperRunSnapshot
            {
                CreatedAtUtc = timestamp,
                Progress = new WebArchiveProgressSnapshot
                {
                    RunState = ScraperRunState.Starting,
                    StageText = "Starting scrape",
                    TargetPostCount = Math.Max(1, request.MaxPostsToScrape),
                },
                Request = request,
                State = ScraperRunState.Starting,
                StatusText = "Starting scrape.",
                UpdatedAtUtc = timestamp,
            });

        session.PauseGate.StateChanged += OnPauseStateChanged;
        return session;
    }

    private ScraperDiagnosticsBuffer CreateSink(RuntimeScraperSession session)
    {
        string diagnosticsDirectory = Path.Combine(_diagnosticsRootDirectory, session.Snapshot.RunId.ToString("N"));
        Directory.CreateDirectory(diagnosticsDirectory);

        return new ScraperDiagnosticsBuffer(
            diagnosticsDirectory,
            diagnosticsEvent => AppendEvent(diagnosticsEvent),
            snapshot => UpdateSnapshot(
                current => current with
                {
                    CurrentUrl = snapshot.PageUrl,
                    LiveSnapshot = snapshot,
                    PageTitle = snapshot.PageTitle,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                }),
            post => AppendPreviewPost(post));
    }

    private RuntimeScraperSession? GetSession()
    {
        lock (_syncRoot)
        {
            return _session;
        }
    }

    private void AppendEvent(ScraperDiagnosticsEvent diagnosticsEvent)
    {
        UpdateSnapshot(
            snapshot =>
            {
                List<ScraperDiagnosticsEvent> events = snapshot.Events.ToList();
                events.Add(diagnosticsEvent);
                if (events.Count > MaximumEvents)
                {
                    events.RemoveRange(0, events.Count - MaximumEvents);
                }

                return snapshot with
                {
                    CurrentUrl = diagnosticsEvent.Url ?? snapshot.CurrentUrl,
                    HtmlSnapshotPath = string.Equals(Path.GetExtension(diagnosticsEvent.ArtifactPath), ".html", StringComparison.OrdinalIgnoreCase)
                        ? diagnosticsEvent.ArtifactPath ?? snapshot.HtmlSnapshotPath
                        : snapshot.HtmlSnapshotPath,
                    StatusText = diagnosticsEvent.Message,
                    Events = events,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private void AppendPreviewPost(ScrapedPostRecord post)
    {
        UpdateSnapshot(
            snapshot =>
            {
                List<ScrapedPostRecord> posts = snapshot.PreviewPosts
                    .Where(existing => !string.Equals(existing.PostId, post.PostId, StringComparison.Ordinal))
                    .Take(MaximumPreviewPosts - 1)
                    .ToList();
                posts.Insert(0, post);

                return snapshot with
                {
                    PreviewPosts = posts,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private void OnPauseStateChanged(object? sender, ScraperPauseStateChangedEventArgs e)
    {
        UpdateSnapshot(
            snapshot =>
            {
                ScraperRunState nextState = snapshot.State;
                string statusText = snapshot.StatusText;

                if (e.IsPauseRequested && e.IsPaused)
                {
                    nextState = snapshot.Progress.RunState == ScraperRunState.WaitingForIntervention
                        ? ScraperRunState.WaitingForIntervention
                        : ScraperRunState.Paused;
                    statusText = string.IsNullOrWhiteSpace(snapshot.BlockingReason) ? "Paused." : snapshot.BlockingReason;
                }
                else if (e.IsPauseRequested && !e.IsPaused)
                {
                    nextState = snapshot.State;
                }
                else if (!e.IsPauseRequested && !e.IsPaused && snapshot.State is ScraperRunState.Paused or ScraperRunState.WaitingForIntervention)
                {
                    nextState = ScraperRunState.Running;
                    statusText = "Resuming scrape.";
                }

                return snapshot with
                {
                    State = nextState,
                    StatusText = statusText,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private async Task RunAsync(RuntimeScraperSession session, CancellationToken cancellationToken)
    {
        ScraperDiagnosticsBuffer sink = CreateSink(session);
        Progress<WebArchiveProgressSnapshot> progress = new(snapshot => OnProgress(snapshot));

        try
        {
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.CancellationTokenSource.Token);
            WebArchiveResult result = await _webArchiveService
                .ArchiveAsync(
                    session.Snapshot.Request,
                    progress,
                    sink,
                    session.PauseGate,
                    session.RunControl,
                    preferVisibleBrowser: false,
                    linkedCancellation.Token)
                .ConfigureAwait(false);

            UpdateSnapshot(
                snapshot =>
                {
                    ScraperRunState state = result.WasForceKilled
                        ? ScraperRunState.Failed
                        : result.WasSuccessful
                            ? ScraperRunState.Completed
                            : ScraperRunState.Failed;
                    string statusText = result.WasForceKilled
                        ? "Scraper was force killed."
                        : result.WasPartialSave
                            ? $"Stopped and saved {result.SavedPostCount} collected posts."
                            : result.WasConservativeStop
                                ? result.WasSuccessful
                                    ? $"Conservative mode stopped early and saved {result.SavedPostCount} posts. {result.ConservativeStopReason}"
                                    : result.ConservativeStopReason ?? "Conservative mode stopped early."
                            : result.WasSuccessful
                                ? "Scrape completed."
                                : result.ErrorMessage ?? "Scrape failed.";

                    return snapshot with
                    {
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        State = state,
                        StatusText = statusText,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    };
                });
        }
        catch (OperationCanceledException) when (session.CancellationTokenSource.IsCancellationRequested)
        {
            UpdateSnapshot(
                snapshot =>
                {
                    bool wasForceKilled = session.RunControl.IsForceKillRequested;
                    return snapshot with
                    {
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        State = wasForceKilled ? ScraperRunState.Failed : ScraperRunState.Stopped,
                        StatusText = wasForceKilled ? "Scraper was force killed." : "Scrape stopped.",
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    };
                });
        }
        finally
        {
            session.PauseGate.StateChanged -= OnPauseStateChanged;
        }
    }

    private void OnProgress(WebArchiveProgressSnapshot progress)
    {
        UpdateSnapshot(
            snapshot =>
            {
                ScraperRunState nextState = progress.RunState switch
                {
                    ScraperRunState.WaitingForIntervention => ScraperRunState.WaitingForIntervention,
                    ScraperRunState.Paused => ScraperRunState.Paused,
                    ScraperRunState.Stopping => ScraperRunState.Stopping,
                    ScraperRunState.Completed => ScraperRunState.Completed,
                    ScraperRunState.Failed => ScraperRunState.Failed,
                    _ => ScraperRunState.Running,
                };

                return snapshot with
                {
                    BlockingReason = progress.BlockingReason,
                    CurrentUrl = progress.CurrentUrl,
                    PageTitle = progress.PageTitle,
                    Progress = progress,
                    State = nextState,
                    StatusText = progress.StageText,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            });
    }

    private void PublishRunChanged()
    {
        RunChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSnapshot(Func<ScraperRunSnapshot, ScraperRunSnapshot> updater)
    {
        bool updated = false;

        lock (_syncRoot)
        {
            if (_session is null)
            {
                return;
            }

            _session.Snapshot = updater(_session.Snapshot);
            updated = true;
        }

        if (updated)
        {
            PublishRunChanged();
        }
    }

    private sealed class RuntimeScraperSession
    {
        public RuntimeScraperSession(
            ScraperPauseGate pauseGate,
            ScraperRunControl runControl,
            CancellationTokenSource cancellationTokenSource,
            ScraperRunSnapshot snapshot)
        {
            PauseGate = pauseGate;
            RunControl = runControl;
            CancellationTokenSource = cancellationTokenSource;
            Snapshot = snapshot;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public ScraperPauseGate PauseGate { get; }

        public ScraperRunControl RunControl { get; }

        public ScraperRunSnapshot Snapshot { get; set; }
    }
}
