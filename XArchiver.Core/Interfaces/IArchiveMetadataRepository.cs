using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveMetadataRepository
{
    Task<ArchivedPostMetadataReadResult> LoadAsync(string? metadataFilePath, CancellationToken cancellationToken);
}
