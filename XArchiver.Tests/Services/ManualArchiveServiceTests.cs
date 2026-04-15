using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ManualArchiveServiceTests
{
    [TestMethod]
    public async Task ArchiveSelectedAsyncWritesOnlySelectedUnarchivedPosts()
    {
        FakeArchiveFileWriter archiveFileWriter = new();
        FakeArchiveIndexRepository archiveIndexRepository = new(["2"]);
        ManualArchiveService service = new(archiveFileWriter, archiveIndexRepository);

        ManualArchiveResult result = await service.ArchiveSelectedAsync(
            new ArchiveProfile
            {
                ArchiveRootPath = "C:\\archive",
                Username = "sample",
            },
            [
                new PreviewPostRecord
                {
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    IsSelected = true,
                    MediaDetails =
                    [
                        new ArchivedMediaDetailRecord
                        {
                            MediaKey = "detail-1",
                            MediaType = "photo",
                            Url = "https://cdn.example.com/image.jpg",
                        },
                    ],
                    PostId = "1",
                    PostType = ArchivePostType.Original,
                    RawPayloadJson = """{"post":{"id":"1"}}""",
                    ReferencedPosts =
                    [
                        new ArchivedReferencedPostRecord
                        {
                            ReferenceType = "quoted",
                            ReferencedPostId = "88",
                        },
                    ],
                    Text = "archive me",
                    UserId = "42",
                    Username = "sample",
                },
                new PreviewPostRecord
                {
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    IsAlreadyArchived = true,
                    IsSelected = true,
                    PostId = "2",
                    PostType = ArchivePostType.Original,
                    Text = "skip me",
                    UserId = "42",
                    Username = "sample",
                },
            ],
            CancellationToken.None);

        Assert.AreEqual(1, result.ArchivedPostCount);
        Assert.AreEqual(1, result.SkippedAlreadyArchivedCount);
        Assert.HasCount(1, archiveFileWriter.WrittenPosts);
        Assert.AreEqual("1", archiveFileWriter.WrittenPosts[0].PostId);
        Assert.AreEqual("88", archiveFileWriter.WrittenPosts[0].ReferencedPosts[0].ReferencedPostId);
        Assert.AreEqual("detail-1", archiveFileWriter.WrittenPosts[0].MediaDetails[0].MediaKey);
    }

    private sealed class FakeArchiveFileWriter : IArchiveFileWriter
    {
        public List<ArchivedPostRecord> WrittenPosts { get; } = [];

        public Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            WrittenPosts.Add(post);
            return Task.FromResult(post);
        }
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
}
