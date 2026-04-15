using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Services;

public sealed class ArchiveRunScheduler : IArchiveRunScheduler
{
    private readonly object _syncRoot = new();
    private readonly IXCredentialStore _credentialStore;
    private readonly IProfileWebScraper _profileWebScraper;
    private readonly IScheduledArchiveRunRepository _repository;
    private readonly IScraperRunManager _scraperRunManager;
    private readonly ISyncSessionManager _syncSessionManager;
    private readonly TimeProvider _timeProvider;
    private readonly List<ScheduledArchiveRunRecord> _runs = [];
    private int _isDispatching;
    private Task? _processingLoopTask;
    private bool _isInitialized;

    public ArchiveRunScheduler(
        IXCredentialStore credentialStore,
        IProfileWebScraper profileWebScraper,
        IScheduledArchiveRunRepository repository,
        IScraperRunManager scraperRunManager,
        ISyncSessionManager syncSessionManager,
        TimeProvider timeProvider)
    {
        _credentialStore = credentialStore;
        _profileWebScraper = profileWebScraper;
        _repository = repository;
        _scraperRunManager = scraperRunManager;
        _syncSessionManager = syncSessionManager;
        _timeProvider = timeProvider;
    }

    public event EventHandler? ScheduledRunsChanged;

    public IReadOnlyList<ScheduledArchiveRunRecord> GetRuns()
    {
        lock (_syncRoot)
        {
            return _runs
                .OrderBy(run => run.ScheduledStartUtc)
                .ThenByDescending(run => run.UpdatedAtUtc)
                .ToList();
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            await DispatchDueRunsAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<ScheduledArchiveRunRecord> runs = await _repository.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
        lock (_syncRoot)
        {
            _runs.Clear();
            _runs.AddRange(runs);
            _isInitialized = true;
        }

        PublishScheduledRunsChanged();
        _processingLoopTask ??= Task.Run(ProcessLoopAsync);
        await DispatchDueRunsAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(Guid runId, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(runId, cancellationToken).ConfigureAwait(false);

        bool removed = false;
        lock (_syncRoot)
        {
            ScheduledArchiveRunRecord? existing = _runs.FirstOrDefault(run => run.RunId == runId);
            if (existing is not null)
            {
                _runs.Remove(existing);
                removed = true;
            }
        }

        if (removed)
        {
            PublishScheduledRunsChanged();
        }

        return removed;
    }

    public async Task<ScheduledArchiveRunRecord> ScheduleApiSyncAsync(ApiSyncRequest request, DateTimeOffset scheduledStartUtc, CancellationToken cancellationToken)
    {
        ScheduledArchiveRunRecord run = ScheduledArchiveRunFactory.CreateApiSync(request, scheduledStartUtc);
        await SaveRunAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<ScheduledArchiveRunRecord> ScheduleWebCaptureAsync(WebArchiveRequest request, DateTimeOffset scheduledStartUtc, CancellationToken cancellationToken)
    {
        ScheduledArchiveRunRecord run = ScheduledArchiveRunFactory.CreateWebCapture(request, scheduledStartUtc);
        await SaveRunAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task DispatchDueRunsAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isDispatching, 1) == 1)
        {
            return;
        }

        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            IReadOnlyList<ScheduledArchiveRunRecord> dueRuns = GetRuns()
                .Where(run =>
                    run.State is ScheduledArchiveRunState.Pending or ScheduledArchiveRunState.WaitingForCapacity &&
                    run.ScheduledStartUtc <= now)
                .ToList();

            foreach (ScheduledArchiveRunRecord run in dueRuns)
            {
                await DispatchRunAsync(run, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isDispatching, 0);
        }
    }

    private async Task DispatchRunAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken)
    {
        switch (run.SourceKind)
        {
            case ScheduledArchiveRunSourceKind.ApiSync:
                await DispatchApiSyncAsync(run, cancellationToken).ConfigureAwait(false);
                break;
            case ScheduledArchiveRunSourceKind.WebCapture:
                await DispatchWebCaptureAsync(run, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task DispatchApiSyncAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken)
    {
        if (run.ApiSyncRequest is null)
        {
            await SaveRunAsync(CreateFailedRun(run, "The scheduled API sync request is missing."), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await _credentialStore.HasCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            await SaveRunAsync(CreateFailedRun(run, "Scheduled API sync failed because no X credential is configured."), cancellationToken).ConfigureAwait(false);
            return;
        }

        _syncSessionManager.Queue(run.ApiSyncRequest);
        await SaveRunAsync(
            run with
            {
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                DispatchedAtUtc = _timeProvider.GetUtcNow(),
                State = ScheduledArchiveRunState.Dispatched,
                StatusText = "Scheduled API sync dispatched to Sync Activity.",
                UpdatedAtUtc = _timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchWebCaptureAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken)
    {
        if (run.WebArchiveRequest is null)
        {
            await SaveRunAsync(CreateFailedRun(run, "The scheduled web capture request is missing."), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await _profileWebScraper.HasSessionAsync(cancellationToken).ConfigureAwait(false))
        {
            await SaveRunAsync(CreateFailedRun(run, "Scheduled web capture failed because no validated browser session is available."), cancellationToken).ConfigureAwait(false);
            return;
        }

        bool started = await _scraperRunManager.StartAsync(run.WebArchiveRequest, cancellationToken).ConfigureAwait(false);
        if (!started)
        {
            await SaveRunAsync(
                run with
                {
                    State = ScheduledArchiveRunState.WaitingForCapacity,
                    StatusText = "Waiting for the current web capture run to finish before starting this scheduled capture.",
                    UpdatedAtUtc = _timeProvider.GetUtcNow(),
                },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await SaveRunAsync(
            run with
            {
                CompletedAtUtc = _timeProvider.GetUtcNow(),
                DispatchedAtUtc = _timeProvider.GetUtcNow(),
                State = ScheduledArchiveRunState.Dispatched,
                StatusText = "Scheduled web capture started.",
                UpdatedAtUtc = _timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private ScheduledArchiveRunRecord CreateFailedRun(ScheduledArchiveRunRecord run, string statusText)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return run with
        {
            CompletedAtUtc = now,
            State = ScheduledArchiveRunState.Failed,
            StatusText = statusText,
            UpdatedAtUtc = now,
        };
    }

    private async Task ProcessLoopAsync()
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                await DispatchDueRunsAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Keep the scheduler alive. Errors remain reflected in per-run state updates.
            }
        }
    }

    private void PublishScheduledRunsChanged()
    {
        ScheduledRunsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveRunAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken)
    {
        await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            ScheduledArchiveRunRecord? existing = _runs.FirstOrDefault(candidate => candidate.RunId == run.RunId);
            if (existing is null)
            {
                _runs.Add(run);
            }
            else
            {
                _runs[_runs.IndexOf(existing)] = run;
            }
        }

        PublishScheduledRunsChanged();
    }
}
