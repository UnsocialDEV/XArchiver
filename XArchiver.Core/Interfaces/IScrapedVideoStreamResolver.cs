using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IScrapedVideoStreamResolver
{
    Task<ScrapedVideoStreamResolution> ResolveAsync(IReadOnlyList<string> candidateUrls, CancellationToken cancellationToken);
}
