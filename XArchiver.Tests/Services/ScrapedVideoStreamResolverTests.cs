using System.Net;
using System.Net.Http;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ScrapedVideoStreamResolverTests
{
    [TestMethod]
    public async Task ResolveAsyncWhenDirectMp4ExistsReturnsThatUrl()
    {
        ScrapedVideoStreamResolver resolver = new(new HttpClient(new StubHttpMessageHandler()));

        var result = await resolver.ResolveAsync(
            ["https://video.twimg.com/ext_tw_video/test-video.mp4"],
            CancellationToken.None);

        Assert.IsTrue(result.WasResolved);
        Assert.AreEqual("DirectMp4", result.ResolutionKind);
        Assert.AreEqual("https://video.twimg.com/ext_tw_video/test-video.mp4", result.ResolvedUrl);
    }

    [TestMethod]
    public async Task ResolveAsyncWhenManifestContainsMp4ResolvesHighestBandwidthVariant()
    {
        Dictionary<string, string> responses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["https://video.twimg.com/ext_tw_video/master.m3u8"] =
                """
                #EXTM3U
                #EXT-X-STREAM-INF:BANDWIDTH=128000
                low.m3u8
                #EXT-X-STREAM-INF:BANDWIDTH=512000
                high.m3u8
                """,
            ["https://video.twimg.com/ext_tw_video/low.m3u8"] =
                """
                #EXTM3U
                #EXT-X-STREAM-INF:BANDWIDTH=96000
                low.mp4
                """,
            ["https://video.twimg.com/ext_tw_video/high.m3u8"] =
                """
                #EXTM3U
                #EXT-X-STREAM-INF:BANDWIDTH=384000
                high.mp4
                """,
        };

        ScrapedVideoStreamResolver resolver = new(new HttpClient(new StubHttpMessageHandler(responses)));

        var result = await resolver.ResolveAsync(
            ["https://video.twimg.com/ext_tw_video/master.m3u8"],
            CancellationToken.None);

        Assert.IsTrue(result.WasResolved);
        Assert.AreEqual("HlsToMp4", result.ResolutionKind);
        Assert.AreEqual("https://video.twimg.com/ext_tw_video/high.mp4", result.ResolvedUrl);
        Assert.AreEqual("https://video.twimg.com/ext_tw_video/master.m3u8", result.ManifestUrl);
    }

    [TestMethod]
    public async Task ResolveAsyncWhenOnlyBlobUrlsExistFails()
    {
        ScrapedVideoStreamResolver resolver = new(new HttpClient(new StubHttpMessageHandler()));

        var result = await resolver.ResolveAsync(
            ["blob:https://x.com/video/123"],
            CancellationToken.None);

        Assert.IsFalse(result.WasResolved);
        Assert.AreEqual("Failed", result.ResolutionKind);
        Assert.AreEqual("No downloadable video asset URLs were discovered.", result.FailureReason);
    }

    [TestMethod]
    public async Task ResolveAsyncWhenManifestHasNoMp4Fails()
    {
        Dictionary<string, string> responses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["https://video.twimg.com/ext_tw_video/master.m3u8"] =
                """
                #EXTM3U
                #EXT-X-TARGETDURATION:10
                #EXTINF:10,
                segment000.ts
                #EXTINF:10,
                segment001.ts
                """,
        };

        ScrapedVideoStreamResolver resolver = new(new HttpClient(new StubHttpMessageHandler(responses)));

        var result = await resolver.ResolveAsync(
            ["https://video.twimg.com/ext_tw_video/master.m3u8"],
            CancellationToken.None);

        Assert.IsFalse(result.WasResolved);
        Assert.AreEqual("Failed", result.ResolutionKind);
        Assert.AreEqual("No downloadable MP4 video asset could be resolved from the discovered sources.", result.FailureReason);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public StubHttpMessageHandler()
            : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public StubHttpMessageHandler(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null &&
                _responses.TryGetValue(request.RequestUri.AbsoluteUri, out string? responseBody))
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseBody),
                    });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
