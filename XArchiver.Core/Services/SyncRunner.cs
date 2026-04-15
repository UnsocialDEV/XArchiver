using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class SyncRunner : ISyncRunner
{
    private readonly IArchiveProfileRepository _archiveProfileRepository;
    private readonly IArchiveSyncService _archiveSyncService;

    public SyncRunner(IArchiveSyncService archiveSyncService, IArchiveProfileRepository archiveProfileRepository)
    {
        _archiveSyncService = archiveSyncService;
        _archiveProfileRepository = archiveProfileRepository;
    }

    public async Task<SyncResult> RunAsync(
        ApiSyncRequest request,
        IProgress<SyncProgressSnapshot> progress,
        ISyncPauseGate pauseGate,
        CancellationToken cancellationToken)
    {
        SyncResult result = await _archiveSyncService
            .SyncAsync(request, progress, pauseGate, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == SyncStatus.Success && result.UpdatedProfile is not null)
        {
            await _archiveProfileRepository.SaveAsync(result.UpdatedProfile, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
