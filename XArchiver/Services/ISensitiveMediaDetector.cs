using Microsoft.Playwright;

namespace XArchiver.Services;

internal interface ISensitiveMediaDetector
{
    Task<IReadOnlyList<SensitiveMediaCandidate>> DetectAsync(IPage page, CancellationToken cancellationToken);
}
