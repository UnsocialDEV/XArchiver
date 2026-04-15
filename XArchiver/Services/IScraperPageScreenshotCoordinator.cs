using Microsoft.Playwright;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal interface IScraperPageScreenshotCoordinator
{
    Task<string> CaptureAsync(
        IPage page,
        IScraperDiagnosticsSink diagnosticsSink,
        string stageText,
        int visiblePostCount,
        bool force = false);
}
