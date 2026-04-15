using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class PlaywrightProfileWebScraper : IProfileWebScraper
{
    private const int MaximumScrapePasses = 2;
    private const string PrimaryColumnSelector = "[data-testid='primaryColumn']";
    private const string StatusLinkSelector = "[data-testid='primaryColumn'] a[href*='/status/']";
    private const string TweetArticleSelector = "article[data-testid='tweet'], article[role='article']";
    private readonly IScraperGateHandler _scraperGateHandler;
    private readonly IScraperRouteGuard _scraperRouteGuard;
    private readonly IScrapedPostHtmlParser _scrapedPostHtmlParser;
    private readonly IScrapedVideoResolver _scrapedVideoResolver;
    private readonly IScraperExecutionPolicyProvider _scraperExecutionPolicyProvider;
    private readonly IScraperSessionStateInspector _scraperSessionStateInspector;
    private readonly ISensitiveMediaRevealCoordinator _sensitiveMediaRevealCoordinator;
    private readonly IScraperSessionLockCleaner _scraperSessionLockCleaner;
    private readonly IScraperSessionStore _scraperSessionStore;

    public PlaywrightProfileWebScraper(
        IScrapedPostHtmlParser scrapedPostHtmlParser,
        IScraperSessionStore scraperSessionStore,
        IScraperGateHandler scraperGateHandler,
        IScrapedVideoResolver scrapedVideoResolver,
        IScraperExecutionPolicyProvider scraperExecutionPolicyProvider,
        ISensitiveMediaRevealCoordinator sensitiveMediaRevealCoordinator,
        IScraperRouteGuard scraperRouteGuard,
        IScraperSessionLockCleaner scraperSessionLockCleaner,
        IScraperSessionStateInspector scraperSessionStateInspector)
    {
        _scrapedPostHtmlParser = scrapedPostHtmlParser;
        _scraperSessionStore = scraperSessionStore;
        _scraperGateHandler = scraperGateHandler;
        _scrapedVideoResolver = scrapedVideoResolver;
        _scraperExecutionPolicyProvider = scraperExecutionPolicyProvider;
        _sensitiveMediaRevealCoordinator = sensitiveMediaRevealCoordinator;
        _scraperRouteGuard = scraperRouteGuard;
        _scraperSessionLockCleaner = scraperSessionLockCleaner;
        _scraperSessionStateInspector = scraperSessionStateInspector;
    }

    public Task<bool> HasSessionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_scraperSessionStore.GetSessionInfo()?.IsValidated == true);
    }

    public async Task<IReadOnlyList<ScrapedPostRecord>> ScrapeAsync(
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPauseGate pauseGate,
        IScraperRunControl runControl,
        bool preferVisibleBrowser,
        CancellationToken cancellationToken)
    {
        ScraperBrowserSessionInfo sessionInfo = _scraperSessionStore.GetSessionInfo()
            ?? throw new InvalidOperationException("Open the X login browser and complete login before starting a web scrape.");

        if (!sessionInfo.IsValidated)
        {
            throw new InvalidOperationException("Validate the dedicated X browser session before starting a web scrape.");
        }

        using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        ScraperExecutionPolicy executionPolicy = _scraperExecutionPolicyProvider.GetPolicy(request.ExecutionMode);
        List<bool> browserModes = preferVisibleBrowser ? [false] : [true, false];

        foreach (bool headless in browserModes.Take(MaximumScrapePasses))
        {
            IReadOnlyList<ScrapedPostRecord> posts = await ExecutePassAsync(
                playwright,
                request,
                progress,
                diagnosticsSink,
                pauseGate,
                runControl,
                executionPolicy,
                sessionInfo,
                headless,
                cancellationToken).ConfigureAwait(false);

            if (runControl.HasConservativeStopRequested)
            {
                return posts;
            }

            if (posts.Count > 0 || !headless)
            {
                return posts;
            }

            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "Browser",
                    "Retrying in visible browser because the headless pass found no posts.",
                    "Retrying in visible browser",
                    ScraperDiagnosticsSeverity.Warning,
                    url: request.ProfileUrl));
        }

        return [];
    }

    public async Task<bool> ValidateSessionAsync(string profileUrl, CancellationToken cancellationToken)
    {
        ScraperBrowserSessionInfo sessionInfo = _scraperSessionStore.GetSessionInfo()
            ?? throw new InvalidOperationException("Open the X login browser before validating the scraper session.");
        using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        string destination = string.IsNullOrWhiteSpace(profileUrl)
            ? "https://x.com/home"
            : profileUrl;

        bool isValid = await TryValidateRunningBrowserSessionAsync(playwright, sessionInfo, destination, cancellationToken).ConfigureAwait(false);
        if (!isValid)
        {
            isValid = await TryValidatePersistentSessionAsync(playwright, sessionInfo, destination, cancellationToken).ConfigureAwait(false);
        }

        _scraperSessionStore.SaveSessionInfo(
            sessionInfo with
            {
                IsInitialized = true,
                IsValidated = isValid,
                LastValidatedUtc = isValid ? DateTimeOffset.UtcNow : null,
            });

        return isValid;
    }

    private static ScraperDiagnosticsEvent CreateEvent(
        string category,
        string message,
        string stageText,
        ScraperDiagnosticsSeverity severity = ScraperDiagnosticsSeverity.Information,
        string? artifactPath = null,
        string? selector = null,
        string? url = null)
    {
        return new ScraperDiagnosticsEvent
        {
            ArtifactPath = artifactPath,
            Category = category,
            Message = message,
            Selector = selector,
            Severity = severity,
            StageText = stageText,
            TimestampUtc = DateTimeOffset.UtcNow,
            Url = url,
        };
    }

    private static WebArchiveProgressSnapshot CreateProgressSnapshot(
        string stageText,
        int targetPostCount,
        int collectedPostCount,
        int visiblePostCount,
        string currentUrl,
        string pageTitle,
        string latestScreenshotPath,
        ScraperRunState runState,
        string blockingReason = "")
    {
        return new WebArchiveProgressSnapshot
        {
            BlockingReason = blockingReason,
            CollectedPostCount = collectedPostCount,
            CurrentUrl = currentUrl,
            LatestScreenshotPath = latestScreenshotPath,
            PageTitle = pageTitle,
            RunState = runState,
            StageText = stageText,
            TargetPostCount = targetPostCount,
            VisiblePostCount = visiblePostCount,
        };
    }

    private async Task CaptureHtmlSnapshotAsync(IPage page, IScraperDiagnosticsSink diagnosticsSink, string artifactKey, string stageText)
    {
        string htmlPath = Path.Combine(
            diagnosticsSink.DiagnosticsDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmssfff}_{artifactKey}.html");
        string html = await page.ContentAsync().ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlPath, html).ConfigureAwait(false);

        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Artifact",
                $"Saved HTML snapshot: {htmlPath}",
                stageText,
                artifactPath: htmlPath,
                url: page.Url));
    }

    private async Task<IReadOnlyList<ScrapedPostRecord>> ExecutePassAsync(
        IPlaywright playwright,
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPauseGate pauseGate,
        IScraperRunControl runControl,
        ScraperExecutionPolicy executionPolicy,
        ScraperBrowserSessionInfo sessionInfo,
        bool headless,
        CancellationToken cancellationToken)
    {
        IBrowserContext? context = null;
        IPage? page = null;
        string latestScreenshotPath = string.Empty;
        string currentStageText = headless ? "Opening headless browser" : "Opening visible browser";
        List<ScrapedPostRecord> scrapedPosts = [];
        IScraperPageScreenshotCoordinator screenshotCoordinator = new ScraperPageScreenshotCoordinator();
        IScraperFrictionMonitor frictionMonitor = new ScraperFrictionMonitor(executionPolicy);
        ISensitiveMediaRevealAttemptTracker sensitiveMediaRevealAttemptTracker = new SensitiveMediaRevealAttemptTracker();
        HashSet<string> seenPostIds = new(StringComparer.Ordinal);
            int noNewPostCycles = 0;
            bool reachedArchiveBoundary = false;

        try
        {
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "Browser",
                    headless ? "Launching headless browser context." : "Launching visible browser context.",
                    currentStageText,
                    url: request.ProfileUrl));
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "Mode",
                    $"Scraper execution mode: {request.ExecutionMode}.",
                    currentStageText,
                    url: request.ProfileUrl));

            (context, page) = await OpenPageAsync(playwright, sessionInfo, request.ProfileUrl, headless, diagnosticsSink, currentStageText).ConfigureAwait(false);
            int visiblePostCount = await GetVisiblePostCountAsync(page).ConfigureAwait(false);
            latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, currentStageText, visiblePostCount).ConfigureAwait(false);
            progress.Report(
                CreateProgressSnapshot(
                    currentStageText,
                    request.MaxPostsToScrape,
                    scrapedPosts.Count,
                    visiblePostCount,
                    page.Url,
                    await page.TitleAsync().ConfigureAwait(false),
                    latestScreenshotPath,
                    ScraperRunState.Starting));

            await WaitForTimelineContentAsync(page, diagnosticsSink, "Waiting for timeline").ConfigureAwait(false);
            ScraperSessionPageState initialSessionState = await _scraperSessionStateInspector.InspectAsync(page).ConfigureAwait(false);
            if (initialSessionState.RequiresAuthentication)
            {
                if (request.ExecutionMode == ScraperExecutionMode.Normal)
                {
                    throw new InvalidOperationException(initialSessionState.Reason);
                }

                frictionMonitor.RecordAuthenticationRequired(initialSessionState.Reason);
                if (await TryStopForFrictionAsync(
                        request,
                        progress,
                        diagnosticsSink,
                        runControl,
                        frictionMonitor,
                        page,
                        latestScreenshotPath,
                        "Checking session state",
                        scrapedPosts.Count,
                        visiblePostCount).ConfigureAwait(false))
                {
                    return scrapedPosts;
                }
            }

            while (!reachedArchiveBoundary && scrapedPosts.Count < request.MaxPostsToScrape && noNewPostCycles < executionPolicy.MaximumNoNewPostCycles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (runControl.IsStopAndSaveRequested)
                {
                    diagnosticsSink.ReportEvent(
                        CreateEvent(
                            "Archive",
                            $"Stop-and-save requested. Returning {scrapedPosts.Count} collected posts.",
                            "Stopping and saving",
                            ScraperDiagnosticsSeverity.Warning,
                            url: page.Url));
                    break;
                }

                await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);

                string routeBeforeRecovery = page.Url;
                if (await _scraperRouteGuard
                        .EnsureExpectedRouteAsync(page, request.ProfileUrl, "Returning to profile timeline", diagnosticsSink, cancellationToken)
                        .ConfigureAwait(false))
                {
                    frictionMonitor.RecordRouteRecovery(routeBeforeRecovery);
                    currentStageText = "Returning to profile timeline";
                    await WaitForDelayAsync(page, executionPolicy.RouteRecoveryCooldownMinimumMilliseconds, executionPolicy.RouteRecoveryCooldownMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
                    latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, currentStageText, await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
                    if (await TryStopForFrictionAsync(
                            request,
                            progress,
                            diagnosticsSink,
                            runControl,
                            frictionMonitor,
                            page,
                            latestScreenshotPath,
                            currentStageText,
                            scrapedPosts.Count,
                            await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                        return scrapedPosts;
                    }
                }

                if (headless && runControl.ConsumeVisibleBrowserRequest())
                {
                    diagnosticsSink.ReportEvent(
                        CreateEvent(
                            "Browser",
                            "Switching the active scrape into visible-browser mode.",
                            "Opening live browser",
                            url: page.Url));

                    (context, page) = await ReopenPageAsync(playwright, sessionInfo, context, page.Url, diagnosticsSink).ConfigureAwait(false);
                    headless = false;
                    currentStageText = "Live browser opened";
                    latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, currentStageText, await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
                }

                ScraperSessionPageState sessionState = await _scraperSessionStateInspector.InspectAsync(page).ConfigureAwait(false);
                if (sessionState.RequiresAuthentication)
                {
                    if (request.ExecutionMode == ScraperExecutionMode.Normal)
                    {
                        diagnosticsSink.ReportEvent(
                            CreateEvent(
                                "Authentication",
                                sessionState.Reason,
                                "Checking session state",
                                ScraperDiagnosticsSeverity.Error,
                                url: page.Url));
                        throw new InvalidOperationException(sessionState.Reason);
                    }

                    frictionMonitor.RecordAuthenticationRequired(sessionState.Reason);
                    diagnosticsSink.ReportEvent(
                        CreateEvent(
                            "Authentication",
                            sessionState.Reason,
                            "Checking session state",
                            ScraperDiagnosticsSeverity.Warning,
                            url: page.Url));
                    if (await TryStopForFrictionAsync(
                            request,
                            progress,
                            diagnosticsSink,
                            runControl,
                            frictionMonitor,
                            page,
                            latestScreenshotPath,
                            "Checking session state",
                            scrapedPosts.Count,
                            visiblePostCount).ConfigureAwait(false))
                    {
                        return scrapedPosts;
                    }

                    await WaitForDelayAsync(page, executionPolicy.RouteRecoveryCooldownMinimumMilliseconds, executionPolicy.RouteRecoveryCooldownMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                ScraperGateResult gateResult = await _scraperGateHandler
                    .HandleAsync(page, "Checking for gates", diagnosticsSink, cancellationToken)
                    .ConfigureAwait(false);

                if (gateResult.Disposition == ScraperGateDisposition.Dismissed)
                {
                    string gateRouteBeforeRecovery = page.Url;
                    if (await _scraperRouteGuard
                        .EnsureExpectedRouteAsync(page, request.ProfileUrl, "Returning to profile timeline", diagnosticsSink, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        frictionMonitor.RecordRouteRecovery(gateRouteBeforeRecovery);
                        await WaitForDelayAsync(page, executionPolicy.RouteRecoveryCooldownMinimumMilliseconds, executionPolicy.RouteRecoveryCooldownMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
                        latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Returning to profile timeline", await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    await WaitForDelayAsync(page, executionPolicy.GateCooldownMinimumMilliseconds, executionPolicy.GateCooldownMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
                }
                else if (gateResult.Disposition == ScraperGateDisposition.Blocked)
                {
                    frictionMonitor.RecordBlockedGate(gateResult.Message);
                    if (headless)
                    {
                        diagnosticsSink.ReportEvent(
                            CreateEvent(
                                "Gate",
                                "Blocked in headless mode; reopening the scrape in a visible browser for intervention.",
                                "Waiting for intervention",
                                ScraperDiagnosticsSeverity.Warning,
                                url: page.Url));

                        (context, page) = await ReopenPageAsync(playwright, sessionInfo, context, page.Url, diagnosticsSink).ConfigureAwait(false);
                        headless = false;
                        latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Waiting for intervention", await GetVisiblePostCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
                    }

                    visiblePostCount = await GetVisiblePostCountAsync(page).ConfigureAwait(false);
                    latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Waiting for intervention", visiblePostCount, force: true).ConfigureAwait(false);
                    await CaptureHtmlSnapshotAsync(page, diagnosticsSink, "gate-blocked", "Waiting for intervention").ConfigureAwait(false);

                    progress.Report(
                        CreateProgressSnapshot(
                            "Waiting for intervention",
                            request.MaxPostsToScrape,
                            scrapedPosts.Count,
                            visiblePostCount,
                            page.Url,
                            await page.TitleAsync().ConfigureAwait(false),
                            latestScreenshotPath,
                            ScraperRunState.WaitingForIntervention,
                            gateResult.Message));

                    if (await TryStopForFrictionAsync(
                            request,
                            progress,
                            diagnosticsSink,
                            runControl,
                            frictionMonitor,
                            page,
                            latestScreenshotPath,
                            "Waiting for intervention",
                            scrapedPosts.Count,
                            visiblePostCount).ConfigureAwait(false))
                    {
                        return scrapedPosts;
                    }

                    pauseGate.Pause();
                    await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                SensitiveMediaRevealBatchResult revealResultWithPolicy = await _sensitiveMediaRevealCoordinator
                    .RevealAsync(page, request.ProfileUrl, request.Username, executionPolicy, frictionMonitor, sensitiveMediaRevealAttemptTracker, diagnosticsSink, screenshotCoordinator, cancellationToken)
                    .ConfigureAwait(false);

                if (revealResultWithPolicy.RevealedCount > 0 || revealResultWithPolicy.FailedArchiveTextOnlyCount > 0)
                {
                    await WaitForDelayAsync(page, executionPolicy.SensitiveRevealCooldownMinimumMilliseconds, executionPolicy.SensitiveRevealCooldownMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
                }

                if (await TryStopForFrictionAsync(
                        request,
                        progress,
                        diagnosticsSink,
                        runControl,
                        frictionMonitor,
                        page,
                        latestScreenshotPath,
                        "Revealing sensitive media",
                        scrapedPosts.Count,
                        visiblePostCount).ConfigureAwait(false))
                {
                    return scrapedPosts;
                }

                currentStageText = "Extracting visible posts";
                IReadOnlyList<ScrapedPostRecord> visiblePosts = ApplySensitiveMediaOutcomes(
                    await GetVisiblePostsAsync(page, request.Username).ConfigureAwait(false),
                    sensitiveMediaRevealAttemptTracker);
                visiblePostCount = visiblePosts.Count;

                diagnosticsSink.ReportEvent(
                    CreateEvent(
                        "Extraction",
                        $"Parsed {visiblePosts.Count} visible post candidates from the current page.",
                        currentStageText,
                        url: page.Url));

                int newPostsThisCycle = 0;
                foreach (ScrapedPostRecord scrapedPost in visiblePosts)
                {
                    if (runControl.IsStopAndSaveRequested)
                    {
                        diagnosticsSink.ReportEvent(
                            CreateEvent(
                                "Archive",
                                $"Stop-and-save requested. Returning {scrapedPosts.Count} collected posts.",
                                "Stopping and saving",
                                ScraperDiagnosticsSeverity.Warning,
                                url: page.Url));
                        break;
                    }

                    if (!seenPostIds.Add(scrapedPost.PostId))
                    {
                        continue;
                    }

                    ScrapedPostRecord resolvedPost = await _scrapedVideoResolver
                        .ResolveAsync(page, request.ProfileUrl, scrapedPost, executionPolicy, frictionMonitor, diagnosticsSink, screenshotCoordinator, cancellationToken)
                        .ConfigureAwait(false);

                    if (request.ArchiveStartUtc.HasValue && resolvedPost.CreatedAtUtc < request.ArchiveStartUtc.Value)
                    {
                        reachedArchiveBoundary = true;
                        diagnosticsSink.ReportEvent(
                            CreateEvent(
                                "Archive",
                                $"Reached the start of the selected archive range at post {resolvedPost.PostId}.",
                                "Reached archive range start",
                                url: resolvedPost.SourceUrl));
                        break;
                    }

                    if (request.ArchiveEndUtc.HasValue && resolvedPost.CreatedAtUtc >= request.ArchiveEndUtc.Value)
                    {
                        diagnosticsSink.ReportEvent(
                            CreateEvent(
                                "Archive",
                                $"Skipped post {resolvedPost.PostId} because it is newer than the selected archive stop time.",
                                "Applying archive range",
                                url: resolvedPost.SourceUrl));
                        continue;
                    }

                    scrapedPosts.Add(resolvedPost);
                    diagnosticsSink.ReportDiscoveredPost(resolvedPost);
                    diagnosticsSink.ReportEvent(
                        CreateEvent(
                            "Extraction",
                            $"Discovered post {resolvedPost.PostId}.",
                            currentStageText,
                            url: resolvedPost.SourceUrl));

                    newPostsThisCycle++;
                    if (scrapedPosts.Count >= request.MaxPostsToScrape)
                    {
                        break;
                    }

                    if (await TryStopForFrictionAsync(
                            request,
                            progress,
                            diagnosticsSink,
                            runControl,
                            frictionMonitor,
                            page,
                            latestScreenshotPath,
                            "Resolving scraped video",
                            scrapedPosts.Count,
                            visiblePostCount).ConfigureAwait(false))
                    {
                        return scrapedPosts;
                    }
                }

                latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, currentStageText, visiblePostCount).ConfigureAwait(false);
                progress.Report(
                    CreateProgressSnapshot(
                        currentStageText,
                        request.MaxPostsToScrape,
                        scrapedPosts.Count,
                        visiblePostCount,
                        page.Url,
                        await page.TitleAsync().ConfigureAwait(false),
                        latestScreenshotPath,
                        ScraperRunState.Running));

                if (scrapedPosts.Count >= request.MaxPostsToScrape)
                {
                    break;
                }

                if (reachedArchiveBoundary)
                {
                    break;
                }

                if (runControl.IsStopAndSaveRequested)
                {
                    break;
                }

                noNewPostCycles = newPostsThisCycle == 0 ? noNewPostCycles + 1 : 0;
                currentStageText = "Scrolling";
                diagnosticsSink.ReportEvent(
                    CreateEvent(
                        "Scroll",
                        $"Scrolling for more posts. Cycle without new posts: {noNewPostCycles}.",
                        currentStageText,
                        url: page.Url));

                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight);").ConfigureAwait(false);
                await WaitForDelayAsync(page, executionPolicy.ScrollDelayMinimumMilliseconds, executionPolicy.ScrollDelayMaximumMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            if (scrapedPosts.Count == 0)
            {
                await CaptureHtmlSnapshotAsync(page, diagnosticsSink, "no-posts", "No posts found").ConfigureAwait(false);
            }

            return scrapedPosts
                .OrderByDescending(post => post.CreatedAtUtc)
                .Take(request.MaxPostsToScrape)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlaywrightException)
        {
            if (page is not null)
            {
                try
                {
                    int visiblePostCount = await GetVisiblePostCountAsync(page).ConfigureAwait(false);
                    latestScreenshotPath = await screenshotCoordinator.CaptureAsync(page, diagnosticsSink, "Failure", visiblePostCount, force: true).ConfigureAwait(false);
                    await CaptureHtmlSnapshotAsync(page, diagnosticsSink, "failure", "Failure").ConfigureAwait(false);
                    progress.Report(
                        CreateProgressSnapshot(
                            "Failure",
                            request.MaxPostsToScrape,
                            scrapedPosts.Count,
                            visiblePostCount,
                            page.Url,
                            await page.TitleAsync().ConfigureAwait(false),
                            latestScreenshotPath,
                            ScraperRunState.Failed,
                            exception.Message));
                }
                catch
                {
                    // Preserve the original scraper failure.
                }
            }

            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "Failure",
                    exception.Message,
                    "Failure",
                    ScraperDiagnosticsSeverity.Error,
                    url: page?.Url ?? request.ProfileUrl));
            throw;
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryStopForFrictionAsync(
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperRunControl runControl,
        IScraperFrictionMonitor frictionMonitor,
        IPage page,
        string latestScreenshotPath,
        string stageText,
        int collectedPostCount,
        int visiblePostCount)
    {
        if (!frictionMonitor.HasStopCondition)
        {
            return false;
        }

        runControl.RequestConservativeStop(frictionMonitor.StopReason);
        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Conservative",
                frictionMonitor.StopReason,
                stageText,
                ScraperDiagnosticsSeverity.Warning,
                url: page.Url));
        progress.Report(
            CreateProgressSnapshot(
                "Conservative mode stopped early",
                request.MaxPostsToScrape,
                collectedPostCount,
                visiblePostCount,
                page.Url,
                await page.TitleAsync().ConfigureAwait(false),
                latestScreenshotPath,
                ScraperRunState.Stopping,
                frictionMonitor.StopReason));
        return true;
    }

    private static async Task WaitForDelayAsync(
        IPage page,
        int minimumMilliseconds,
        int maximumMilliseconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumMilliseconds <= 0)
        {
            return;
        }

        int delay = minimumMilliseconds >= maximumMilliseconds
            ? minimumMilliseconds
            : Random.Shared.Next(minimumMilliseconds, maximumMilliseconds + 1);
        if (delay > 0)
        {
            await page.WaitForTimeoutAsync(delay).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<ScrapedPostRecord>> GetVisiblePostsAsync(IPage page, string fallbackUsername)
    {
        string[] articleHtml = await page.EvaluateAsync<string[]>(
            """
            () => {
              const articleNodes = Array.from(document.querySelectorAll("article[data-testid='tweet'], article[role='article']"));
              if (articleNodes.length > 0) {
                return articleNodes.map(article => article.outerHTML);
              }

              const statusLinks = Array.from(document.querySelectorAll("[data-testid='primaryColumn'] a[href*='/status/']"));
              const containerHtml = statusLinks
                .map(link => link.closest("article, div[data-testid='cellInnerDiv'], section, div[role='article']"))
                .filter(container => container)
                .map(container => container.outerHTML);
              return [...new Set(containerHtml)];
            }
            """)
            .ConfigureAwait(false);

        return articleHtml
            .Select(html => _scrapedPostHtmlParser.Parse(html, fallbackUsername))
            .Where(post => post is not null)
            .Cast<ScrapedPostRecord>()
            .ToList();
    }

    private static async Task<int> GetVisiblePostCountAsync(IPage page)
    {
        int articleCount = await page.Locator(TweetArticleSelector).CountAsync().ConfigureAwait(false);
        if (articleCount > 0)
        {
            return articleCount;
        }

        return await page.Locator(StatusLinkSelector).CountAsync().ConfigureAwait(false);
    }

    private async Task<IBrowserContext> LaunchPersistentContextAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        bool headless)
    {
        BrowserTypeLaunchPersistentContextOptions options = new()
        {
            Args =
            [
                "--disable-blink-features=AutomationControlled",
            ],
            ExecutablePath = sessionInfo.BrowserExecutablePath,
            Headless = headless,
            ViewportSize = new ViewportSize
            {
                Height = 960,
                Width = 1400,
            },
        };

        try
        {
            return await playwright.Chromium
                .LaunchPersistentContextAsync(sessionInfo.UserDataDirectory, options)
                .ConfigureAwait(false);
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("user data directory is already in use", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Close the dedicated X login browser before starting or resuming the scraper.", exception);
        }
    }

    private async Task<(IBrowserContext Context, IPage Page)> OpenPageAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        string url,
        bool headless,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText)
    {
        return await OpenPageWithRecoveryAsync(playwright, sessionInfo, url, headless, diagnosticsSink, stageText).ConfigureAwait(false);
    }

    private async Task<(IBrowserContext Context, IPage Page)> ReopenPageAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        IBrowserContext? existingContext,
        string url,
        IScraperDiagnosticsSink diagnosticsSink)
    {
        if (existingContext is not null)
        {
            await existingContext.DisposeAsync().ConfigureAwait(false);
        }

        return await OpenPageAsync(playwright, sessionInfo, url, headless: false, diagnosticsSink, "Opening live browser").ConfigureAwait(false);
    }

    private static bool IsClosedTargetFailure(Exception exception)
    {
        return exception is PlaywrightException playwrightException &&
               (playwrightException.Message.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase) ||
                playwrightException.Message.Contains("Browser closed", StringComparison.OrdinalIgnoreCase) ||
                playwrightException.Message.Contains("Process failed", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(IBrowserContext Context, IPage Page)> OpenPageCoreAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        string url,
        bool headless,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText)
    {
        IBrowserContext context = await LaunchPersistentContextAsync(playwright, sessionInfo, headless).ConfigureAwait(false);
        IPage page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);

        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Navigation",
                $"Navigating to {url}.",
                stageText,
                url: url));

        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
        }).ConfigureAwait(false);

        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Navigation",
                $"Navigation completed: {page.Url}",
                stageText,
                url: page.Url));

        return (context, page);
    }

    private async Task<(IBrowserContext Context, IPage Page)> OpenPageWithRecoveryAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        string url,
        bool headless,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText)
    {
        try
        {
            return await OpenPageCoreAsync(playwright, sessionInfo, url, headless, diagnosticsSink, stageText).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsClosedTargetFailure(exception))
        {
            bool hasLiveBrowser = await CanConnectToDebugEndpointAsync(sessionInfo.RemoteDebuggingPort, CancellationToken.None).ConfigureAwait(false);
            if (hasLiveBrowser)
            {
                throw new InvalidOperationException("The dedicated X browser session is already open. Close that browser, then try scraping again.", exception);
            }

            bool cleanedLocks = _scraperSessionLockCleaner.Clean(sessionInfo.UserDataDirectory);
            if (!cleanedLocks)
            {
                throw new InvalidOperationException("The dedicated scraper browser session closed unexpectedly. Reset the scraper browser session and sign in again.", exception);
            }

            diagnosticsSink.ReportEvent(
                CreateEvent(
                    "Browser",
                    "Cleaned stale scraper session lock files and retrying browser launch.",
                    stageText,
                    ScraperDiagnosticsSeverity.Warning,
                    url: url));

            try
            {
                return await OpenPageCoreAsync(playwright, sessionInfo, url, headless, diagnosticsSink, stageText).ConfigureAwait(false);
            }
            catch (Exception retryException) when (IsClosedTargetFailure(retryException))
            {
                throw new InvalidOperationException("The dedicated scraper browser session still closed unexpectedly after session recovery. Reset the scraper browser session and sign in again.", retryException);
            }
        }
    }

    private async Task WaitForTimelineContentAsync(IPage page, IScraperDiagnosticsSink diagnosticsSink, string stageText)
    {
        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Wait",
                "Waiting for timeline content.",
                stageText,
                url: page.Url));

        for (int attempt = 0; attempt < 10; attempt++)
        {
            bool hasArticles = await page.Locator(TweetArticleSelector).CountAsync().ConfigureAwait(false) > 0;
            bool hasStatusLinks = await page.Locator(StatusLinkSelector).CountAsync().ConfigureAwait(false) > 0;
            if (hasArticles || hasStatusLinks)
            {
                diagnosticsSink.ReportEvent(
                    CreateEvent(
                        "Wait",
                        $"Timeline content detected on attempt {attempt + 1}.",
                        stageText,
                        url: page.Url));
                return;
            }

            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
        }

        diagnosticsSink.ReportEvent(
            CreateEvent(
                "Wait",
                "Timed out waiting for timeline content.",
                stageText,
                ScraperDiagnosticsSeverity.Warning,
                url: page.Url));
    }

    private static async Task<bool> CanConnectToDebugEndpointAsync(int remoteDebuggingPort, CancellationToken cancellationToken)
    {
        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(2),
        };

        try
        {
            using HttpResponseMessage response = await client
                .GetAsync($"http://127.0.0.1:{remoteDebuggingPort}/json/version", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private async Task<bool> ValidatePageSessionAsync(IPage page, string destination, CancellationToken cancellationToken)
    {
        await page.GotoAsync(destination, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
        }).ConfigureAwait(false);

        DateTimeOffset timeoutAt = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScraperSessionPageState sessionState = await _scraperSessionStateInspector.InspectAsync(page).ConfigureAwait(false);
            if (sessionState.RequiresAuthentication)
            {
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                continue;
            }

            if (sessionState.HasTimelineContent || sessionState.HasSensitiveProfileInterstitial)
            {
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                return true;
            }

            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
        }

        return false;
    }

    private async Task<bool> TryValidatePersistentSessionAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        string destination,
        CancellationToken cancellationToken)
    {
        IBrowserContext? context = null;
        try
        {
            context = await LaunchPersistentContextAsync(playwright, sessionInfo, headless: true).ConfigureAwait(false);
            IPage page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);
            return await ValidatePageSessionAsync(page, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return false;
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryValidateRunningBrowserSessionAsync(
        IPlaywright playwright,
        ScraperBrowserSessionInfo sessionInfo,
        string destination,
        CancellationToken cancellationToken)
    {
        if (!await CanConnectToDebugEndpointAsync(sessionInfo.RemoteDebuggingPort, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            await using IBrowser browser = await playwright.Chromium
                .ConnectOverCDPAsync($"http://127.0.0.1:{sessionInfo.RemoteDebuggingPort}")
                .ConfigureAwait(false);

            IBrowserContext? context = browser.Contexts.FirstOrDefault();
            if (context is null)
            {
                return false;
            }

            IPage page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().ConfigureAwait(false);
            return await ValidatePageSessionAsync(page, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    private static IReadOnlyList<ScrapedPostRecord> ApplySensitiveMediaOutcomes(
        IReadOnlyList<ScrapedPostRecord> visiblePosts,
        ISensitiveMediaRevealAttemptTracker attemptTracker)
    {
        return visiblePosts
            .Select(
                post =>
                {
                    if (!attemptTracker.TryGetOutcome(post.PostId, out SensitiveMediaPostOutcome outcome))
                    {
                        return post;
                    }

                    return MergeWithSensitiveMediaOutcome(post, outcome);
                })
            .ToList();
    }

    private static List<ScrapedMediaRecord> MergeMedia(
        IReadOnlyList<ScrapedMediaRecord> existingMedia,
        IReadOnlyList<ScrapedMediaRecord> incomingMedia)
    {
        Dictionary<string, ScrapedMediaRecord> mediaByKey = new(StringComparer.Ordinal);
        foreach (ScrapedMediaRecord media in existingMedia.Concat(incomingMedia))
        {
            string mediaKey = !string.IsNullOrWhiteSpace(media.MediaKey)
                ? media.MediaKey
                : media.SourceUrl;
            if (string.IsNullOrWhiteSpace(mediaKey))
            {
                continue;
            }

            mediaByKey[mediaKey] = media;
        }

        return mediaByKey.Values.ToList();
    }

    private static ScrapedPostRecord MergeWithSensitiveMediaOutcome(
        ScrapedPostRecord originalPost,
        SensitiveMediaPostOutcome outcome)
    {
        ScrapedPostRecord snapshot = outcome.PostSnapshot ?? new ScrapedPostRecord();
        return new ScrapedPostRecord
        {
            ContainsSensitiveMediaWarning = true,
            CreatedAtUtc = originalPost.CreatedAtUtc,
            Media = MergeMedia(originalPost.Media, snapshot.Media),
            PostId = originalPost.PostId,
            RawHtml = string.IsNullOrWhiteSpace(snapshot.RawHtml) ? originalPost.RawHtml : snapshot.RawHtml,
            SensitiveMediaFailureReason = outcome.Kind == SensitiveMediaPostOutcomeKind.Revealed
                ? string.Empty
                : outcome.Reason,
            SensitiveMediaRevealSucceeded = outcome.Kind == SensitiveMediaPostOutcomeKind.Revealed,
            SourceUrl = string.IsNullOrWhiteSpace(originalPost.SourceUrl) ? snapshot.SourceUrl : originalPost.SourceUrl,
            Text = string.IsNullOrWhiteSpace(originalPost.Text) ? snapshot.Text : originalPost.Text,
            Username = string.IsNullOrWhiteSpace(originalPost.Username) ? snapshot.Username : originalPost.Username,
        };
    }
}
