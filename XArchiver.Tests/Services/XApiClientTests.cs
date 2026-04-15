using System.Net;
using System.Text;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class XApiClientTests
{
    [TestMethod]
    public async Task GetUserPostsAsyncWhenResponseIncludesMediaMapsPostsAndNextToken()
    {
        FakeHttpMessageHandler handler = new();
        handler.AddResponse(
            "https://api.x.com/2/users/42/tweets?max_results=50&tweet.fields=attachments,author_id,conversation_id,created_at,in_reply_to_user_id,public_metrics,referenced_tweets,text&expansions=attachments.media_keys&media.fields=duration_ms,height,media_key,preview_image_url,type,url,variants,width&since_id=5",
            """
            {
              "data": [
                {
                  "id": "100",
                  "text": "quoted post",
                  "created_at": "2026-04-14T10:00:00Z",
                  "conversation_id": "100",
                  "public_metrics": { "like_count": 3, "reply_count": 2, "retweet_count": 1, "quote_count": 4 },
                  "referenced_tweets": [{ "type": "quoted", "id": "90" }],
                  "attachments": { "media_keys": ["m1", "m2"] }
                },
                {
                  "id": "101",
                  "text": "reply post",
                  "created_at": "2026-04-14T09:00:00Z",
                  "conversation_id": "80",
                  "in_reply_to_user_id": "99",
                  "public_metrics": { "like_count": 1, "reply_count": 0, "retweet_count": 0, "quote_count": 0 }
                }
              ],
              "includes": {
                "media": [
                  { "media_key": "m1", "type": "photo", "url": "https://cdn.example.com/image.jpg", "width": 100, "height": 100 },
                  { "media_key": "m2", "type": "video", "preview_image_url": "https://cdn.example.com/preview.jpg", "variants": [
                    { "content_type": "video/mp4", "bit_rate": 256000, "url": "https://cdn.example.com/video-low.mp4" },
                    { "content_type": "video/mp4", "bit_rate": 1024000, "url": "https://cdn.example.com/video-high.mp4" }
                  ] }
                ]
              },
              "meta": { "next_token": "token-2" }
            }
            """);

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.x.com/2/"),
        };

        XApiClient client = new(httpClient, new MediaSelector());
        XTimelinePage page = await client.GetUserPostsAsync(
            new XUserProfile { UserId = "42", UserName = "sampleuser" },
            "token",
            "5",
            null,
            null,
            null,
            50,
            CancellationToken.None);

        Assert.AreEqual("token-2", page.NextToken);
        Assert.HasCount(2, page.Posts);
        Assert.AreEqual(ArchivePostType.Quote, page.Posts[0].PostType);
        Assert.HasCount(2, page.Posts[0].Media);
        Assert.IsNotNull(page.Posts[0].RawPayloadJson);
        StringAssert.Contains(page.Posts[0].RawPayloadJson, "\"media\"");
        StringAssert.Contains(page.Posts[0].RawPayloadJson, "\"id\": \"90\"");
        Assert.AreEqual("90", page.Posts[0].ReferencedPosts[0].ReferencedPostId);
        Assert.AreEqual("video", page.Posts[0].MediaDetails[1].MediaType);
        Assert.AreEqual("https://cdn.example.com/video-high.mp4", page.Posts[0].Media[1].SourceUrl);
        Assert.AreEqual(ArchivePostType.Reply, page.Posts[1].PostType);
    }

    [TestMethod]
    public async Task GetUserPostsAsyncWhenRangeProvidedAddsStartAndEndTimeQuery()
    {
        FakeHttpMessageHandler handler = new();
        handler.AddResponse(
            "https://api.x.com/2/users/42/tweets?max_results=10&tweet.fields=attachments,author_id,conversation_id,created_at,in_reply_to_user_id,public_metrics,referenced_tweets,text&expansions=attachments.media_keys&media.fields=duration_ms,height,media_key,preview_image_url,type,url,variants,width&start_time=2026-04-14T16%3A00%3A00.0000000Z&end_time=2026-04-14T19%3A00%3A00.0000000Z",
            """{ "data": [], "meta": {} }""");

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.x.com/2/"),
        };

        XApiClient client = new(httpClient, new MediaSelector());
        XTimelinePage page = await client.GetUserPostsAsync(
            new XUserProfile { UserId = "42", UserName = "sampleuser" },
            "token",
            null,
            new DateTimeOffset(2026, 4, 14, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 19, 0, 0, TimeSpan.Zero),
            null,
            10,
            CancellationToken.None);

        Assert.AreEqual(0, page.Posts.Count);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = [];

        public void AddResponse(string absoluteUri, string responseBody)
        {
            _responses[absoluteUri] = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string absoluteUri = request.RequestUri!.AbsoluteUri;
            if (_responses.TryGetValue(absoluteUri, out string? responseBody))
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                    });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
