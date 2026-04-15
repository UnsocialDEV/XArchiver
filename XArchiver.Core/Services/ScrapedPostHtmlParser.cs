using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ScrapedPostHtmlParser : IScrapedPostHtmlParser
{
    private static readonly string[] SensitiveMediaWarningMarkers =
    [
        "Content warning",
        "Adult Content",
        "sensitive content",
        "sensitive material",
        "This media may contain",
    ];

    private static readonly Regex StatusUrlPattern = new(
        @"/(?<username>[^/\?\#]+)/status/(?<postId>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly HtmlParser _htmlParser = new();

    public ScrapedPostRecord? Parse(string articleHtml, string fallbackUsername)
    {
        AngleSharp.Dom.IDocument document = _htmlParser.ParseDocument(articleHtml);
        string? statusUrl = GetStatusUrl(document);
        if (statusUrl is null)
        {
            return null;
        }

        Match match = StatusUrlPattern.Match(statusUrl);
        if (!match.Success)
        {
            return null;
        }

        string? datetime = document.QuerySelector("time")?.GetAttribute("datetime");
        if (!DateTimeOffset.TryParse(datetime, out DateTimeOffset createdAtUtc))
        {
            return null;
        }

        string username = match.Groups["username"].Value;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = fallbackUsername;
        }

        List<ScrapedMediaRecord> media = GetMedia(document, match.Groups["postId"].Value);
        return new ScrapedPostRecord
        {
            ContainsSensitiveMediaWarning = HasSensitiveMediaWarning(document),
            CreatedAtUtc = createdAtUtc,
            Media = media,
            PostId = match.Groups["postId"].Value,
            RawHtml = articleHtml,
            SourceUrl = statusUrl,
            Text = GetText(document),
            Username = username,
        };
    }

    private static bool HasSensitiveMediaWarning(AngleSharp.Dom.IDocument document)
    {
        string documentText = NormalizeText(document.Body?.TextContent ?? document.DocumentElement?.TextContent ?? string.Empty);
        return SensitiveMediaWarningMarkers.Any(
            marker => documentText.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetText(AngleSharp.Dom.IDocument document)
    {
        IReadOnlyList<string> textParts = document.QuerySelectorAll("[data-testid='tweetText']")
            .Select(element => NormalizeText(element.TextContent))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return textParts.Count == 0 ? string.Empty : string.Join(Environment.NewLine, textParts);
    }

    private static List<ScrapedMediaRecord> GetMedia(AngleSharp.Dom.IDocument document, string postId)
    {
        List<ScrapedMediaRecord> media = [];
        HashSet<string> seenImageUrls = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenVideoUrls = new(StringComparer.OrdinalIgnoreCase);
        int imageIndex = 0;
        int videoIndex = 0;

        foreach (string imageUrl in document.QuerySelectorAll("img")
                     .Select(element => element.GetAttribute("src"))
                     .Where(IsArchivableImageUrl)
                     .Cast<string>())
        {
            if (!seenImageUrls.Add(imageUrl))
            {
                continue;
            }

            media.Add(
                new ScrapedMediaRecord
                {
                    Kind = ArchiveMediaKind.Image,
                    MediaKey = $"{postId}_image_{imageIndex++}",
                    SourceUrl = imageUrl,
                });
        }

        foreach (AngleSharp.Dom.IElement videoElement in document.QuerySelectorAll("video"))
        {
            List<string> videoSourceUrls = videoElement
                .QuerySelectorAll("source[src]")
                .Select(element => element.GetAttribute("src"))
                .Append(videoElement.GetAttribute("src"))
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .Where(IsArchivableVideoUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string posterUrl = videoElement.GetAttribute("poster") ?? string.Empty;
            bool hasArchivablePoster = IsArchivableImageUrl(posterUrl);

            if (videoSourceUrls.Count == 0)
            {
                if (!hasArchivablePoster)
                {
                    continue;
                }

                media.Add(
                    new ScrapedMediaRecord
                    {
                        IsPartial = true,
                        Kind = ArchiveMediaKind.Video,
                        MediaKey = $"{postId}_video_{videoIndex++}",
                        PreviewImageUrl = posterUrl,
                        RequiresResolution = true,
                    });
                continue;
            }

            foreach (string videoUrl in videoSourceUrls)
            {
                if (!seenVideoUrls.Add(videoUrl))
                {
                    continue;
                }

                media.Add(
                    new ScrapedMediaRecord
                    {
                        IsPartial = RequiresResolution(videoUrl),
                        Kind = ArchiveMediaKind.Video,
                        ManifestUrl = videoUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? videoUrl : string.Empty,
                        MediaKey = $"{postId}_video_{videoIndex++}",
                        PreviewImageUrl = hasArchivablePoster ? posterUrl : string.Empty,
                        RequiresResolution = RequiresResolution(videoUrl),
                        SourceUrl = videoUrl,
                    });
            }
        }

        return media;
    }

    private static string? GetStatusUrl(AngleSharp.Dom.IDocument document)
    {
        foreach (string? href in document.QuerySelectorAll("a[href*='/status/']")
                     .Select(element => element.GetAttribute("href")))
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : $"https://x.com{href}";
        }

        return null;
    }

    private static bool IsArchivableImageUrl(string? sourceUrl)
    {
        return !string.IsNullOrWhiteSpace(sourceUrl) &&
               sourceUrl.Contains("twimg.com", StringComparison.OrdinalIgnoreCase) &&
               !sourceUrl.Contains("profile_images", StringComparison.OrdinalIgnoreCase) &&
               !sourceUrl.Contains("emoji", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchivableVideoUrl(string? sourceUrl)
    {
        return !string.IsNullOrWhiteSpace(sourceUrl) &&
               !sourceUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) &&
               (sourceUrl.Contains("video.twimg.com", StringComparison.OrdinalIgnoreCase) ||
                sourceUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                sourceUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresResolution(string sourceUrl)
    {
        return string.IsNullOrWhiteSpace(sourceUrl) ||
               sourceUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
