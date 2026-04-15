using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IManualArchiveService
{
    Task<ManualArchiveResult> ArchiveSelectedAsync(
        ArchiveProfile profile,
        IReadOnlyList<PreviewPostRecord> posts,
        CancellationToken cancellationToken);
}
