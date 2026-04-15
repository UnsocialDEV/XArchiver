using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class PostReviewServiceTests
{
    [TestMethod]
    public async Task LoadPageAsyncFiltersPostTypesAndMarksArchivedPosts()
    {
        FakeXApiClient xApiClient = new();
        xApiClient.PreviewPages.Enqueue(
            new PreviewPageResult
            {
                NextToken = "next-1",
                Posts =
                [
                    new PreviewPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "1",
                        PostType = ArchivePostType.Original,
                        Text = "keep me",
                        UserId = "42",
                        Username = "sample",
                    },
                    new PreviewPostRecord
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        PostId = "2",
                        PostType = ArchivePostType.Reply,
                        Text = "skip me",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
                ScannedPostReads = 2,
            });

        PostReviewService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveIndexRepository(["1"]));

        PreviewPageResult result = await service.LoadPageAsync(
            new ApiSyncRequest
            {
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    IncludeOriginalPosts = true,
                    IncludeQuotes = false,
                    IncludeReplies = false,
                    IncludeReposts = false,
                    Username = "sample",
                },
            },
            null,
            CancellationToken.None);

        Assert.IsNull(result.NextToken);
        Assert.HasCount(1, result.Posts);
        Assert.AreEqual("1", result.Posts[0].PostId);
        Assert.IsTrue(result.Posts[0].IsAlreadyArchived);
    }

    [TestMethod]
    public async Task LoadPageAsyncWhenArchiveRangeIsSetReturnsOnlyMatchingPosts()
    {
        FakeXApiClient xApiClient = new();
        DateTimeOffset archiveStart = new(2026, 4, 14, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset archiveEnd = new(2026, 4, 14, 19, 0, 0, TimeSpan.Zero);
        xApiClient.PreviewPages.Enqueue(
            new PreviewPageResult
            {
                Posts =
                [
                    new PreviewPostRecord
                    {
                        CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 20, 0, 0, TimeSpan.Zero),
                        PostId = "too-new",
                        PostType = ArchivePostType.Original,
                        Text = "skip",
                        UserId = "42",
                        Username = "sample",
                    },
                    new PreviewPostRecord
                    {
                        CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 17, 0, 0, TimeSpan.Zero),
                        PostId = "in-range",
                        PostType = ArchivePostType.Original,
                        Text = "keep",
                        UserId = "42",
                        Username = "sample",
                    },
                    new PreviewPostRecord
                    {
                        CreatedAtUtc = archiveStart.AddMinutes(-1),
                        PostId = "too-old",
                        PostType = ArchivePostType.Original,
                        Text = "skip",
                        UserId = "42",
                        Username = "sample",
                    },
                ],
                ScannedPostReads = 2,
            });

        PostReviewService service = new(
            new FakeCredentialStore("token"),
            xApiClient,
            new FakeArchiveIndexRepository([]));

        PreviewPageResult result = await service.LoadPageAsync(
            new ApiSyncRequest
            {
                ArchiveEndUtc = archiveEnd,
                ArchiveStartUtc = archiveStart,
                Profile = new ArchiveProfile
                {
                    ArchiveRootPath = "C:\\archive",
                    Username = "sample",
                },
            },
            null,
            CancellationToken.None);

        Assert.AreEqual(archiveStart, xApiClient.StartTimeUtc);
        Assert.AreEqual(archiveEnd, xApiClient.EndTimeUtc);
        Assert.HasCount(1, result.Posts);
        Assert.AreEqual("in-range", result.Posts[0].PostId);
    }

    private sealed class FakeArchiveIndexRepository : IArchiveIndexRepository
    {
        private readonly IReadOnlySet<string> _archivedIds;

        public FakeArchiveIndexRepository(IReadOnlyCollection<string> archivedIds)
        {
            _archivedIds = new HashSet<string>(archivedIds, StringComparer.Ordinal);
        }

        public Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ArchivedPostRecord?>(null);
        }

        public Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(
            ArchiveProfile profile,
            IReadOnlyCollection<string> postIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlySet<string>>(_archivedIds);
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

    private sealed class FakeXApiClient : IXApiClient
    {
        public Queue<PreviewPageResult> PreviewPages { get; } = new();

        public Task<XUserProfile> GetUserAsync(string username, string bearerToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new XUserProfile { UserId = "42", UserName = username });
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
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            return Task.FromResult(PreviewPages.Count == 0 ? new PreviewPageResult() : PreviewPages.Dequeue());
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
            return Task.FromResult(new XTimelinePage());
        }

        public DateTimeOffset? EndTimeUtc { get; private set; }

        public DateTimeOffset? StartTimeUtc { get; private set; }
    }
}
