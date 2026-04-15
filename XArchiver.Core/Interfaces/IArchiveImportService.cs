using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveImportService
{
    Task<ArchiveImportResult> ImportAsync(string parentFolderPath, CancellationToken cancellationToken);
}
