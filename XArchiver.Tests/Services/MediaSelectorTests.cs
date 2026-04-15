using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class MediaSelectorTests
{
    [TestMethod]
    public void SelectMediaWhenMediaIncludesPhotoAndVideoSelectsExpectedOutputs()
    {
        MediaSelector selector = new();
        List<XMediaDefinition> definitions =
        [
            new XMediaDefinition
            {
                MediaKey = "photo1",
                Type = "photo",
                Url = "https://cdn.example.com/image.jpg",
            },
            new XMediaDefinition
            {
                MediaKey = "video1",
                Type = "video",
                PreviewImageUrl = "https://cdn.example.com/preview.jpg",
                Variants =
                [
                    new XMediaVariant { Url = "https://cdn.example.com/video-low.mp4", ContentType = "video/mp4", BitRate = 256000 },
                    new XMediaVariant { Url = "https://cdn.example.com/video-high.mp4", ContentType = "video/mp4", BitRate = 1024000 },
                ],
            },
            new XMediaDefinition
            {
                MediaKey = "video2",
                Type = "video",
                PreviewImageUrl = "https://cdn.example.com/preview-only.jpg",
            },
        ];

        IReadOnlyList<ArchivedMediaRecord> media = selector.SelectMedia("123", definitions);

        Assert.HasCount(3, media);
        Assert.AreEqual("https://cdn.example.com/image.jpg", media[0].SourceUrl);
        Assert.AreEqual(ArchiveMediaKind.Image, media[0].Kind);
        Assert.AreEqual("https://cdn.example.com/video-high.mp4", media[1].SourceUrl);
        Assert.AreEqual(ArchiveMediaKind.Video, media[1].Kind);
        Assert.AreEqual("https://cdn.example.com/preview-only.jpg", media[2].SourceUrl);
        Assert.IsTrue(media[2].IsPartial);
    }
}
