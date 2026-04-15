namespace XArchiver.Core.Models;

public enum ScheduledArchiveRunState
{
    Pending = 1,
    WaitingForCapacity = 2,
    Dispatched = 3,
    Failed = 4,
}
