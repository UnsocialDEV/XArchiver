using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ArchiveSyncServiceTests
{
    private static readonly int[] ExpectedPageSizes = [5, 5];

    [TestMethod]
    public async Task SyncAsyncWhenCredentialMissingReturnsMissingCredential()
    {
        ArchiveSyncService service = new(
            new FakeCredentialStore(null),
            new FakeXApiClient(),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile { Username = "sample", ArchiveRootPath = "C:\\archive" },
            },
            new ProgressCollector(),
            new FakePauseGate(),
            CancellationToken.None);

        Assert.AreEqual(SyncStatus.MissingCredential, result.Status);
    }

    [TestMethod]
    public async Task SyncAsyncWhenProfileHasSinceIdUsesItAndUpdatesCheckpoint()
    {
        FakeXApiClient xApiClient = new();
        xApiClient.TimelinePages.Enqueue(
            new XTimelinePage
            {
                Posts =
                [
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "10",
                        PostType = ArchivePostType.Original,
                        Text = "hello",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
            });

        ArchiveSyncService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());

        ProgressCollector progress = new();

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    LastSinceId = "5",
                    MaxPostsPerSync = 20,
                    Username = "sample",
                },
            },
            progress,
            new FakePauseGate(),
            CancellationToken.None);

        Assert.AreEqual("5", xApiClient.LastSinceId);
        Assert.AreEqual(SyncStatus.Success, result.Status);
        Assert.AreEqual("10", result.UpdatedProfile!.LastSinceId);
        Assert.AreEqual(1, result.ArchivedPostCount);
        Assert.AreEqual(1, result.ScannedPageCount);
        Assert.IsTrue(progress.Snapshots.Any(snapshot => snapshot.StageText == "Authenticating"));
        Assert.IsTrue(progress.Snapshots.Any(snapshot => snapshot.StageText == "Writing files"));
    }

    [TestMethod]
    public async Task SyncAsyncWhenRemainingPostsDropBelowFiveStillRequestsFive()
    {
        FakeXApiClient xApiClient = new();
        xApiClient.TimelinePages.Enqueue(
            new XTimelinePage
            {
                NextToken = "page-2",
                Posts =
                [
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "11",
                        PostType = ArchivePostType.Original,
                        Text = "one",
                        UserId = "42",
                        Username = "sample",
                    },
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "12",
                        PostType = ArchivePostType.Original,
                        Text = "two",
                        UserId = "42",
                        Username = "sample",
                    },
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "13",
                        PostType = ArchivePostType.Original,
                        Text = "three",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
            });
        xApiClient.TimelinePages.Enqueue(
            new XTimelinePage
            {
                Posts =
                [
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "14",
                        PostType = ArchivePostType.Original,
                        Text = "four",
                        UserId = "42",
                        Username = "sample",
                    },
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "15",
                        PostType = ArchivePostType.Original,
                        Text = "five",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
            });

        ArchiveSyncService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());

        ProgressCollector progress = new();

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    MaxPostsPerSync = 5,
                    Username = "sample",
                },
            },
            progress,
            new FakePauseGate(),
            CancellationToken.None);

        CollectionAssert.AreEqual(ExpectedPageSizes, xApiClient.RequestedPageSizes);
        Assert.AreEqual(5, result.ArchivedPostCount);
        Assert.AreEqual(2, result.ScannedPageCount);
        Assert.IsTrue(progress.Snapshots.Any(snapshot => snapshot.ScannedPageCount == 2));
    }

    [TestMethod]
    public async Task SyncAsyncReportsCompletionProgressForLargeSync()
    {
        FakeXApiClient xApiClient = new();
        xApiClient.TimelinePages.Enqueue(
            new XTimelinePage
            {
                Posts =
                [
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "21",
                        PostType = ArchivePostType.Original,
                        Text = "progress",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
            });

        ArchiveSyncService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());
        ProgressCollector progress = new();

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    MaxPostsPerSync = 25,
                    Username = "sample",
                },
            },
            progress,
            new FakePauseGate(),
            CancellationToken.None);

        Assert.AreEqual(SyncStatus.Success, result.Status);
        Assert.AreEqual("Completed", progress.Snapshots[^1].StageText);
        Assert.AreEqual(25, progress.Snapshots[^1].TargetPostCount);
        Assert.AreEqual(1, progress.Snapshots[^1].ArchivedPostCount);
    }

    [TestMethod]
    public async Task SyncAsyncWhenNoPostsMatchReportsUpToDateCompletion()
    {
        FakeXApiClient xApiClient = new();
        xApiClient.TimelinePages.Enqueue(new XTimelinePage());

        ArchiveSyncService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());
        ProgressCollector progress = new();

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    LastSinceId = "99",
                    MaxPostsPerSync = 20,
                    Username = "sample",
                },
            },
            progress,
            new FakePauseGate(),
            CancellationToken.None);

        Assert.AreEqual(SyncStatus.Success, result.Status);
        Assert.AreEqual(0, result.ArchivedPostCount);
        Assert.AreEqual(1, result.ScannedPageCount);
        Assert.AreEqual("99", result.UpdatedProfile!.LastSinceId);
        Assert.AreEqual("No new posts matched this sync", progress.Snapshots[^1].StageText);
    }

    [TestMethod]
    public async Task SyncAsyncWhenArchiveRangeIsSetUsesRangeAndKeepsCheckpoint()
    {
        FakeXApiClient xApiClient = new();
        DateTimeOffset archiveStart = new(2026, 4, 14, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset archiveEnd = new(2026, 4, 14, 19, 0, 0, TimeSpan.Zero);
        xApiClient.TimelinePages.Enqueue(
            new XTimelinePage
            {
                Posts =
                [
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 20, 0, 0, TimeSpan.Zero),
                        PostId = "21",
                        PostType = ArchivePostType.Original,
                        Text = "too new",
                        UserId = "42",
                        Username = "sample",
                    },
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 18, 0, 0, TimeSpan.Zero),
                        PostId = "20",
                        PostType = ArchivePostType.Original,
                        Text = "in range",
                        UserId = "42",
                        Username = "sample",
                    },
                    new ArchivedPostRecord
                    {
                        CreatedAtUtc = archiveStart.AddMinutes(-1),
                        PostId = "19",
                        PostType = ArchivePostType.Original,
                        Text = "too old",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
            });

        ArchiveSyncService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository());

        SyncResult result = await service.SyncAsync(
            new ApiSyncRequest
            {
                ArchiveEndUtc = archiveEnd,
                ArchiveStartUtc = archiveStart,
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    LastSinceId = "17",
                    MaxPostsPerSync = 10,
                    Username = "sample",
                },
            },
            new ProgressCollector(),
            new FakePauseGate(),
            CancellationToken.None);

        Assert.IsNull(xApiClient.LastSinceId);
        Assert.AreEqual(archiveStart, xApiClient.StartTimeUtc);
        Assert.AreEqual(archiveEnd, xApiClient.EndTimeUtc);
        Assert.AreEqual(1, result.ArchivedPostCount);
        Assert.AreEqual("17", result.UpdatedProfile!.LastSinceId);
    }

    private sealed class FakeArchiveFileWriter : IArchiveFileWriter
    {
        public Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            return Task.FromResult(post);
        }
    }

    private sealed class FakeArchiveIndexRepository : IArchiveIndexRepository
    {
        public Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ArchivedPostRecord?>(null);
        }

        public Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(
            ArchiveProfile profile,
            IReadOnlyCollection<string> postIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        public Task InitializeAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchivedPostRecord>> QueryAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedPostRecord>>([]);
        }

        public Task<IReadOnlyList<ArchivedGalleryMediaRecord>> QueryGalleryMediaAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedGalleryMediaRecord>>([]);
        }

        public Task UpsertAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore : IXCredentialStore
    {
        private readonly string? _credential;

        public FakeCredentialStore(string? credential)
        {
            _credential = credential;
        }

        public Task DeleteCredentialAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetCredentialAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_credential);
        }

        public Task<bool> HasCredentialAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(_credential));
        }

        public Task SaveCredentialAsync(string credential, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakePauseGate : ISyncPauseGate
    {
        public event EventHandler<SyncPauseStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public bool IsPauseRequested => false;

        public void Pause()
        {
        }

        public void ResumeSync()
        {
        }

        public Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ProgressCollector : IProgress<SyncProgressSnapshot>
    {
        public List<SyncProgressSnapshot> Snapshots { get; } = [];

        public void Report(SyncProgressSnapshot value)
        {
            Snapshots.Add(value);
        }
    }

    private sealed class FakeXApiClient : IXApiClient
    {
        public string? LastSinceId { get; private set; }

        public Queue<XTimelinePage> TimelinePages { get; } = new();

        public List<int> RequestedPageSizes { get; } = [];

        public Task<XUserProfile> GetUserAsync(string username, string bearerToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new XUserProfile { UserId = "42", UserName = username });
        }

        public Task<XTimelinePage> GetUserPostsAsync(
            XUserProfile user,
            string bearerToken,
            string? sinceId,
            DateTimeOffset? startTimeUtc,
            DateTimeOffset? endTimeUtc,
            string? paginationToken,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastSinceId = sinceId;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            RequestedPageSizes.Add(pageSize);

            if (TimelinePages.Count == 0)
            {
                return Task.FromResult(new XTimelinePage());
            }

            return Task.FromResult(TimelinePages.Dequeue());
        }

        public Task<PreviewPageResult> GetUserPreviewPostsAsync(
            XUserProfile user,
            string bearerToken,
            DateTimeOffset? startTimeUtc,
            DateTimeOffset? endTimeUtc,
            string? paginationToken,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PreviewPageResult());
        }

        public DateTimeOffset? EndTimeUtc { get; private set; }

        public DateTimeOffset? StartTimeUtc { get; private set; }
    }
}
