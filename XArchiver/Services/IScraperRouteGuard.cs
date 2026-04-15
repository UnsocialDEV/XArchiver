using Microsoft.Playwright;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal interface IScraperRouteGuard
{
    Task<bool> EnsureExpectedRouteAsync(
        IPage page,
        string targetUrl,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken);
}
