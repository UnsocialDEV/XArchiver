namespace XArchiver.Core.Models;

public sealed class ScraperPauseStateChangedEventArgs : EventArgs
{
    public bool IsPaused { get; init; }

    public bool IsPauseRequested { get; init; }
}
