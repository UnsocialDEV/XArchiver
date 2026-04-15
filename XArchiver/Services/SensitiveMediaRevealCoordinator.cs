using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class SensitiveMediaRevealCoordinator : ISensitiveMediaRevealCoordinator
{
    private const string TweetArticleSelector = "article[data-testid='tweet'], article[role='article']";
    private static readonly string[] RevealLabels =
    [
        "Show",
        "Yes, view profile",
        "View profile",
    ];

    private readonly ISensitiveMediaDetector _sensitiveMediaDetector;
    private readonly IScrapedPostHtmlParser _scrapedPostHtmlParser;
    private readonly IScraperRouteGuard _scraperRouteGuard;

    public SensitiveMediaRevealCoordinator(
        ISensitiveMediaDetector sensitiveMediaDetector,
        IScrapedPostHtmlParser scrapedPostHtmlParser,
        IScraperRouteGuard scraperRouteGuard)
    {
        _sensitiveMediaDetector = sensitiveMediaDetector;
        _scrapedPostHtmlParser = scrapedPostHtmlParser;
        _scraperRouteGuard = scraperRouteGuard;
    }

    public async Task<SensitiveMediaRevealBatchResult> RevealAsync(
        IPage page,
        string profileUrl,
        string fallbackUsername,
        ScraperExecutionPolicy executionPolicy,
        IScraperFrictionMonitor frictionMonitor,
        ISensitiveMediaRevealAttemptTracker attemptTracker,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPageScreenshotCoordinator screenshotCoordinator,
        CancellationToken cancellationToken)
    {
        SensitiveMediaRevealBatchResult result = new();
        IReadOnlyList<SensitiveMediaCandidate> candidates = await _sensitiveMediaDetector
            .DetectAsync(page, cancellationToken)
            .ConfigureAwait(false);

        foreach (SensitiveMediaCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attemptTracker.HasTerminalOutcome(candidate.PostId))
            {
                result = result with
                {
                    SkippedCount = result.SkippedCount + 1,
                };
                continue;
            }

            SensitiveMediaPostOutcome outcome = await ProcessCandidateAsync(
                page,
                profileUrl,
                fallbackUsername,
                candidate,
                executionPolicy,
                frictionMonitor,
                diagnosticsSink,
                screenshotCoordinator,
                cancellationToken)
                .ConfigureAwait(false);

            switch (outcome.Kind)
            {
                case SensitiveMediaPostOutcomeKind.Revealed:
                    attemptTracker.MarkRevealed(candidate.PostId, outcome);
                    result = result with
                    {
                        RevealedCount = result.RevealedCount + 1,
                    };
                    break;

                case SensitiveMediaPostOutcomeKind.SkippedNoRetry:
                    attemptTracker.MarkSkippedNoRetry(candidate.PostId, outcome);
                    result = result with
                    {
                        SkippedCount = result.SkippedCount + 1,
                    };
                    break;

                default:
                    attemptTracker.MarkFailedArchiveTextOnly(candidate.PostId, outcome);
                    result = result with
                    {
                        FailedArchiveTextOnlyCount = result.FailedArchiveTextOnlyCount + 1,
                    };
                    break;
            }
        }

        return result;
    }

    private static ScraperDiagnosticsEvent CreateEvent(
        string category,
        string message,
        string stageText,
        ScraperDiagnosticsSeverity severity = ScraperDiagnosticsSeverity.Information,
        string? selector = null,
        string? url = null)
    {
        return new ScraperDiagnosticsEvent
        {
            Category = category,
            Message = message,
            Selector = selector,
            Severity = severity,
            StageText = stageText,
            TimestampUtc = DateTimeOffset.UtcNow,
            Url = url,
        };
    }

    private static string BuildPostSelector(string postId)
    {
        return $"{TweetArticleSelector}:has(a[href*='/status/{postId}'])";
    }

    private static async Task<string> GetOuterHtmlAsync(ILocator locator)
    {
        return await locator.EvaluateAsync<string>("element => element.outerHTML").ConfigureAwait(false);
    }

    private static bool HasResolvedMedia(ScrapedPostRecord? post)
    {
        return post is not null && post.Media.Any(media => !media.IsPartial && !string.IsNullOrWhiteSpace(media.SourceUrl));
    }

    private static ScrapedPostRecord MergeFailureState(
        ScrapedPostRecord? preferredSnapshot,
        SensitiveMediaCandidate candidate,
        string reason)
    {
        ScrapedPostRecord snapshot = preferredSnapshot ?? new ScrapedPostRecord
        {
            PostId = candidate.PostId,
            SourceUrl = candidate.PostUrl,
        };

        snapshot.ContainsSensitiveMediaWarning = true;
        snapshot.SensitiveMediaRevealSucceeded = false;
        snapshot.SensitiveMediaFailureReason = reason;
        return snapshot;
    }

    private async Task<SensitiveMediaPostOutcome> ProcessCandidateAsync(
        IPage page,
        string profileUrl,
        string fallbackUsername,
        SensitiveMediaCandidate candidate,
        ScraperExecutionPolicy executionPolicy,
        IScraperFrictionMonitor frictionMonitor,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPageScreenshotCoordinator screenshotCoordinator,
        CancellationToken cancellationToken)
    {
        diagnosticsSink.ReportEvent(
            CreateEvent(
                "SensitiveMedia",
                $"Processing sensitive-media reveal for post {candidate.PostId}.",
                "Revealing sensitive media",
                url: candidate.PostUrl));

        ILocator timelineLocator = page.Locator(BuildPostSelector(candidate.PostId)).First;
        ScrapedPostRecord? initialSnapshot = await TryParseLocatorAsync(timelineLocator, fallbackUsername).ConfigureAwait(false);

        ScrapedPostRecord? inlineSnapshot = await TryRevealAtCurrentRouteAsync(
            page,
            timelineLocator,
            candidate,
            fallbackUsername,
            diagnosticsSink,
            "Inline reveal",
            cancellationToken)
            .ConfigureAwait(false);

        if (HasResolvedMedia(inlineSnapshot))
        {
            inlineSnapshot!.ContainsSensitiveMediaWarning = true;
            inlineSnapshot.SensitiveMediaRevealSucceeded = true;
            inlineSnapshot.SensitiveMediaFailureReason = string.Empty;

            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "SensitiveMedia",
                    $"Inline sensitive-media reveal succeeded for post {candidate.PostId}.",
                    "Revealing sensitive media",
                    url: candidate.PostUrl));

            return new SensitiveMediaPostOutcome
            {
                Kind = SensitiveMediaPostOutcomeKind.Revealed,
                PostSnapshot = inlineSnapshot,
                WarningMarker = candidate.WarningMarker,
            };
        }

        ScraperFrictionSnapshot detailOpenSnapshot = frictionMonitor.RecordSensitiveDetailPageOpen(candidate.PostId);
        if (detailOpenSnapshot.ShouldStop)
        {
            return new SensitiveMediaPostOutcome
            {
                Kind = SensitiveMediaPostOutcomeKind.FailedArchiveTextOnly,
                PostSnapshot = MergeFailureState(initialSnapshot, candidate, detailOpenSnapshot.Reason),
                Reason = detailOpenSnapshot.Reason,
                WarningMarker = candidate.WarningMarker,
            };
        }

        await page.GotoAsync(candidate.PostUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            }).ConfigureAwait(false);

        await WaitForPolicyCooldownAsync(page, executionPolicy, cancellationToken).ConfigureAwait(false);
        await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Revealing sensitive media", await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);

        try
        {
            ILocator detailLocator = page.Locator(BuildPostSelector(candidate.PostId)).First;
            if (await detailLocator.CountAsync().ConfigureAwait(false) == 0)
            {
                detailLocator = page.Locator(TweetArticleSelector).First;
            }

            ScrapedPostRecord? detailSnapshot = await TryRevealAtCurrentRouteAsync(
                page,
                detailLocator,
                candidate,
                fallbackUsername,
                diagnosticsSink,
                "Post route reveal",
                cancellationToken)
                .ConfigureAwait(false);

            if (HasResolvedMedia(detailSnapshot))
            {
                detailSnapshot!.ContainsSensitiveMediaWarning = true;
                detailSnapshot.SensitiveMediaRevealSucceeded = true;
                detailSnapshot.SensitiveMediaFailureReason = string.Empty;

                diagnosticsSink.ReportEvent(
                    CreateEvent(
                        "SensitiveMedia",
                        $"Post-route sensitive-media reveal succeeded for post {candidate.PostId}.",
                        "Revealing sensitive media",
                        url: candidate.PostUrl));

                return new SensitiveMediaPostOutcome
                {
                    Kind = SensitiveMediaPostOutcomeKind.Revealed,
                    PostSnapshot = detailSnapshot,
                    WarningMarker = candidate.WarningMarker,
                };
            }

            ScrapedPostRecord? fallbackSnapshot = detailSnapshot ?? initialSnapshot;
            string reason = "Sensitive media could not be revealed automatically.";
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "SensitiveMedia",
                    $"Sensitive-media reveal failed for post {candidate.PostId}. Archiving text only.",
                    "Revealing sensitive media",
                    ScraperDiagnosticsSeverity.Warning,
                    url: candidate.PostUrl));
            frictionMonitor.RecordSensitiveRevealFailure(candidate.PostId, reason);

            return new SensitiveMediaPostOutcome
            {
                Kind = SensitiveMediaPostOutcomeKind.FailedArchiveTextOnly,
                PostSnapshot = MergeFailureState(fallbackSnapshot, candidate, reason),
                Reason = reason,
                WarningMarker = candidate.WarningMarker,
            };
        }
        finally
        {
            string driftedRoute = page.Url;
            if (await _scraperRouteGuard
                .EnsureExpectedRouteAsync(page, profileUrl, "Returning to profile timeline", diagnosticsSink, cancellationToken)
                .ConfigureAwait(false))
            {
                frictionMonitor.RecordRouteRecovery(driftedRoute);
                await WaitForRouteRecoveryCooldownAsync(page, executionPolicy, cancellationToken).ConfigureAwait(false);
                await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Returning to profile timeline", await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> GetVisiblePostCountAsync(IPage page)
    {
        int articleCount = await page.Locator(TweetArticleSelector).CountAsync().ConfigureAwait(false);
        if (articleCount > 0)
        {
            return articleCount;
        }

        return await page.Locator("[data-testid='primaryColumn'] a[href*='/status/']").CountAsync().ConfigureAwait(false);
    }

    private async Task<ScrapedPostRecord?> TryParseLocatorAsync(ILocator locator, string fallbackUsername)
    {
        if (await locator.CountAsync().ConfigureAwait(false) == 0)
        {
            return null;
        }

        string outerHtml = await GetOuterHtmlAsync(locator).ConfigureAwait(false);
        return _scrapedPostHtmlParser.Parse(outerHtml, fallbackUsername);
    }

    private async Task<ScrapedPostRecord?> TryRevealAtCurrentRouteAsync(
        IPage page,
        ILocator postLocator,
        SensitiveMediaCandidate candidate,
        string fallbackUsername,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText,
        CancellationToken cancellationToken)
    {
        ScrapedPostRecord? initialSnapshot = await TryParseLocatorAsync(postLocator, fallbackUsername).ConfigureAwait(false);
        if (HasResolvedMedia(initialSnapshot))
        {
            return initialSnapshot;
        }

        bool clickedAny = false;
        foreach (string revealLabel in RevealLabels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ILocator revealControl = postLocator.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = revealLabel }).First;
            if (await revealControl.CountAsync().ConfigureAwait(false) == 0)
            {
                revealControl = postLocator.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = revealLabel }).First;
            }

            if (await revealControl.CountAsync().ConfigureAwait(false) == 0)
            {
                continue;
            }

            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "SensitiveMedia",
                    $"{stageText}: attempting reveal click '{revealLabel}' for post {candidate.PostId}.",
                    "Revealing sensitive media",
                    selector: revealLabel,
                    url: page.Url));

            try
            {
                await revealControl.ClickAsync().ConfigureAwait(false);
                clickedAny = true;
                await page.WaitForTimeoutAsync(750).ConfigureAwait(false);
            }
            catch (PlaywrightException exception)
            {
                diagnosticsSink.ReportEvent(
                    CreateEvent(
                        "SensitiveMedia",
                        $"{stageText}: reveal click '{revealLabel}' failed for post {candidate.PostId}: {exception.Message}",
                        "Revealing sensitive media",
                        ScraperDiagnosticsSeverity.Warning,
                        selector: revealLabel,
                        url: page.Url));
            }

            ScrapedPostRecord? currentSnapshot = await TryParseLocatorAsync(postLocator, fallbackUsername).ConfigureAwait(false);
            if (HasResolvedMedia(currentSnapshot))
            {
                return currentSnapshot;
            }
        }

        if (!clickedAny)
        {
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "SensitiveMedia",
                    $"{stageText}: no reveal controls matched for post {candidate.PostId}.",
                    "Revealing sensitive media",
                    ScraperDiagnosticsSeverity.Warning,
                    url: candidate.PostUrl));
        }

        return await TryParseLocatorAsync(postLocator, fallbackUsername).ConfigureAwait(false);
    }

    private static async Task WaitForPolicyCooldownAsync(
        IPage page,
        ScraperExecutionPolicy executionPolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int minimum = executionPolicy.SensitiveRevealCooldownMinimumMilliseconds;
        int maximum = executionPolicy.SensitiveRevealCooldownMaximumMilliseconds;
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

    private static async Task WaitForRouteRecoveryCooldownAsync(
        IPage page,
        ScraperExecutionPolicy executionPolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int minimum = executionPolicy.RouteRecoveryCooldownMinimumMilliseconds;
        int maximum = executionPolicy.RouteRecoveryCooldownMaximumMilliseconds;
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
}
