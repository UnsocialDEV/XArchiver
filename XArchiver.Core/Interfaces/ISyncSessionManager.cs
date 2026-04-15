using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface ISyncSessionManager
{
    event EventHandler? SessionsChanged;

    IReadOnlyList<SyncSessionRecord> GetSessions();

    bool Pause(Guid sessionId);

    SyncSessionRecord Queue(ApiSyncRequest request);

    Task<SyncSessionRecord?> StartAsync(Guid sessionId, CancellationToken cancellationToken);

    bool StopSession(Guid sessionId);
}
