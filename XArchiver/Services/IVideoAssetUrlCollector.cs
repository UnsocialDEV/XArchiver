using Microsoft.Playwright;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal interface IVideoAssetUrlCollector
{
    Task<IReadOnlyList<string>> CollectAsync(
        IPage page,
        string postId,
        IScraperDiagnosticsSink diagnosticsSink,
        CancellationToken cancellationToken);
}
