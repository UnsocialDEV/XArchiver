using Microsoft.Playwright;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal interface ISensitiveMediaRevealCoordinator
{
    Task<SensitiveMediaRevealBatchResult> RevealAsync(
        IPage page,
        string profileUrl,
        string fallbackUsername,
        ScraperExecutionPolicy executionPolicy,
        IScraperFrictionMonitor frictionMonitor,
        ISensitiveMediaRevealAttemptTracker attemptTracker,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPageScreenshotCoordinator screenshotCoordinator,
        CancellationToken cancellationToken);
}
