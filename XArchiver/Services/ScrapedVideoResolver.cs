using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScrapedVideoResolver : IScrapedVideoResolver
{
    private static readonly Regex RawVideoUrlPattern = new(@"https://video\.twimg\.com[^""'\s<]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EscapedVideoUrlPattern = new(@"https:(?:\\\\/\\\\/|\\\\/|\\/)+video\.twimg\.com[^""'\s<]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IScrapedVideoStreamResolver _scrapedVideoStreamResolver;
    private readonly IVideoAssetUrlCollector _videoAssetUrlCollector;

    public ScrapedVideoResolver(
        IVideoAssetUrlCollector videoAssetUrlCollector,
        IScrapedVideoStreamResolver scrapedVideoStreamResolver)
    {
        _videoAssetUrlCollector = videoAssetUrlCollector;
        _scrapedVideoStreamResolver = scrapedVideoStreamResolver;
    }

    public async Task<ScrapedPostRecord> ResolveAsync(
        IPage page,
        string profileUrl,
        ScrapedPostRecord post,
        ScraperExecutionPolicy executionPolicy,
        IScraperFrictionMonitor frictionMonitor,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPageScreenshotCoordinator screenshotCoordinator,
        CancellationToken cancellationToken)
    {
        if (!NeedsVideoResolution(post))
        {
            return post;
        }

        ScraperFrictionSnapshot detailOpenSnapshot = frictionMonitor.RecordVideoDetailPageOpen(post.PostId);
        if (detailOpenSnapshot.ShouldStop)
        {
            return BuildFallbackPost(post, detailOpenSnapshot.Reason);
        }

        diagnosticsSink.ReportEvent(
            new ScraperDiagnosticsEvent
            {
                Category = "Video",
                Message = $"Resolving video assets for post {post.PostId}.",
                StageText = "Resolving scraped video",
                TimestampUtc = DateTimeOffset.UtcNow,
                Url = post.SourceUrl,
            });

        IPage? detailPage = null;
        ConcurrentBag<string> responseCandidateUrls = [];
        List<Task> responseCaptureTasks = [];
        EventHandler<IResponse>? responseHandler = null;
        try
        {
            detailPage = await page.Context.NewPageAsync().ConfigureAwait(false);
            responseHandler = (_, response) =>
            {
                if (!ShouldInspectResponse(response, post.PostId))
                {
                    return;
                }

                responseCaptureTasks.Add(CaptureResponseCandidatesAsync(response, responseCandidateUrls));
            };
            detailPage.Response += responseHandler;

            await detailPage.GotoAsync(post.SourceUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 7000,
            }).ConfigureAwait(false);
            await detailPage.WaitForTimeoutAsync(1200).ConfigureAwait(false);
            await WaitForPolicyCooldownAsync(detailPage, executionPolicy, cancellationToken).ConfigureAwait(false);
            await screenshotCoordinator.CaptureAsync(detailPage, diagnosticsSink, "Resolving scraped video", visiblePostCount: 1).ConfigureAwait(false);
            await WaitForResponseCapturesAsync(responseCaptureTasks).ConfigureAwait(false);

            List<string> candidateUrls = post.Media
                .SelectMany(media => new[] { media.SourceUrl, media.ManifestUrl })
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            MergeCandidateUrls(candidateUrls, responseCandidateUrls);

            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Video",
                    Message = $"Opened post detail page for video resolution: {detailPage.Url}",
                    StageText = "Resolving scraped video",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = detailPage.Url,
                });

            IReadOnlyList<string> collectedUrls = await _videoAssetUrlCollector
                .CollectAsync(detailPage, post.PostId, diagnosticsSink, cancellationToken)
                .ConfigureAwait(false);
            foreach (string collectedUrl in collectedUrls)
            {
                if (!candidateUrls.Contains(collectedUrl, StringComparer.OrdinalIgnoreCase))
                {
                    candidateUrls.Add(collectedUrl);
                }
            }
            await WaitForResponseCapturesAsync(responseCaptureTasks).ConfigureAwait(false);
            MergeCandidateUrls(candidateUrls, responseCandidateUrls);

            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Video",
                    Message = $"Collected {candidateUrls.Count} candidate video asset URLs for post {post.PostId}.",
                    StageText = "Resolving scraped video",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = post.SourceUrl,
                });

            ScrapedVideoStreamResolution resolution = await _scrapedVideoStreamResolver
                .ResolveAsync(candidateUrls, cancellationToken)
                .ConfigureAwait(false);

            if (resolution.WasResolved)
            {
                diagnosticsSink.ReportEvent(
                    new ScraperDiagnosticsEvent
                    {
                        Category = "Video",
                        Message = $"Resolved video URL for post {post.PostId}: {resolution.ResolvedUrl}",
                        StageText = "Resolving scraped video",
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = post.SourceUrl,
                    });

                return BuildResolvedPost(post, resolution);
            }

            ScrapedPostRecord fallbackPost = BuildFallbackPost(post, resolution.FailureReason);
            frictionMonitor.RecordVideoResolutionFailure(post.PostId, resolution.FailureReason);
            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Video",
                    Message = $"Video resolution completed with fallback for post {post.PostId}: {fallbackPost.VideoResolutionStatus}. {resolution.FailureReason}",
                    Severity = ScraperDiagnosticsSeverity.Warning,
                    StageText = "Resolving scraped video",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = post.SourceUrl,
                });

            return fallbackPost;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException or InvalidOperationException)
        {
            ScrapedPostRecord fallbackPost = BuildFallbackPost(post, exception.Message);
            frictionMonitor.RecordVideoResolutionFailure(post.PostId, exception.Message);
            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Video",
                    Message = $"Video resolution failed for post {post.PostId}; continuing with {fallbackPost.VideoResolutionStatus}. {exception.Message}",
                    Severity = ScraperDiagnosticsSeverity.Warning,
                    StageText = "Resolving scraped video",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = post.SourceUrl,
                });
            return fallbackPost;
        }
        finally
        {
            if (detailPage is not null)
            {
                if (responseHandler is not null)
                {
                    detailPage.Response -= responseHandler;
                }

                await detailPage.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task WaitForPolicyCooldownAsync(
        IPage page,
        ScraperExecutionPolicy executionPolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int minimum = executionPolicy.VideoDetailCooldownMinimumMilliseconds;
        int maximum = executionPolicy.VideoDetailCooldownMaximumMilliseconds;
        if (maximum <= 0)
        {
            return;
        }

        int delay = minimum >= maximum
            ? minimum
            : Random.Shared.Next(minimum, maximum + 1);
        if (delay > 0)
        {
            await page.WaitForTimeoutAsync(delay).ConfigureAwait(false);
        }
    }

    private static ScrapedPostRecord BuildFallbackPost(ScrapedPostRecord post, string failureReason)
    {
        List<ScrapedMediaRecord> media = [];
        bool usedPosterFallback = false;

        foreach (ScrapedMediaRecord item in post.Media)
        {
            if (item.Kind != ArchiveMediaKind.Video || !MediaNeedsResolution(item))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceUrl))
                {
                    media.Add(CloneMedia(item));
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(item.PreviewImageUrl))
            {
                continue;
            }

            usedPosterFallback = true;
            media.Add(
                new ScrapedMediaRecord
                {
                    Height = item.Height,
                    IsPartial = true,
                    Kind = ArchiveMediaKind.Image,
                    MediaKey = $"{item.MediaKey}_poster",
                    PreviewImageUrl = item.PreviewImageUrl,
                    SourceUrl = item.PreviewImageUrl,
                    Width = item.Width,
                });
        }

        return ClonePost(
            post,
            media,
            usedPosterFallback ? "PosterFallback" : "Failed",
            failureReason);
    }

    private static ScrapedPostRecord BuildResolvedPost(ScrapedPostRecord post, ScrapedVideoStreamResolution resolution)
    {
        List<ScrapedMediaRecord> media = [];
        bool appliedResolution = false;

        foreach (ScrapedMediaRecord item in post.Media)
        {
            if (item.Kind != ArchiveMediaKind.Video || !MediaNeedsResolution(item))
            {
                if (!string.IsNullOrWhiteSpace(item.SourceUrl))
                {
                    media.Add(CloneMedia(item));
                }

                continue;
            }

            if (appliedResolution)
            {
                if (!string.IsNullOrWhiteSpace(item.PreviewImageUrl))
                {
                    media.Add(
                        new ScrapedMediaRecord
                        {
                            Height = item.Height,
                            IsPartial = true,
                            Kind = ArchiveMediaKind.Image,
                            MediaKey = $"{item.MediaKey}_poster",
                            PreviewImageUrl = item.PreviewImageUrl,
                            SourceUrl = item.PreviewImageUrl,
                            Width = item.Width,
                        });
                }

                continue;
            }

            media.Add(
                new ScrapedMediaRecord
                {
                    DurationMs = item.DurationMs,
                    Height = item.Height,
                    IsPartial = false,
                    Kind = ArchiveMediaKind.Video,
                    ManifestUrl = resolution.ManifestUrl,
                    MediaKey = item.MediaKey,
                    PreviewImageUrl = item.PreviewImageUrl,
                    RequiresResolution = false,
                    SourceUrl = resolution.ResolvedUrl,
                    Width = item.Width,
                });
            appliedResolution = true;
        }

        return ClonePost(post, media, resolution.ResolutionKind, string.Empty);
    }

    private static ScrapedMediaRecord CloneMedia(ScrapedMediaRecord media)
    {
        return new ScrapedMediaRecord
        {
            DurationMs = media.DurationMs,
            Height = media.Height,
            IsPartial = media.IsPartial,
            Kind = media.Kind,
            ManifestUrl = media.ManifestUrl,
            MediaKey = media.MediaKey,
            PreviewImageUrl = media.PreviewImageUrl,
            RequiresResolution = media.RequiresResolution,
            SourceUrl = media.SourceUrl,
            Width = media.Width,
        };
    }

    private static ScrapedPostRecord ClonePost(
        ScrapedPostRecord post,
        List<ScrapedMediaRecord> media,
        string videoResolutionStatus,
        string videoResolutionFailureReason)
    {
        return new ScrapedPostRecord
        {
            ContainsSensitiveMediaWarning = post.ContainsSensitiveMediaWarning,
            CreatedAtUtc = post.CreatedAtUtc,
            Media = media,
            PostId = post.PostId,
            RawHtml = post.RawHtml,
            SensitiveMediaFailureReason = post.SensitiveMediaFailureReason,
            SensitiveMediaRevealSucceeded = post.SensitiveMediaRevealSucceeded,
            SourceUrl = post.SourceUrl,
            Text = post.Text,
            Username = post.Username,
            VideoResolutionFailureReason = videoResolutionFailureReason,
            VideoResolutionStatus = videoResolutionStatus,
        };
    }

    private static bool MediaNeedsResolution(ScrapedMediaRecord media)
    {
        return media.Kind == ArchiveMediaKind.Video &&
               (media.RequiresResolution ||
                string.IsNullOrWhiteSpace(media.SourceUrl) ||
                media.SourceUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase));
    }

    private static bool NeedsVideoResolution(ScrapedPostRecord post)
    {
        return post.Media.Any(MediaNeedsResolution);
    }

    private static async Task CaptureResponseCandidatesAsync(IResponse response, ConcurrentBag<string> responseCandidateUrls)
    {
        try
        {
            responseCandidateUrls.Add(response.Url);

            string contentType = response.Headers.TryGetValue("content-type", out string? headerValue)
                ? headerValue
                : string.Empty;
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase) &&
                !response.Url.Contains("graphql", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string body = await response.TextAsync().ConfigureAwait(false);
            AddMatches(body, responseCandidateUrls);
        }
        catch
        {
            // Best-effort response inspection only.
        }
    }

    private static void AddMatches(string sourceText, ConcurrentBag<string> responseCandidateUrls)
    {
        foreach (Match match in RawVideoUrlPattern.Matches(sourceText))
        {
            if (match.Success)
            {
                responseCandidateUrls.Add(NormalizeCandidateUrl(match.Value));
            }
        }

        foreach (Match match in EscapedVideoUrlPattern.Matches(sourceText))
        {
            if (match.Success)
            {
                responseCandidateUrls.Add(NormalizeCandidateUrl(match.Value));
            }
        }
    }

    private static void MergeCandidateUrls(List<string> candidateUrls, IEnumerable<string> additionalUrls)
    {
        foreach (string additionalUrl in additionalUrls
                     .Where(url => !string.IsNullOrWhiteSpace(url))
                     .Select(NormalizeCandidateUrl)
                     .Where(url => url.Contains(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                   url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!candidateUrls.Contains(additionalUrl, StringComparer.OrdinalIgnoreCase))
            {
                candidateUrls.Add(additionalUrl);
            }
        }
    }

    private static string NormalizeCandidateUrl(string candidateUrl)
    {
        return candidateUrl
            .Replace("\\u002F", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("\\/", "/", StringComparison.Ordinal)
            .Replace(@"\\/", "/", StringComparison.Ordinal)
            .Trim();
    }

    private static bool ShouldInspectResponse(IResponse response, string postId)
    {
        if (response.Url.Contains("video.twimg.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return response.Url.Contains(postId, StringComparison.OrdinalIgnoreCase) ||
               response.Url.Contains("graphql", StringComparison.OrdinalIgnoreCase) ||
               response.Url.Contains("TweetDetail", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitForResponseCapturesAsync(List<Task> responseCaptureTasks)
    {
        Task[] pendingTasks = responseCaptureTasks
            .Where(task => !task.IsCompleted)
            .ToArray();
        if (pendingTasks.Length == 0)
        {
            return;
        }

        await Task.WhenAll(pendingTasks).ConfigureAwait(false);
    }
}
