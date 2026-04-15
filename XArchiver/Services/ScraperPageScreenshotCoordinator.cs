using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScraperPageScreenshotCoordinator : IScraperPageScreenshotCoordinator
{
    private readonly Dictionary<IPage, PageCaptureState> _captureStateByPage = [];

    public async Task<string> CaptureAsync(
        IPage page,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText,
        int visiblePostCount,
        bool force = false)
    {
        string currentUrl = NormalizeUrl(page.Url);
        if (!force &&
            _captureStateByPage.TryGetValue(page, out PageCaptureState? existingState) &&
            string.Equals(existingState.PageUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
        {
            return existingState.ScreenshotPath;
        }

        string screenshotPath = Path.Combine(
            diagnosticsSink.DiagnosticsDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmssfff}_{SanitizeFileSegment(stageText)}.png");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = false,
            Path = screenshotPath,
        }).ConfigureAwait(false);

        string pageTitle = await page.TitleAsync().ConfigureAwait(false);
        diagnosticsSink.ReportLiveSnapshot(
            new ScraperLiveSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                PageTitle = pageTitle,
                PageUrl = page.Url,
                ScreenshotPath = screenshotPath,
                StageText = stageText,
                VisiblePostCount = visiblePostCount,
            });

        diagnosticsSink.ReportEvent(
            new ScraperDiagnosticsEvent
            {
                ArtifactPath = screenshotPath,
                Category = "Artifact",
                Message = $"Captured screenshot: {screenshotPath}",
                Severity = ScraperDiagnosticsSeverity.Information,
                StageText = stageText,
                TimestampUtc = DateTimeOffset.UtcNow,
                Url = page.Url,
            });

        _captureStateByPage[page] = new PageCaptureState(currentUrl, screenshotPath);
        return screenshotPath;
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim();
    }

    private static string SanitizeFileSegment(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalidCharacters.Contains(character) ? '_' : character));
    }

    private sealed record PageCaptureState(string PageUrl, string ScreenshotPath);
}
