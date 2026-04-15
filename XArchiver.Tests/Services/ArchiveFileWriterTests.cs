using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;
using XArchiver.Core.Utilities;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ArchiveFileWriterTests
{
    [TestMethod]
    public async Task WriteAsyncWhenPostContainsMediaCreatesSeparatedArchiveFiles()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "sample/user",
        };

        ArchivedPostRecord post = new()
        {
            CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero),
            MediaDetails =
            [
                new ArchivedMediaDetailRecord
                {
                    MediaKey = "image-key",
                    MediaType = "photo",
                    Url = "https://cdn.example.com/image.jpg",
                },
            ],
            Media =
            [
                new ArchivedMediaRecord
                {
                    Kind = ArchiveMediaKind.Image,
                    MediaKey = "image-key",
                    PostId = "100",
                    SourceUrl = "https://cdn.example.com/image.jpg",
                },
                new ArchivedMediaRecord
                {
                    Kind = ArchiveMediaKind.Video,
                    MediaKey = "video-key",
                    PostId = "100",
                    SourceUrl = "https://cdn.example.com/video.mp4",
                },
            ],
            PostId = "100",
            PostType = ArchivePostType.Original,
            RawPayloadJson = """{"post":{"id":"100"}}""",
            ReferencedPosts =
            [
                new ArchivedReferencedPostRecord
                {
                    ReferenceType = "quoted",
                    ReferencedPostId = "99",
                },
            ],
            Text = "hello archive",
            UserId = "user-1",
            Username = "sampleuser",
        };

        ArchiveFileWriter writer = new(new FakeMediaDownloader(), new ArchiveMetadataBuilder());

        ArchivedPostRecord archivedPost = await writer.WriteAsync(profile, post, CancellationToken.None);

        string profileRoot = ArchivePathBuilder.GetProfileRoot(profile);
        Assert.IsTrue(File.Exists(Path.Combine(profileRoot, archivedPost.TextRelativePath!)));
        Assert.IsTrue(File.Exists(Path.Combine(profileRoot, archivedPost.MetadataRelativePath!)));
        string metadataPath = Path.Combine(profileRoot, archivedPost.MetadataRelativePath!);
        ArchivedPostMetadataReadResult metadata = await new ArchiveMetadataRepository().LoadAsync(metadataPath, CancellationToken.None);
        Assert.HasCount(2, archivedPost.Media);
        Assert.IsTrue(archivedPost.Media.All(media => File.Exists(Path.Combine(profileRoot, media.RelativePath!))));
        Assert.IsTrue(archivedPost.Media.Any(media => media.RelativePath!.StartsWith($"images{Path.DirectorySeparatorChar}", StringComparison.Ordinal)));
        Assert.IsTrue(archivedPost.Media.Any(media => media.RelativePath!.StartsWith($"videos{Path.DirectorySeparatorChar}", StringComparison.Ordinal)));
        Assert.IsTrue(metadata.IsExtendedMetadata);
        Assert.AreEqual(ArchivedPostRecord.ExtendedMetadataSchemaVersion, metadata.Document!.SchemaVersion);
        Assert.AreEqual("99", metadata.Document.Post.ReferencedPosts[0].ReferencedPostId);

        Directory.Delete(tempRoot, true);
    }

    private sealed class FakeMediaDownloader : IMediaDownloader
    {
        public Task DownloadAsync(Uri sourceUri, string destinationPath, CancellationToken cancellationToken)
        {
            return File.WriteAllTextAsync(destinationPath, sourceUri.AbsoluteUri, cancellationToken);
        }
    }
}
