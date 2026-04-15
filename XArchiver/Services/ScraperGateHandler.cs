using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScraperGateHandler : IScraperGateHandler
{
    private static readonly string[] CandidateLabels =
    [
        "Yes, view profile",
        "View profile",
        "Continue",
        "Show more",
        "Show",
    ];

    private static readonly string[] ModalSelectors =
    [
        "[role='dialog']",
        "[data-testid='sheetDialog']",
        "[aria-modal='true']",
        "[data-testid='confirmationSheetDialog']",
    ];

    public async Task<ScraperGateResult> HandleAsync(
        IPage page,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await HasBlockingSurfaceAsync(page).ConfigureAwait(false))
        {
            return new ScraperGateResult
            {
                Disposition = ScraperGateDisposition.NotPresent,
            };
        }

        diagnosticsSink.ReportEvent(
            CreateEvent(
                stageText,
                "Gate",
                "Detected a blocking surface over the profile page.",
                ScraperDiagnosticsSeverity.Warning,
                url: page.Url));

        foreach (string candidateLabel in CandidateLabels)
        {
            ScraperGateResult? clickedResult = await TryClickCandidateAsync(page, candidateLabel, stageText, diagnosticsSink).ConfigureAwait(false);
            if (clickedResult is not null)
            {
                return clickedResult;
            }
        }

        return new ScraperGateResult
        {
            Disposition = ScraperGateDisposition.Blocked,
            Message = "A page gate is blocking the timeline and no known continue control was matched.",
        };
    }

    private static ScraperDiagnosticsEvent CreateEvent(
        string stageText,
        string category,
        string message,
        ScraperDiagnosticsSeverity severity,
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

    private static async Task<bool> HasBlockingSurfaceAsync(IPage page)
    {
        foreach (string selector in ModalSelectors)
        {
            if (await page.Locator(selector).CountAsync().ConfigureAwait(false) > 0)
            {
                return true;
            }
        }

        bool hasTweets = await page.Locator("article[data-testid='tweet'], article[role='article']").CountAsync().ConfigureAwait(false) > 0;
        bool hasStatusLinks = await page.Locator("[data-testid='primaryColumn'] a[href*='/status/']").CountAsync().ConfigureAwait(false) > 0;
        bool hasPrimaryColumn = await page.Locator("[data-testid='primaryColumn']").CountAsync().ConfigureAwait(false) > 0;
        return hasPrimaryColumn && !hasTweets && !hasStatusLinks;
    }

    private static async Task<ScraperGateResult?> TryClickCandidateAsync(
        IPage page,
        string candidateLabel,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink)
    {
        ILocator buttonLocator = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = candidateLabel });
        if (await buttonLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            return await ClickLocatorAsync(buttonLocator.First, candidateLabel, stageText, diagnosticsSink, page.Url).ConfigureAwait(false);
        }

        ILocator linkLocator = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = candidateLabel });
        if (await linkLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            return await ClickLocatorAsync(linkLocator.First, candidateLabel, stageText, diagnosticsSink, page.Url).ConfigureAwait(false);
        }

        ILocator textLocator = page.GetByText(candidateLabel, new PageGetByTextOptions
        {
            Exact = true,
        });
        if (await textLocator.CountAsync().ConfigureAwait(false) > 0)
        {
            return await ClickLocatorAsync(textLocator.First, candidateLabel, stageText, diagnosticsSink, page.Url).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<ScraperGateResult> ClickLocatorAsync(
        ILocator locator,
        string candidateLabel,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink,
        string url)
    {
        diagnosticsSink.ReportEvent(
            CreateEvent(
                stageText,
                "Gate",
                $"Attempting gate click: {candidateLabel}.",
                ScraperDiagnosticsSeverity.Information,
                selector: candidateLabel,
                url: url));

        try
        {
            await locator.ClickAsync().ConfigureAwait(false);
            await locator.Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    stageText,
                    "Gate",
                    $"Clicked gate control: {candidateLabel}.",
                    ScraperDiagnosticsSeverity.Information,
                    selector: candidateLabel,
                    url: locator.Page.Url));

            return new ScraperGateResult
            {
                Disposition = ScraperGateDisposition.Dismissed,
                Message = $"Clicked gate control: {candidateLabel}.",
                Selector = candidateLabel,
            };
        }
        catch (PlaywrightException exception)
        {
            diagnosticsSink.ReportEvent(
                CreateEvent(
                    stageText,
                    "Gate",
                    $"Gate click failed for {candidateLabel}: {exception.Message}",
                    ScraperDiagnosticsSeverity.Warning,
                    selector: candidateLabel,
                    url: locator.Page.Url));

            return new ScraperGateResult
            {
                Disposition = ScraperGateDisposition.Blocked,
                Message = $"Gate control '{candidateLabel}' could not be clicked: {exception.Message}",
                Selector = candidateLabel,
            };
        }
    }
}
