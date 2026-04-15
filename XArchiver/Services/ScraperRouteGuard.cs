using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScraperRouteGuard : IScraperRouteGuard
{
    public async Task<bool> EnsureExpectedRouteAsync(
        IPage page,
        string targetUrl,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedTargetUrl = NormalizeRoute(targetUrl);
        string normalizedCurrentUrl = NormalizeRoute(page.Url);
        if (string.Equals(normalizedCurrentUrl, normalizedTargetUrl, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ShouldReturnToTarget(normalizedCurrentUrl, normalizedTargetUrl))
        {
            return false;
        }

        diagnosticsSink.ReportEvent(
            new ScraperDiagnosticsEvent
            {
                Category = "Navigation",
                Message = $"Returning to expected route: {targetUrl}",
                Severity = ScraperDiagnosticsSeverity.Warning,
                StageText = stageText,
                TimestampUtc = DateTimeOffset.UtcNow,
                Url = page.Url,
            });

        await page.GotoAsync(targetUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
        }).ConfigureAwait(false);
        await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
        return true;
    }

    private static string NormalizeRoute(string url)
    {
        return url.Trim().TrimEnd('/');
    }

    private static bool ShouldReturnToTarget(string currentUrl, string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(currentUrl) || string.IsNullOrWhiteSpace(targetUrl))
        {
            return false;
        }

        if (currentUrl.Contains("/photo/", StringComparison.OrdinalIgnoreCase) ||
            currentUrl.Contains("/video/", StringComparison.OrdinalIgnoreCase) ||
            currentUrl.Contains("/media_tags", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (targetUrl.Contains("/status/", StringComparison.OrdinalIgnoreCase))
        {
            return currentUrl.Contains("/status/", StringComparison.OrdinalIgnoreCase);
        }

        return currentUrl.Contains("/status/", StringComparison.OrdinalIgnoreCase);
    }
}
