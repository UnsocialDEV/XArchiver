namespace XArchiver.Core.Models;

public sealed record SyncControlAvailability
{
    public bool CanPause { get; init; }

    public bool CanStart { get; init; }

    public bool CanStop { get; init; }

    public static SyncControlAvailability FromState(SyncSessionState state)
    {
        return state switch
        {
            SyncSessionState.Queued => new SyncControlAvailability { CanStart = true, CanStop = true },
            SyncSessionState.Starting => new SyncControlAvailability { CanStop = true },
            SyncSessionState.Running => new SyncControlAvailability { CanPause = true, CanStop = true },
            SyncSessionState.Pausing => new SyncControlAvailability { CanStop = true },
            SyncSessionState.Paused => new SyncControlAvailability { CanStart = true, CanStop = true },
            SyncSessionState.Stopping => new SyncControlAvailability(),
            SyncSessionState.Stopped => new SyncControlAvailability { CanStart = true },
            SyncSessionState.Completed => new SyncControlAvailability { CanStart = true },
            SyncSessionState.Failed => new SyncControlAvailability { CanStart = true },
            _ => new SyncControlAvailability(),
        };
    }
}
