using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Utilities;

namespace XArchiver.Core.Services;

public sealed class XApiClient : IXApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IMediaSelector _mediaSelector;

    public XApiClient(HttpClient httpClient, IMediaSelector mediaSelector)
    {
        _httpClient = httpClient;
        _mediaSelector = mediaSelector;
    }

    public async Task<XUserProfile> GetUserAsync(string username, string bearerToken, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest($"users/by/username/{Uri.EscapeDataString(username)}?user.fields=id,name,username", bearerToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement data = document.RootElement.GetProperty("data");
        return new XUserProfile
        {
            DisplayName = data.GetProperty("name").GetString() ?? string.Empty,
            UserId = data.GetProperty("id").GetString() ?? string.Empty,
            UserName = data.GetProperty("username").GetString() ?? username,
        };
    }

    public async Task<XTimelinePage> GetUserPostsAsync(
        XUserProfile user,
        string bearerToken,
        string? sinceId,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        string? paginationToken,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string query = BuildTimelineQuery(user.UserId, sinceId, startTimeUtc, endTimeUtc, paginationToken, pageSize);
        using HttpRequestMessage request = CreateRequest(query, bearerToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        JsonElement root = document.RootElement;
        Dictionary<string, XMediaDefinition> mediaByKey = ParseMediaLookup(root);
        List<ArchivedPostRecord> posts = [];

        if (root.TryGetProperty("data", out JsonElement dataElement))
        {
            foreach (JsonElement postElement in dataElement.EnumerateArray())
            {
                posts.Add(ParsePost(postElement, user, mediaByKey));
            }
        }

        string? nextToken = null;
        if (root.TryGetProperty("meta", out JsonElement metaElement) &&
            metaElement.TryGetProperty("next_token", out JsonElement nextTokenElement))
        {
            nextToken = nextTokenElement.GetString();
        }

        return new XTimelinePage
        {
            NextToken = nextToken,
            Posts = posts,
        };
    }

    public async Task<PreviewPageResult> GetUserPreviewPostsAsync(
        XUserProfile user,
        string bearerToken,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        string? paginationToken,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string query = BuildTimelineQuery(user.UserId, null, startTimeUtc, endTimeUtc, paginationToken, pageSize);
        using HttpRequestMessage request = CreateRequest(query, bearerToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        JsonElement root = document.RootElement;
        Dictionary<string, XMediaDefinition> mediaByKey = ParseMediaLookup(root);
        List<PreviewPostRecord> posts = [];

        if (root.TryGetProperty("data", out JsonElement dataElement))
        {
            foreach (JsonElement postElement in dataElement.EnumerateArray())
            {
                posts.Add(ParsePreviewPost(postElement, user, mediaByKey));
            }
        }

        string? nextToken = null;
        if (root.TryGetProperty("meta", out JsonElement metaElement) &&
            metaElement.TryGetProperty("next_token", out JsonElement nextTokenElement))
        {
            nextToken = nextTokenElement.GetString();
        }

        return new PreviewPageResult
        {
            NextToken = nextToken,
            Posts = posts,
            ScannedPostReads = posts.Count,
        };
    }

    private static string BuildTimelineQuery(
        string userId,
        string? sinceId,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        string? paginationToken,
        int pageSize)
    {
        List<string> parameters =
        [
            $"max_results={pageSize}",
            "tweet.fields=attachments,author_id,conversation_id,created_at,in_reply_to_user_id,public_metrics,referenced_tweets,text",
            "expansions=attachments.media_keys",
            "media.fields=duration_ms,height,media_key,preview_image_url,type,url,variants,width",
        ];

        if (!string.IsNullOrWhiteSpace(sinceId))
        {
            parameters.Add($"since_id={Uri.EscapeDataString(sinceId)}");
        }

        if (startTimeUtc.HasValue)
        {
            parameters.Add($"start_time={Uri.EscapeDataString(startTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (endTimeUtc.HasValue)
        {
            parameters.Add($"end_time={Uri.EscapeDataString(endTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (!string.IsNullOrWhiteSpace(paginationToken))
        {
            parameters.Add($"pagination_token={Uri.EscapeDataString(paginationToken)}");
        }

        return $"users/{Uri.EscapeDataString(userId)}/tweets?{string.Join("&", parameters)}";
    }

    private static HttpRequestMessage CreateRequest(string relativeUri, string bearerToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException($"X API request failed ({(int)response.StatusCode}): {content}");
    }

    private static Dictionary<string, XMediaDefinition> ParseMediaLookup(JsonElement root)
    {
        Dictionary<string, XMediaDefinition> mediaByKey = [];
        if (!root.TryGetProperty("includes", out JsonElement includesElement) ||
            !includesElement.TryGetProperty("media", out JsonElement mediaElement))
        {
            return mediaByKey;
        }

        foreach (JsonElement mediaDefinitionElement in mediaElement.EnumerateArray())
        {
            XMediaDefinition definition = new()
            {
                DurationMs = TryGetInt64(mediaDefinitionElement, "duration_ms"),
                Height = TryGetInt32(mediaDefinitionElement, "height"),
                MediaKey = mediaDefinitionElement.GetProperty("media_key").GetString() ?? string.Empty,
                PreviewImageUrl = TryGetString(mediaDefinitionElement, "preview_image_url"),
                RawJson = mediaDefinitionElement.GetRawText(),
                Type = mediaDefinitionElement.GetProperty("type").GetString() ?? string.Empty,
                Url = TryGetString(mediaDefinitionElement, "url"),
                Width = TryGetInt32(mediaDefinitionElement, "width"),
            };

            if (mediaDefinitionElement.TryGetProperty("variants", out JsonElement variantsElement))
            {
                foreach (JsonElement variantElement in variantsElement.EnumerateArray())
                {
                    string? url = TryGetString(variantElement, "url");
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    definition.Variants.Add(
                        new XMediaVariant
                        {
                            BitRate = TryGetInt32(variantElement, "bit_rate"),
                            ContentType = TryGetString(variantElement, "content_type") ?? string.Empty,
                            Url = url,
                        });
                }
            }

            mediaByKey[definition.MediaKey] = definition;
        }

        return mediaByKey;
    }

    private ArchivedPostRecord ParsePost(JsonElement postElement, XUserProfile user, IReadOnlyDictionary<string, XMediaDefinition> mediaByKey)
    {
        string postId = postElement.GetProperty("id").GetString() ?? string.Empty;
        string? inReplyToUserId = TryGetString(postElement, "in_reply_to_user_id");
        List<string> referenceTypes = ParseReferenceTypes(postElement);
        List<XMediaDefinition> mediaDefinitions = ParseMediaDefinitions(postElement, mediaByKey);

        return new ArchivedPostRecord
        {
            ArchivedAtUtc = null,
            ConversationId = TryGetString(postElement, "conversation_id"),
            CreatedAtUtc = DateTimeOffset.Parse(postElement.GetProperty("created_at").GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            InReplyToUserId = inReplyToUserId,
            LikeCount = TryGetMetric(postElement, "like_count"),
            MediaDetails = CreateMediaDetails(mediaDefinitions),
            Media = _mediaSelector.SelectMedia(postId, mediaDefinitions).ToList(),
            PostId = postId,
            PostType = PostTypeClassifier.Classify(!string.IsNullOrWhiteSpace(inReplyToUserId), referenceTypes),
            QuoteCount = TryGetMetric(postElement, "quote_count"),
            RawPayloadJson = CreateRawPayloadJson(postElement, mediaDefinitions),
            ReferencedPosts = ParseReferencedPosts(postElement),
            ReplyCount = TryGetMetric(postElement, "reply_count"),
            RepostCount = TryGetMetric(postElement, "retweet_count"),
            Text = postElement.GetProperty("text").GetString() ?? string.Empty,
            UserId = user.UserId,
            Username = user.UserName,
        };
    }

    private PreviewPostRecord ParsePreviewPost(JsonElement postElement, XUserProfile user, IReadOnlyDictionary<string, XMediaDefinition> mediaByKey)
    {
        string postId = postElement.GetProperty("id").GetString() ?? string.Empty;
        string? inReplyToUserId = TryGetString(postElement, "in_reply_to_user_id");
        List<string> referenceTypes = ParseReferenceTypes(postElement);
        List<XMediaDefinition> mediaDefinitions = ParseMediaDefinitions(postElement, mediaByKey);
        IReadOnlyList<ArchivedMediaRecord> selectedMedia = _mediaSelector.SelectMedia(postId, mediaDefinitions).ToList();

        return new PreviewPostRecord
        {
            ConversationId = TryGetString(postElement, "conversation_id"),
            CreatedAtUtc = DateTimeOffset.Parse(postElement.GetProperty("created_at").GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            InReplyToUserId = inReplyToUserId,
            LikeCount = TryGetMetric(postElement, "like_count"),
            MediaDetails = CreateMediaDetails(mediaDefinitions),
            Media = selectedMedia
                .Select(media => MapPreviewMedia(media, mediaDefinitions))
                .ToList(),
            PostId = postId,
            PostType = PostTypeClassifier.Classify(!string.IsNullOrWhiteSpace(inReplyToUserId), referenceTypes),
            QuoteCount = TryGetMetric(postElement, "quote_count"),
            RawPayloadJson = CreateRawPayloadJson(postElement, mediaDefinitions),
            ReferencedPosts = ParseReferencedPosts(postElement),
            ReplyCount = TryGetMetric(postElement, "reply_count"),
            RepostCount = TryGetMetric(postElement, "retweet_count"),
            Text = postElement.GetProperty("text").GetString() ?? string.Empty,
            UserId = user.UserId,
            Username = user.UserName,
        };
    }

    private static List<ArchivedMediaDetailRecord> CreateMediaDetails(IReadOnlyList<XMediaDefinition> mediaDefinitions)
    {
        return mediaDefinitions
            .Select(
                definition => new ArchivedMediaDetailRecord
                {
                    DurationMs = definition.DurationMs,
                    Height = definition.Height,
                    MediaKey = definition.MediaKey,
                    MediaType = definition.Type,
                    PreviewImageUrl = definition.PreviewImageUrl,
                    Url = definition.Url,
                    Variants = definition.Variants
                        .Select(
                            variant => new ArchivedMediaVariantRecord
                            {
                                BitRate = variant.BitRate,
                                ContentType = variant.ContentType,
                                Url = variant.Url,
                            })
                        .ToList(),
                    Width = definition.Width,
                })
            .ToList();
    }

    private static string CreateRawPayloadJson(JsonElement postElement, IReadOnlyList<XMediaDefinition> mediaDefinitions)
    {
        JsonArray mediaArray = [];
        foreach (XMediaDefinition definition in mediaDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.RawJson))
            {
                mediaArray.Add(JsonNode.Parse(definition.RawJson));
            }
        }

        JsonObject payload = new()
        {
            ["media"] = mediaArray,
            ["post"] = JsonNode.Parse(postElement.GetRawText()),
        };

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<ArchivedReferencedPostRecord> ParseReferencedPosts(JsonElement postElement)
    {
        if (!postElement.TryGetProperty("referenced_tweets", out JsonElement referencedElement))
        {
            return [];
        }

        List<ArchivedReferencedPostRecord> references = [];
        foreach (JsonElement referencedPost in referencedElement.EnumerateArray())
        {
            references.Add(
                new ArchivedReferencedPostRecord
                {
                    ReferenceType = TryGetString(referencedPost, "type") ?? string.Empty,
                    ReferencedPostId = TryGetString(referencedPost, "id") ?? string.Empty,
                });
        }

        return references;
    }

    private static PreviewMediaRecord MapPreviewMedia(ArchivedMediaRecord media, IReadOnlyList<XMediaDefinition> mediaDefinitions)
    {
        XMediaDefinition? definition = mediaDefinitions.FirstOrDefault(candidate => string.Equals(candidate.MediaKey, media.MediaKey, StringComparison.Ordinal));
        return new PreviewMediaRecord
        {
            DurationMs = media.DurationMs,
            Height = media.Height,
            IsPartial = media.IsPartial,
            Kind = media.Kind,
            MediaKey = media.MediaKey,
            PostId = media.PostId,
            PreviewImageUrl = media.Kind == ArchiveMediaKind.Image ? media.SourceUrl : definition?.PreviewImageUrl,
            SourceUrl = media.SourceUrl,
            Width = media.Width,
        };
    }

    private static List<XMediaDefinition> ParseMediaDefinitions(JsonElement postElement, IReadOnlyDictionary<string, XMediaDefinition> mediaByKey)
    {
        if (!postElement.TryGetProperty("attachments", out JsonElement attachmentsElement) ||
            !attachmentsElement.TryGetProperty("media_keys", out JsonElement mediaKeysElement))
        {
            return [];
        }

        List<XMediaDefinition> definitions = [];
        foreach (JsonElement mediaKeyElement in mediaKeysElement.EnumerateArray())
        {
            string mediaKey = mediaKeyElement.GetString() ?? string.Empty;
            if (mediaByKey.TryGetValue(mediaKey, out XMediaDefinition? definition))
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private static List<string> ParseReferenceTypes(JsonElement postElement)
    {
        if (!postElement.TryGetProperty("referenced_tweets", out JsonElement referencedElement))
        {
            return [];
        }

        List<string> referenceTypes = [];
        foreach (JsonElement referencedPost in referencedElement.EnumerateArray())
        {
            string? referenceType = TryGetString(referencedPost, "type");
            if (!string.IsNullOrWhiteSpace(referenceType))
            {
                referenceTypes.Add(referenceType);
            }
        }

        return referenceTypes;
    }

    private static int TryGetMetric(JsonElement postElement, string propertyName)
    {
        if (!postElement.TryGetProperty("public_metrics", out JsonElement metricsElement) ||
            !metricsElement.TryGetProperty(propertyName, out JsonElement metricElement))
        {
            return 0;
        }

        return metricElement.GetInt32();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetString()
            : null;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetInt32()
            : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetInt64()
            : null;
    }
}
