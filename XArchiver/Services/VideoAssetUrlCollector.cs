using System.Text.RegularExpressions;
using Microsoft.Playwright;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal sealed class VideoAssetUrlCollector : IVideoAssetUrlCollector
{
    private const string TweetArticleSelector = "article[data-testid='tweet'], article[role='article']";
    private static readonly Regex RawVideoUrlPattern = new(@"https://video\.twimg\.com[^""'\s<]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EscapedVideoUrlPattern = new(@"https:(?:\\\\/\\\\/|\\\\/|\\/)+video\.twimg\.com[^""'\s<]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<IReadOnlyList<string>> CollectAsync(
        IPage page,
        string postId,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string> candidateUrls = new(StringComparer.OrdinalIgnoreCase);
        ILocator postLocator = await ResolvePostLocatorAsync(page, postId).ConfigureAwait(false);

        await AddDomCandidateUrlsAsync(postLocator, candidateUrls).ConfigureAwait(false);
        await AddPerformanceCandidateUrlsAsync(page, candidateUrls).ConfigureAwait(false);
        await AddSerializedCandidateUrlsAsync(page, candidateUrls).ConfigureAwait(false);

        bool activatedPlayback = await TryActivateVideoSurfaceAsync(postLocator, diagnosticsSink, page.Url, cancellationToken).ConfigureAwait(false);
        if (activatedPlayback)
        {
            await page.WaitForTimeoutAsync(500).ConfigureAwait(false);
            if (!IsMediaViewRoute(page.Url))
            {
                await AddDomCandidateUrlsAsync(postLocator, candidateUrls).ConfigureAwait(false);
                await AddPerformanceCandidateUrlsAsync(page, candidateUrls).ConfigureAwait(false);
                await AddSerializedCandidateUrlsAsync(page, candidateUrls).ConfigureAwait(false);
            }
            else
            {
                diagnosticsSink.ReportEvent(
                    new XArchiver.Core.Models.ScraperDiagnosticsEvent
                    {
                        Category = "Video",
                        Message = "Playback activation navigated into a media-view route; using already collected candidates only.",
                        Severity = XArchiver.Core.Models.ScraperDiagnosticsSeverity.Warning,
                        StageText = "Resolving scraped video",
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = page.Url,
                    });
            }
        }

        List<string> resolvedUrls = candidateUrls
            .Where(IsArchivableVideoUrl)
            .ToList();

        diagnosticsSink.ReportEvent(
            new XArchiver.Core.Models.ScraperDiagnosticsEvent
            {
                Category = "Video",
                Message = $"Collected {resolvedUrls.Count} raw video candidates from the page.",
                StageText = "Resolving scraped video",
                TimestampUtc = DateTimeOffset.UtcNow,
                Url = page.Url,
            });

        return resolvedUrls;
    }

    private static async Task AddDomCandidateUrlsAsync(ILocator postLocator, HashSet<string> candidateUrls)
    {
        if (await postLocator.CountAsync().ConfigureAwait(false) == 0)
        {
            return;
        }

        string[] domUrls = await postLocator.EvaluateAsync<string[]>(
            """
            element => {
              const values = new Set();
              const add = value => {
                if (typeof value === "string" && value.trim().length > 0) {
                  values.add(value.trim());
                }
              };

              element.querySelectorAll("video").forEach(video => {
                add(video.currentSrc);
                add(video.src);
                add(video.poster);
                video.querySelectorAll("source[src]").forEach(source => add(source.src));
              });

              return Array.from(values);
            }
            """)
            .ConfigureAwait(false);

        foreach (string domUrl in domUrls)
        {
            candidateUrls.Add(domUrl);
        }
    }

    private static async Task AddSerializedCandidateUrlsAsync(IPage page, HashSet<string> candidateUrls)
    {
        string html = await page.ContentAsync().ConfigureAwait(false);
        AddMatches(html, candidateUrls);

        string[] scriptContents = await page.EvaluateAsync<string[]>(
            """
            () => Array.from(document.scripts)
                .map(script => script.textContent || "")
                .filter(text => text.length > 0)
            """)
            .ConfigureAwait(false);

        foreach (string scriptContent in scriptContents)
        {
            AddMatches(scriptContent, candidateUrls);
        }
    }

    private static async Task AddPerformanceCandidateUrlsAsync(IPage page, HashSet<string> candidateUrls)
    {
        string[] performanceUrls = await page.EvaluateAsync<string[]>(
            """
            () => {
              return performance
                .getEntriesByType("resource")
                .map(entry => entry.name)
                .filter(name => typeof name === "string" && name.includes("video.twimg.com"));
            }
            """)
            .ConfigureAwait(false);

        foreach (string performanceUrl in performanceUrls)
        {
            candidateUrls.Add(performanceUrl);
        }
    }

    private static bool IsArchivableVideoUrl(string candidateUrl)
    {
        return !string.IsNullOrWhiteSpace(candidateUrl) &&
               !candidateUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) &&
               (candidateUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase) ||
                candidateUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ILocator> ResolvePostLocatorAsync(IPage page, string postId)
    {
        ILocator postLocator = page.Locator($"{TweetArticleSelector}:has(a[href*='/status/{postId}'])").First;
        if (await postLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            return postLocator;
        }

        return page.Locator(TweetArticleSelector).First;
    }

    private static async Task<bool> TryActivateVideoSurfaceAsync(
        ILocator postLocator,
        IScraperDiagnosticsSink diagnosticsSink,
        string currentUrl,
        CancellationToken cancellationToken)
    {
        string[] selectors =
        [
            "[data-testid='videoPlayer']",
            "button[aria-label*='Play']",
            "[role='button'][aria-label*='Play']",
            "video",
        ];

        foreach (string selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ILocator control = postLocator.Locator(selector).First;
            if (await control.CountAsync().ConfigureAwait(false) == 0)
            {
                continue;
            }

            try
            {
                await control.ClickAsync(new LocatorClickOptions
                {
                    Timeout = 1500,
                }).ConfigureAwait(false);

                diagnosticsSink.ReportEvent(
                    new XArchiver.Core.Models.ScraperDiagnosticsEvent
                    {
                        Category = "Video",
                        Message = $"Activated video surface with selector {selector}.",
                        Selector = selector,
                        StageText = "Resolving scraped video",
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = currentUrl,
                    });
                return true;
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException or InvalidOperationException)
            {
                diagnosticsSink.ReportEvent(
                    new XArchiver.Core.Models.ScraperDiagnosticsEvent
                    {
                        Category = "Video",
                        Message = $"Video activation selector {selector} could not be used: {exception.Message}",
                        Selector = selector,
                        Severity = XArchiver.Core.Models.ScraperDiagnosticsSeverity.Warning,
                        StageText = "Resolving scraped video",
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = currentUrl,
                    });
            }
        }

        return false;
    }

    private static void AddMatches(string sourceText, HashSet<string> candidateUrls)
    {
        foreach (Match match in RawVideoUrlPattern.Matches(sourceText))
        {
            if (match.Success)
            {
                candidateUrls.Add(NormalizeCandidateUrl(match.Value));
            }
        }

        foreach (Match match in EscapedVideoUrlPattern.Matches(sourceText))
        {
            if (match.Success)
            {
                candidateUrls.Add(NormalizeCandidateUrl(match.Value));
            }
        }
    }

    private static bool IsMediaViewRoute(string currentUrl)
    {
        return currentUrl.Contains("/photo/", StringComparison.OrdinalIgnoreCase) ||
               currentUrl.Contains("/video/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCandidateUrl(string candidateUrl)
    {
        return candidateUrl
            .Replace("\\u002F", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("\\/", "/", StringComparison.Ordinal)
            .Replace(@"\\/", "/", StringComparison.Ordinal)
            .Trim();
    }
}
