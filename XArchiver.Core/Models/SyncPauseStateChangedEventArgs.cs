namespace XArchiver.Core.Models;

public sealed class SyncPauseStateChangedEventArgs : EventArgs
{
    public bool IsPaused { get; init; }

    public bool IsPauseRequested { get; init; }
}
