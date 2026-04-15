using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class SyncsPageViewModel : ObservableObject
{
    private readonly IArchiveRunScheduler _archiveRunScheduler;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ISyncSessionManager _syncSessionManager;
    private bool _isInitialized;
    private int _failedCount;
    private int _pausedCount;
    private int _queuedCount;
    private int _runningCount;
    private int _scheduledCount;
    private string _statusMessage = string.Empty;

    public SyncsPageViewModel(IArchiveRunScheduler archiveRunScheduler, ISyncSessionManager syncSessionManager)
    {
        _archiveRunScheduler = archiveRunScheduler;
        _syncSessionManager = syncSessionManager;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Missing dispatcher queue.");
    }

    public Visibility EmptyStateVisibility => Sessions.Count == 0 && ScheduledRuns.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    public Visibility ListVisibility => Sessions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public int PausedCount
    {
        get => _pausedCount;
        set => SetProperty(ref _pausedCount, value);
    }

    public int QueuedCount
    {
        get => _queuedCount;
        set => SetProperty(ref _queuedCount, value);
    }

    public int RunningCount
    {
        get => _runningCount;
        set => SetProperty(ref _runningCount, value);
    }

    public int ScheduledCount
    {
        get => _scheduledCount;
        set => SetProperty(ref _scheduledCount, value);
    }

    public ObservableCollection<SyncSessionItemViewModel> Sessions { get; } = [];

    public ObservableCollection<ScheduledArchiveRunItemViewModel> ScheduledRuns { get; } = [];

    public Visibility ScheduledRunsVisibility => ScheduledRuns.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public void Deactivate()
    {
        if (!_isInitialized)
        {
            return;
        }

        _archiveRunScheduler.ScheduledRunsChanged -= OnScheduledRunsChanged;
        _syncSessionManager.SessionsChanged -= OnSessionsChanged;
        _isInitialized = false;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            RefreshSessions();
            await _archiveRunScheduler.InitializeAsync();
            RefreshScheduledRuns();
            return;
        }

        _archiveRunScheduler.ScheduledRunsChanged += OnScheduledRunsChanged;
        _syncSessionManager.SessionsChanged += OnSessionsChanged;
        await _archiveRunScheduler.InitializeAsync();
        RefreshSessions();
        RefreshScheduledRuns();
        _isInitialized = true;
    }

    public async Task StartAsync(Guid sessionId)
    {
        SyncSessionRecord? session = await _syncSessionManager.StartAsync(sessionId, CancellationToken.None);
        if (session is null)
        {
            StatusMessage = "Sync session no longer exists.";
        }
    }

    public void Pause(Guid sessionId)
    {
        if (!_syncSessionManager.Pause(sessionId))
        {
            StatusMessage = "That sync cannot be paused right now.";
        }
    }

    public void Stop(Guid sessionId)
    {
        if (!_syncSessionManager.StopSession(sessionId))
        {
            StatusMessage = "That sync cannot be stopped right now.";
        }
    }

    public async Task RemoveScheduledRunAsync(Guid runId)
    {
        if (!await _archiveRunScheduler.RemoveAsync(runId, CancellationToken.None))
        {
            StatusMessage = "That scheduled run no longer exists.";
        }
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(RefreshSessions);
    }

    private void OnScheduledRunsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(RefreshScheduledRuns);
    }

    private void RefreshSessions()
    {
        IReadOnlyList<SyncSessionRecord> sessions = _syncSessionManager.GetSessions();

        Sessions.Clear();
        foreach (SyncSessionRecord session in sessions)
        {
            Sessions.Add(new SyncSessionItemViewModel(session));
        }

        RunningCount = sessions.Count(session => session.State == SyncSessionState.Running || session.State == SyncSessionState.Starting);
        QueuedCount = sessions.Count(session => session.State == SyncSessionState.Queued);
        PausedCount = sessions.Count(session => session.State == SyncSessionState.Paused || session.State == SyncSessionState.Pausing);
        FailedCount = sessions.Count(session => session.State == SyncSessionState.Failed);

        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ListVisibility));
    }

    private void RefreshScheduledRuns()
    {
        ScheduledRuns.Clear();
        IReadOnlyList<ScheduledArchiveRunRecord> runs = _archiveRunScheduler.GetRuns()
            .Where(run => run.SourceKind == ScheduledArchiveRunSourceKind.ApiSync)
            .ToList();

        foreach (ScheduledArchiveRunRecord run in runs)
        {
            ScheduledRuns.Add(new ScheduledArchiveRunItemViewModel(run));
        }

        ScheduledCount = runs.Count(run => run.State is ScheduledArchiveRunState.Pending or ScheduledArchiveRunState.WaitingForCapacity);
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ScheduledRunsVisibility));
    }
}
