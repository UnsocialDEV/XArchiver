using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class SyncSessionManagerTests
{
    [TestMethod]
    public async Task QueueWhenIdleStartsImmediatelyAndKeepsSecondSessionQueued()
    {
        FakeArchiveProfileRepository repository = new();
        ControlledSyncRunner runner = new();
        SyncSessionManager manager = new(runner, repository);

        manager.Queue(CreateRequest("alpha"));
        SyncSessionRecord secondSession = manager.Queue(CreateRequest("beta"));

        await runner.WaitForRunCountAsync(1);
        await WaitForConditionAsync(
            () =>
            {
                IReadOnlyList<SyncSessionRecord> sessions = manager.GetSessions();
                return sessions.Count == 2 &&
                       sessions.Any(session => session.Profile.Username == "alpha" && session.State is SyncSessionState.Running or SyncSessionState.Starting) &&
                       sessions.Any(session => session.SessionId == secondSession.SessionId && session.State == SyncSessionState.Queued);
            });

        runner.CompleteRun(0);
        await runner.WaitForRunCountAsync(2);
        await WaitForConditionAsync(() => manager.GetSessions().Any(session => session.Profile.Username == "beta" && session.State is SyncSessionState.Running or SyncSessionState.Starting));
        runner.CompleteRun(1);
        await WaitForConditionAsync(() => manager.GetSessions().Count(session => session.State == SyncSessionState.Completed) == 2);
    }

    [TestMethod]
    public async Task PauseThenStartAsyncResumesPausedSession()
    {
        FakeArchiveProfileRepository repository = new();
        ControlledSyncRunner runner = new();
        SyncSessionManager manager = new(runner, repository);

        SyncSessionRecord session = manager.Queue(CreateRequest("pause-me"));
        await runner.WaitForRunCountAsync(1);
        await WaitForConditionAsync(() => manager.GetSessions().Any(candidate => candidate.SessionId == session.SessionId && candidate.State == SyncSessionState.Running));

        Assert.IsTrue(manager.Pause(session.SessionId));
        await WaitForConditionAsync(() => manager.GetSessions().Any(candidate => candidate.SessionId == session.SessionId && candidate.State == SyncSessionState.Paused));

        await manager.StartAsync(session.SessionId, CancellationToken.None);
        await WaitForConditionAsync(() => manager.GetSessions().Any(candidate => candidate.SessionId == session.SessionId && candidate.State == SyncSessionState.Running));
        runner.CompleteRun(0);
        await WaitForConditionAsync(() => manager.GetSessions().Any(candidate => candidate.SessionId == session.SessionId && candidate.State == SyncSessionState.Completed));
    }

    [TestMethod]
    public async Task StopSessionWhenQueuedMarksItStoppedWithoutRunning()
    {
        FakeArchiveProfileRepository repository = new();
        ControlledSyncRunner runner = new();
        SyncSessionManager manager = new(runner, repository);

        manager.Queue(CreateRequest("first"));
        SyncSessionRecord queuedSession = manager.Queue(CreateRequest("second"));
        await runner.WaitForRunCountAsync(1);

        Assert.IsTrue(manager.StopSession(queuedSession.SessionId));
        await WaitForConditionAsync(() => manager.GetSessions().Any(session => session.SessionId == queuedSession.SessionId && session.State == SyncSessionState.Stopped));
        Assert.AreEqual(1, runner.RunCount);
        runner.CompleteRun(0);
        await WaitForConditionAsync(() => manager.GetSessions().Any(session => session.Profile.Username == "first" && session.State == SyncSessionState.Completed));
    }

    [TestMethod]
    public async Task StartAsyncWhenSessionCompletedCreatesNewSession()
    {
        FakeArchiveProfileRepository repository = new();
        ControlledSyncRunner runner = new();
        SyncSessionManager manager = new(runner, repository);

        SyncSessionRecord completedSession = manager.Queue(CreateRequest("restartable"));
        await runner.WaitForRunCountAsync(1);
        runner.CompleteRun(0);
        await WaitForConditionAsync(() => manager.GetSessions().Any(session => session.SessionId == completedSession.SessionId && session.State == SyncSessionState.Completed));

        SyncSessionRecord? restartedSession = await manager.StartAsync(completedSession.SessionId, CancellationToken.None);

        Assert.IsNotNull(restartedSession);
        Assert.AreNotEqual(completedSession.SessionId, restartedSession.SessionId);
        await runner.WaitForRunCountAsync(2);
        await WaitForConditionAsync(() => manager.GetSessions().Count >= 2);
        runner.CompleteRun(1);
        await WaitForConditionAsync(() => manager.GetSessions().Count(session => session.State == SyncSessionState.Completed) == 2);
    }

    [TestMethod]
    public async Task QueueWhenRunFindsNoNewPostsMarksSessionUpToDate()
    {
        FakeArchiveProfileRepository repository = new();
        ControlledSyncRunner runner = new()
        {
            ArchivedPostCountToReturn = 0,
        };
        SyncSessionManager manager = new(runner, repository);

        SyncSessionRecord session = manager.Queue(CreateRequest("up-to-date"));
        await runner.WaitForRunCountAsync(1);

        runner.CompleteRun(0);

        await WaitForConditionAsync(
            () =>
            {
                SyncSessionRecord? completedSession = manager.GetSessions().FirstOrDefault(candidate => candidate.SessionId == session.SessionId);
                return completedSession is not null &&
                       completedSession.State == SyncSessionState.Completed &&
                       completedSession.LatestStatusText == "No new posts matched this sync." &&
                       completedSession.Progress.StageText == "Up to date";
            });
    }

    private static ApiSyncRequest CreateRequest(string username)
    {
        return new ApiSyncRequest
        {
            Profile = CreateProfile(username),
        };
    }

    private static ArchiveProfile CreateProfile(string username)
    {
        return new ArchiveProfile
        {
            ArchiveRootPath = "C:\\archive",
            MaxPostsPerSync = 25,
            ProfileId = Guid.NewGuid(),
            Username = username,
        };
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        DateTimeOffset timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= timeout)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class ControlledSyncRunner : ISyncRunner
    {
        private readonly List<RunContext> _runs = [];
        private readonly object _syncRoot = new();

        public int ArchivedPostCountToReturn { get; set; } = 1;

        public int RunCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _runs.Count;
                }
            }
        }

        public void CompleteRun(int index)
        {
            RunContext run;
            lock (_syncRoot)
            {
                run = _runs[index];
            }

            run.Complete.TrySetResult();
        }

        public async Task<SyncResult> RunAsync(
            ApiSyncRequest request,
            IProgress<SyncProgressSnapshot> progress,
            ISyncPauseGate pauseGate,
            CancellationToken cancellationToken)
        {
            ArchiveProfile profile = request.Profile;
            RunContext run = new(profile);
            lock (_syncRoot)
            {
                _runs.Add(run);
            }

            run.Started.TrySetResult();

            int scannedPages = 0;
            while (!run.Complete.Task.IsCompleted)
            {
                await pauseGate.WaitIfPausedAsync(cancellationToken);
                scannedPages++;
                progress.Report(
                    new SyncProgressSnapshot
                    {
                        ArchivedPostCount = 0,
                        ScannedPageCount = scannedPages,
                        StageText = "Scanning posts",
                        TargetPostCount = profile.MaxPostsPerSync,
                    });
                await Task.Delay(20, cancellationToken);
            }

            await run.Complete.Task.WaitAsync(cancellationToken);

            ArchiveProfile updatedProfile = new()
            {
                ArchiveRootPath = profile.ArchiveRootPath,
                DownloadImages = profile.DownloadImages,
                DownloadVideos = profile.DownloadVideos,
                IncludeOriginalPosts = profile.IncludeOriginalPosts,
                IncludeQuotes = profile.IncludeQuotes,
                IncludeReplies = profile.IncludeReplies,
                IncludeReposts = profile.IncludeReposts,
                LastSinceId = "99",
                LastSuccessfulSyncUtc = DateTimeOffset.UtcNow,
                MaxPostsPerSync = profile.MaxPostsPerSync,
                ProfileId = profile.ProfileId,
                UserId = profile.UserId,
                Username = profile.Username,
            };

            return new SyncResult
            {
                ArchivedPostCount = ArchivedPostCountToReturn,
                ScannedPageCount = scannedPages,
                Status = SyncStatus.Success,
                UpdatedProfile = updatedProfile,
            };
        }

        public async Task WaitForRunCountAsync(int expectedCount)
        {
            await WaitForConditionAsync(() => RunCount >= expectedCount);
        }

        private sealed class RunContext
        {
            public RunContext(ArchiveProfile profile)
            {
                Profile = profile;
            }

            public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ArchiveProfile Profile { get; }

            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class FakeArchiveProfileRepository : IArchiveProfileRepository
    {
        private readonly List<ArchiveProfile> _profiles = [];

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken)
        {
            _profiles.RemoveAll(profile => profile.ProfileId == profileId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchiveProfile>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchiveProfile>>(_profiles.ToList());
        }

        public Task SaveAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            _profiles.RemoveAll(existing => existing.ProfileId == profile.ProfileId);
            _profiles.Add(profile);
            return Task.CompletedTask;
        }
    }
}
