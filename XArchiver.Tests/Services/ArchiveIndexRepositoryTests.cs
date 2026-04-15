using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ArchiveIndexRepositoryTests
{
    [TestMethod]
    public async Task QueryAsyncWhenFilterRequiresImagesAndSearchAppliesBoth()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "viewer",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);

        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "media-1",
                        PostId = "1",
                        RelativePath = "images\\media-1.jpg",
                        SourceUrl = "https://cdn.example.com/media-1.jpg",
                    },
                ],
                PostId = "1",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "archived sunset",
                UserId = "u1",
                Username = "viewer",
            },
            CancellationToken.None);

        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                PostId = "2",
                PostType = ArchivePostType.Reply,
                ProfileId = profile.ProfileId,
                Text = "plain text only",
                UserId = "u1",
                Username = "viewer",
            },
            CancellationToken.None);

        IReadOnlyList<ArchivedPostRecord> results = await repository.QueryAsync(
            profile,
            new ArchiveViewerFilter
            {
                IncludeImagePosts = true,
                IncludeOriginalPosts = true,
                IncludeQuotes = true,
                IncludeReplies = true,
                IncludeReposts = true,
                SearchText = "sunset",
                IncludeTextPosts = false,
                IncludeVideoPosts = false,
            },
            CancellationToken.None);

        Assert.HasCount(1, results);
        Assert.AreEqual("1", results[0].PostId);
    }

    [TestMethod]
    public async Task QueryAsyncWhenFilterRequiresVideosReturnsOnlyVideoPosts()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "viewer",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);

        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Video,
                        MediaKey = "video-1",
                        PostId = "video-post",
                        RelativePath = "videos\\video-1.mp4",
                        SourceUrl = "https://video.twimg.com/video-1.mp4",
                    },
                ],
                PostId = "video-post",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "video content",
                UserId = "u1",
                Username = "viewer",
            },
            CancellationToken.None);

        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "image-1",
                        PostId = "image-post",
                        RelativePath = "images\\image-1.jpg",
                        SourceUrl = "https://pbs.twimg.com/media/image-1.jpg",
                    },
                ],
                PostId = "image-post",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "image content",
                UserId = "u1",
                Username = "viewer",
            },
            CancellationToken.None);

        IReadOnlyList<ArchivedPostRecord> results = await repository.QueryAsync(
            profile,
            new ArchiveViewerFilter
            {
                IncludeImagePosts = false,
                IncludeOriginalPosts = true,
                IncludeQuotes = true,
                IncludeReplies = true,
                IncludeReposts = true,
                IncludeTextPosts = false,
                IncludeVideoPosts = true,
            },
            CancellationToken.None);

        Assert.HasCount(1, results);
        Assert.AreEqual("video-post", results[0].PostId);
    }

    [TestMethod]
    public async Task GetArchivedPostIdsAsyncReturnsOnlyStoredIds()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "lookup",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);
        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                PostId = "100",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "lookup post",
                UserId = "u1",
                Username = "lookup",
            },
            CancellationToken.None);

        IReadOnlySet<string> results = await repository.GetArchivedPostIdsAsync(
            profile,
            ["100", "200"],
            CancellationToken.None);

        Assert.Contains("100", results);
        Assert.DoesNotContain("200", results);
    }

    [TestMethod]
    public async Task QueryGalleryMediaAsyncWhenPicturesAndVideosEnabledReturnsOneTilePerMediaItem()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "gallery",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);
        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "image-1",
                        PostId = "post-1",
                        RelativePath = "images\\image-1.jpg",
                        SourceUrl = "https://pbs.twimg.com/media/image-1.jpg",
                    },
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Video,
                        MediaKey = "video-1",
                        PostId = "post-1",
                        RelativePath = "videos\\video-1.mp4",
                        SourceUrl = "https://video.twimg.com/video-1.mp4",
                    },
                ],
                PostId = "post-1",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "gallery media",
                UserId = "u1",
                Username = "gallery",
            },
            CancellationToken.None);

        IReadOnlyList<ArchivedGalleryMediaRecord> results = await repository.QueryGalleryMediaAsync(
            profile,
            new ArchiveViewerFilter
            {
                IncludeImagePosts = true,
                IncludeOriginalPosts = true,
                IncludeQuotes = true,
                IncludeReplies = true,
                IncludeReposts = true,
                IncludeTextPosts = true,
                IncludeVideoPosts = true,
            },
            CancellationToken.None);

        Assert.HasCount(2, results);
        Assert.AreEqual("post-1", results[0].ParentPostId);
        Assert.AreEqual("image-1", results[0].Media.MediaKey);
        Assert.AreEqual("video-1", results[1].Media.MediaKey);
    }

    [TestMethod]
    public async Task QueryGalleryMediaAsyncWhenOnlyTextIsEnabledReturnsNoTiles()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "gallery",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);
        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "image-1",
                        PostId = "post-1",
                        RelativePath = "images\\image-1.jpg",
                        SourceUrl = "https://pbs.twimg.com/media/image-1.jpg",
                    },
                ],
                PostId = "post-1",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "gallery media",
                UserId = "u1",
                Username = "gallery",
            },
            CancellationToken.None);

        IReadOnlyList<ArchivedGalleryMediaRecord> results = await repository.QueryGalleryMediaAsync(
            profile,
            new ArchiveViewerFilter
            {
                IncludeImagePosts = false,
                IncludeOriginalPosts = true,
                IncludeQuotes = true,
                IncludeReplies = true,
                IncludeReposts = true,
                IncludeTextPosts = true,
                IncludeVideoPosts = false,
            },
            CancellationToken.None);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task GetPostAsyncReturnsPostWithMedia()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = tempRoot,
            Username = "gallery",
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);
        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Media =
                [
                    new ArchivedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "image-1",
                        PostId = "post-1",
                        RelativePath = "images\\image-1.jpg",
                        SourceUrl = "https://pbs.twimg.com/media/image-1.jpg",
                    },
                ],
                PostId = "post-1",
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = "gallery media",
                UserId = "u1",
                Username = "gallery",
            },
            CancellationToken.None);

        ArchivedPostRecord? post = await repository.GetPostAsync(profile, "post-1", CancellationToken.None);

        Assert.IsNotNull(post);
        Assert.AreEqual("post-1", post.PostId);
        Assert.HasCount(1, post.Media);
        Assert.AreEqual("image-1", post.Media[0].MediaKey);
    }
}
