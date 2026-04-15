using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveProfileRepository
{
    Task<IReadOnlyList<ArchiveProfile>> GetAllAsync(CancellationToken cancellationToken);

    Task SaveAsync(ArchiveProfile profile, CancellationToken cancellationToken);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken);
}
