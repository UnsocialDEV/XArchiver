namespace XArchiver.Core.Models;

public enum SyncSessionState
{
    Queued = 1,
    Starting = 2,
    Running = 3,
    Pausing = 4,
    Paused = 5,
    Stopping = 6,
    Stopped = 7,
    Completed = 8,
    Failed = 9,
}
