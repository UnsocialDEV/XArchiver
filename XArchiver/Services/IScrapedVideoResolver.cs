using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal interface IScrapedVideoResolver
{
    Task<ScrapedPostRecord> ResolveAsync(
        IPage page,
        string profileUrl,
        ScrapedPostRecord post,
        ScraperExecutionPolicy executionPolicy,
        IScraperFrictionMonitor frictionMonitor,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPageScreenshotCoordinator screenshotCoordinator,
        CancellationToken cancellationToken);
}
