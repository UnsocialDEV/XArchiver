using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ScrapedPostHtmlParserTests
{
    [TestMethod]
    public void ParseWhenHtmlContainsStatusTextAndMediaReturnsScrapedPost()
    {
        ScrapedPostHtmlParser parser = new();

        ScrapedPostRecord? result = parser.Parse(
            """
            <article data-testid="tweet">
              <a href="/openai/status/1234567890">
                <time datetime="2026-04-14T16:25:00.000Z"></time>
              </a>
              <div data-testid="tweetText">First line</div>
              <div data-testid="tweetText">Second line</div>
              <img src="https://pbs.twimg.com/media/test-image.jpg" />
              <video src="https://video.twimg.com/ext_tw_video/test-video.mp4"></video>
            </article>
            """,
            "fallback-user");

        Assert.IsNotNull(result);
        Assert.AreEqual("1234567890", result.PostId);
        Assert.AreEqual("openai", result.Username);
        Assert.AreEqual("https://x.com/openai/status/1234567890", result.SourceUrl);
        Assert.AreEqual("First line" + Environment.NewLine + "Second line", result.Text);
        Assert.IsFalse(result.ContainsSensitiveMediaWarning);
        Assert.HasCount(2, result.Media);
        Assert.AreEqual(ArchiveMediaKind.Image, result.Media[0].Kind);
        Assert.AreEqual(ArchiveMediaKind.Video, result.Media[1].Kind);
    }

    [TestMethod]
    public void ParseWhenVideoOnlyHasPosterCreatesResolvableVideoCandidate()
    {
        ScrapedPostHtmlParser parser = new();

        ScrapedPostRecord? result = parser.Parse(
            """
            <article data-testid="tweet">
              <a href="/openai/status/222">
                <time datetime="2026-04-14T17:10:00.000Z"></time>
              </a>
              <div data-testid="tweetText">Poster only</div>
              <video poster="https://pbs.twimg.com/media/poster.jpg"></video>
            </article>
            """,
            "fallback-user");

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Media);
        Assert.AreEqual(ArchiveMediaKind.Video, result.Media[0].Kind);
        Assert.IsTrue(result.Media[0].IsPartial);
        Assert.IsTrue(result.Media[0].RequiresResolution);
        Assert.AreEqual(string.Empty, result.Media[0].SourceUrl);
        Assert.AreEqual("https://pbs.twimg.com/media/poster.jpg", result.Media[0].PreviewImageUrl);
    }

    [TestMethod]
    public void ParseWhenVideoUsesManifestMarksVideoForResolution()
    {
        ScrapedPostHtmlParser parser = new();

        ScrapedPostRecord? result = parser.Parse(
            """
            <article data-testid="tweet">
              <a href="/openai/status/444">
                <time datetime="2026-04-14T19:00:00.000Z"></time>
              </a>
              <div data-testid="tweetText">Manifest video</div>
              <video poster="https://pbs.twimg.com/media/poster.jpg">
                <source src="https://video.twimg.com/ext_tw_video/test-stream.m3u8" />
              </video>
            </article>
            """,
            "fallback-user");

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Media);
        Assert.AreEqual(ArchiveMediaKind.Video, result.Media[0].Kind);
        Assert.IsTrue(result.Media[0].RequiresResolution);
        Assert.AreEqual("https://video.twimg.com/ext_tw_video/test-stream.m3u8", result.Media[0].SourceUrl);
        Assert.AreEqual("https://video.twimg.com/ext_tw_video/test-stream.m3u8", result.Media[0].ManifestUrl);
        Assert.AreEqual("https://pbs.twimg.com/media/poster.jpg", result.Media[0].PreviewImageUrl);
    }

    [TestMethod]
    public void ParseWhenHtmlContainsSensitiveWarningSetsSensitiveMediaFlag()
    {
        ScrapedPostHtmlParser parser = new();

        ScrapedPostRecord? result = parser.Parse(
            """
            <article data-testid="tweet">
              <a href="/openai/status/333">
                <time datetime="2026-04-14T18:00:00.000Z"></time>
              </a>
              <div data-testid="tweetText">Protected image</div>
              <div>Content warning: Adult Content</div>
              <button>Show</button>
            </article>
            """,
            "fallback-user");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsSensitiveMediaWarning);
        Assert.IsFalse(result.SensitiveMediaRevealSucceeded);
        Assert.AreEqual(string.Empty, result.SensitiveMediaFailureReason);
    }
}
