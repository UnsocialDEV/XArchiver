using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IScheduledArchiveRunRepository
{
    Task DeleteAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledArchiveRunRecord>> GetAllAsync(CancellationToken cancellationToken);

    Task SaveAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken);
}
