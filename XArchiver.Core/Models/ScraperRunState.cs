namespace XArchiver.Core.Models;

public enum ScraperRunState
{
    Idle,
    Starting,
    Running,
    Paused,
    WaitingForIntervention,
    Stopping,
    Stopped,
    Completed,
    Failed,
}
