using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class SyncSessionItemViewModel : ObservableObject
{
    public SyncSessionItemViewModel(SyncSessionRecord session)
    {
        SessionId = session.SessionId;
        Username = $"@{session.Profile.Username}";
        ArchiveRootPath = session.Profile.ArchiveRootPath;
        State = session.State;
        StateText = FormatStateText(session.State);
        LatestStatusText = session.LatestStatusText;
        ProgressValue = GetProgressValue(session.Progress);
        ProgressSummary = $"{session.Progress.ArchivedPostCount} / {session.Progress.TargetPostCount} archived";
        PageScanSummary = $"{session.Progress.ScannedPageCount} timeline pages scanned";
        MediaSummary = $"{session.Progress.DownloadedImageCount} images, {session.Progress.DownloadedVideoCount} videos, {session.Progress.PartialMediaCount} partial";
        LastUpdatedText = $"Last update {session.UpdatedAtUtc.LocalDateTime:g}";

        SyncControlAvailability controls = SyncControlAvailability.FromState(session.State);
        CanStart = controls.CanStart;
        CanPause = controls.CanPause;
        CanStop = controls.CanStop;
        StartButtonText = session.State switch
        {
            SyncSessionState.Paused => "Resume",
            SyncSessionState.Completed or SyncSessionState.Failed or SyncSessionState.Stopped => "Restart",
            _ => "Start",
        };
    }

    public string ArchiveRootPath { get; }

    public bool CanPause { get; }

    public bool CanStart { get; }

    public bool CanStop { get; }

    public string LastUpdatedText { get; }

    public string LatestStatusText { get; }

    public string MediaSummary { get; }

    public string PageScanSummary { get; }

    public double ProgressValue { get; }

    public string ProgressSummary { get; }

    public Guid SessionId { get; }

    public SyncSessionState State { get; }

    public string StartButtonText { get; }

    public string StateText { get; }

    public string Username { get; }

    private static string FormatStateText(SyncSessionState state)
    {
        return state switch
        {
            SyncSessionState.Queued => "Queued",
            SyncSessionState.Starting => "Starting",
            SyncSessionState.Running => "Running",
            SyncSessionState.Pausing => "Pausing",
            SyncSessionState.Paused => "Paused",
            SyncSessionState.Stopping => "Stopping",
            SyncSessionState.Stopped => "Stopped",
            SyncSessionState.Completed => "Completed",
            SyncSessionState.Failed => "Failed",
            _ => state.ToString(),
        };
    }

    private static double GetProgressValue(SyncProgressSnapshot progress)
    {
        if (progress.TargetPostCount <= 0)
        {
            return 0;
        }

        double ratio = (double)progress.ArchivedPostCount / progress.TargetPostCount * 100;
        return Math.Clamp(ratio, 0, 100);
    }
}
