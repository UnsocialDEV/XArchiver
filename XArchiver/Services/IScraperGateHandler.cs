using Microsoft.Playwright;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal interface IScraperGateHandler
{
    Task<ScraperGateResult> HandleAsync(
        IPage page,
        string stageText,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken);
}
