using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveFileWriter
{
    Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken);
}
