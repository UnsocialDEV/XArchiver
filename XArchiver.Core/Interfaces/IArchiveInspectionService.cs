using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveInspectionService
{
    Task<DiscoveredArchiveRecord?> InspectAsync(string archiveFolderPath, CancellationToken cancellationToken);
}
