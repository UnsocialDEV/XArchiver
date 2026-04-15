using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ScraperPauseGate : IScraperPauseGate
{
    private readonly object _syncRoot = new();
    private bool _isPauseRequested;
    private TaskCompletionSource _resumeSource = CreateResumeSource();

    public event EventHandler<ScraperPauseStateChangedEventArgs>? StateChanged;

    public bool IsPauseRequested
    {
        get
        {
            lock (_syncRoot)
            {
                return _isPauseRequested;
            }
        }
    }

    public void Pause()
    {
        bool shouldRaiseEvent = false;

        lock (_syncRoot)
        {
            if (_isPauseRequested)
            {
                return;
            }

            _isPauseRequested = true;
            shouldRaiseEvent = true;
        }

        if (shouldRaiseEvent)
        {
            RaiseStateChanged(isPaused: false, isPauseRequested: true);
        }
    }

    public void ResumeScrape()
    {
        TaskCompletionSource? resumeSource = null;

        lock (_syncRoot)
        {
            if (!_isPauseRequested)
            {
                return;
            }

            _isPauseRequested = false;
            resumeSource = _resumeSource;
            _resumeSource = CreateResumeSource();
        }

        resumeSource.TrySetResult();
        RaiseStateChanged(isPaused: false, isPauseRequested: false);
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask = null;
        bool enteredPausedState = false;

        lock (_syncRoot)
        {
            if (_isPauseRequested)
            {
                waitTask = _resumeSource.Task;
                enteredPausedState = true;
            }
        }

        if (!enteredPausedState || waitTask is null)
        {
            return;
        }

        RaiseStateChanged(isPaused: true, isPauseRequested: true);

        try
        {
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static TaskCompletionSource CreateResumeSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void RaiseStateChanged(bool isPaused, bool isPauseRequested)
    {
        StateChanged?.Invoke(
            this,
            new ScraperPauseStateChangedEventArgs
            {
                IsPaused = isPaused,
                IsPauseRequested = isPauseRequested,
            });
    }
}
