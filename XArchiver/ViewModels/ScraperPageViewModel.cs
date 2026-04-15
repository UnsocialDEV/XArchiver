using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ScraperPageViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IArchiveRunScheduler _archiveRunScheduler;
    private readonly IArchiveProfileRepository _archiveProfileRepository;
    private readonly IProfileUrlValidator _profileUrlValidator;
    private readonly IProfileWebScraper _profileWebScraper;
    private readonly IResourceService _resourceService;
    private readonly IScraperBrowserSessionLauncher _scraperBrowserSessionLauncher;
    private readonly IScraperRunManager _scraperRunManager;
    private readonly IWebArchiveRequestFactory _webArchiveRequestFactory;
    private string _archiveRootPath = string.Empty;
    private string _blockingReason = string.Empty;
    private int _collectedPostCount;
    private string _currentPageTitle = string.Empty;
    private string _currentUrl = string.Empty;
    private double _diagnosticsPanelHeight = 280;
    private int _downloadedImageCount;
    private int _downloadedVideoCount;
    private bool _hasValidatedSession;
    private string _htmlSnapshotPath = string.Empty;
    private bool _isBusy;
    private bool _isInitialized;
    private string _latestScreenshotPath = string.Empty;
    private int _maxPostsToScrape = 100;
    private double _previewPostsPanelHeight = 280;
    private string _profileUrl = string.Empty;
    private double _progressValue;
    private int _savedPostCount;
    private ArchiveProfile? _selectedProfile;
    private int _selectedExecutionModeIndex;
    private string _sessionBrowserDisplayName = string.Empty;
    private bool _showLiveScreenshots = true;
    private string _stageText = string.Empty;
    private ScraperRunState _state = ScraperRunState.Idle;
    private bool _statusIsError;
    private string _statusMessage = string.Empty;
    private int _visiblePostCount;

    public ScraperPageViewModel(
        IArchiveRunScheduler archiveRunScheduler,
        IArchiveProfileRepository archiveProfileRepository,
        IProfileUrlValidator profileUrlValidator,
        IProfileWebScraper profileWebScraper,
        IResourceService resourceService,
        IScraperBrowserSessionLauncher scraperBrowserSessionLauncher,
        IScraperRunManager scraperRunManager,
        IWebArchiveRequestFactory webArchiveRequestFactory)
    {
        _archiveRunScheduler = archiveRunScheduler;
        _archiveProfileRepository = archiveProfileRepository;
        _profileUrlValidator = profileUrlValidator;
        _profileWebScraper = profileWebScraper;
        _resourceService = resourceService;
        _scraperBrowserSessionLauncher = scraperBrowserSessionLauncher;
        _scraperRunManager = scraperRunManager;
        _webArchiveRequestFactory = webArchiveRequestFactory;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Missing dispatcher queue.");
        ScreenshotOverlay = new ScraperScreenshotOverlayViewModel();
        Timing = new ArchiveRunTimingViewModel();
    }

    public string ArchiveRootPath
    {
        get => _archiveRootPath;
        private set => SetProperty(ref _archiveRootPath, value);
    }

    public string BlockingReason
    {
        get => _blockingReason;
        set
        {
            if (SetProperty(ref _blockingReason, value))
            {
                OnPropertyChanged(nameof(InterventionVisibility));
            }
        }
    }

    public bool CanOpenLiveBrowser => State is ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.Paused or ScraperRunState.WaitingForIntervention;

    public bool CanForceKill => State is ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.Paused or ScraperRunState.WaitingForIntervention or ScraperRunState.Stopping;

    public bool CanPause => State is ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.WaitingForIntervention;

    public bool CanResume => State is ScraperRunState.Paused or ScraperRunState.WaitingForIntervention;

    public bool CanStartScrape =>
        !IsBusy &&
        HasValidatedSession &&
        SelectedProfile is not null &&
        SelectedProfile.PreferredSource == ArchiveSourceKind.WebCapture &&
        (State is ScraperRunState.Idle or ScraperRunState.Completed or ScraperRunState.Failed or ScraperRunState.Stopped);

    public bool CanStopAndSave => State is ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.Paused or ScraperRunState.WaitingForIntervention;

    public bool CanStop => State is ScraperRunState.Starting or ScraperRunState.Running or ScraperRunState.Paused or ScraperRunState.WaitingForIntervention or ScraperRunState.Stopping;

    public int CollectedPostCount
    {
        get => _collectedPostCount;
        set
        {
            if (SetProperty(ref _collectedPostCount, value))
            {
                OnPropertyChanged(nameof(CollectedPostsText));
                OnPropertyChanged(nameof(ProgressSummaryText));
            }
        }
    }

    public string CollectedPostsText => $"Collected posts: {CollectedPostCount} of {MaxPostsToScrape}";

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        set => SetProperty(ref _currentPageTitle, value);
    }

    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetProperty(ref _currentUrl, value);
    }

    public double DiagnosticsPanelHeight
    {
        get => _diagnosticsPanelHeight;
        set => SetProperty(ref _diagnosticsPanelHeight, value);
    }

    public ObservableCollection<ScraperDiagnosticsEventItemViewModel> DiagnosticsEvents { get; } = [];

    public ObservableCollection<ScheduledArchiveRunItemViewModel> ScheduledRuns { get; } = [];

    public int DownloadedImageCount
    {
        get => _downloadedImageCount;
        set
        {
            if (SetProperty(ref _downloadedImageCount, value))
            {
                OnPropertyChanged(nameof(DownloadedImagesText));
                OnPropertyChanged(nameof(ProgressSummaryText));
            }
        }
    }

    public string DownloadedImagesText => $"Images saved: {DownloadedImageCount}";

    public int DownloadedVideoCount
    {
        get => _downloadedVideoCount;
        set
        {
            if (SetProperty(ref _downloadedVideoCount, value))
            {
                OnPropertyChanged(nameof(DownloadedVideosText));
                OnPropertyChanged(nameof(ProgressSummaryText));
            }
        }
    }

    public string DownloadedVideosText => $"Videos saved: {DownloadedVideoCount}";

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool HasSnapshot => !string.IsNullOrWhiteSpace(LatestScreenshotPath);

    public bool HasValidatedSession
    {
        get => _hasValidatedSession;
        set
        {
            if (SetProperty(ref _hasValidatedSession, value))
            {
                OnPropertyChanged(nameof(SessionStatusText));
                OnPropertyChanged(nameof(CanStartScrape));
            }
        }
    }

    public string HtmlSnapshotPath
    {
        get => _htmlSnapshotPath;
        set => SetProperty(ref _htmlSnapshotPath, value);
    }

    public Visibility InfoStatusVisibility => !string.IsNullOrWhiteSpace(StatusMessage) && !StatusIsError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorStatusVisibility => !string.IsNullOrWhiteSpace(StatusMessage) && StatusIsError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InterventionVisibility => string.IsNullOrWhiteSpace(BlockingReason) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanStartScrape));
            }
        }
    }

    public string LatestScreenshotPath
    {
        get => _latestScreenshotPath;
        set
        {
            if (SetProperty(ref _latestScreenshotPath, value))
            {
                OnPropertyChanged(nameof(HasSnapshot));
                OnPropertyChanged(nameof(LiveScreenshotEmptyVisibility));
                OnPropertyChanged(nameof(LiveScreenshotVisibility));
            }
        }
    }

    public Visibility LiveScreenshotEmptyVisibility => HasSnapshot ? Visibility.Collapsed : Visibility.Visible;

    public Visibility LiveScreenshotSectionVisibility => ShowLiveScreenshots ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LiveScreenshotVisibility => HasSnapshot ? Visibility.Visible : Visibility.Collapsed;

    public int MaxPostsToScrape
    {
        get => _maxPostsToScrape;
        private set
        {
            if (SetProperty(ref _maxPostsToScrape, value))
            {
                OnPropertyChanged(nameof(TargetPostsText));
                OnPropertyChanged(nameof(CollectedPostsText));
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }
    }

    public string ProfileContextText => SelectedProfile is null
        ? "Select a saved profile to run web capture."
        : $"@{SelectedProfile.Username} · {SelectedProfile.ArchiveRootPath}";

    public string ProfileUrl
    {
        get => _profileUrl;
        private set => SetProperty(ref _profileUrl, value);
    }

    public ObservableCollection<ArchiveProfile> Profiles { get; } = [];

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ExecutionModeDescription => SelectedExecutionMode switch
    {
        ScraperExecutionMode.Conservative => "Reduces request intensity, adds cooldowns, and stops early when X starts blocking or degrading the session.",
        _ => "Uses the standard pacing and media-resolution flow for the fastest archive attempt.",
    };

    public ObservableCollection<ScraperPreviewPostItemViewModel> PreviewPosts { get; } = [];

    public Visibility ScheduledRunsEmptyVisibility => ScheduledRuns.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ScheduledRunsListVisibility => ScheduledRuns.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public double PreviewPostsPanelHeight
    {
        get => _previewPostsPanelHeight;
        set => SetProperty(ref _previewPostsPanelHeight, value);
    }

    public string ProgressPercentText => $"{Math.Round(ProgressValue, MidpointRounding.AwayFromZero):0}% of target";

    public string ProgressSummaryText => $"Saved {SavedPostCount} posts · Visible {VisiblePostCount} · Images {DownloadedImageCount} · Videos {DownloadedVideoCount}";

    public int SavedPostCount
    {
        get => _savedPostCount;
        set
        {
            if (SetProperty(ref _savedPostCount, value))
            {
                OnPropertyChanged(nameof(ProgressSummaryText));
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }
    }

    public ArchiveProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ApplySelectedProfile(value);
            }
        }
    }

    public ScraperExecutionMode SelectedExecutionMode => SelectedExecutionModeIndex == 1
        ? ScraperExecutionMode.Conservative
        : ScraperExecutionMode.Normal;

    public int SelectedExecutionModeIndex
    {
        get => _selectedExecutionModeIndex;
        set
        {
            if (SetProperty(ref _selectedExecutionModeIndex, value))
            {
                OnPropertyChanged(nameof(SelectedExecutionMode));
                OnPropertyChanged(nameof(ExecutionModeDescription));
            }
        }
    }

    public string SelectedProfileSourceText => SelectedProfile?.PreferredSource.ToString() ?? "No source selected";

    public string SessionBrowserDisplayName
    {
        get => _sessionBrowserDisplayName;
        set
        {
            if (SetProperty(ref _sessionBrowserDisplayName, value))
            {
                OnPropertyChanged(nameof(SessionStatusText));
            }
        }
    }

    public Visibility SessionLoginActionsVisibility => HasValidatedSession ? Visibility.Collapsed : Visibility.Visible;

    public string SessionStatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SessionBrowserDisplayName))
            {
                return _resourceService.GetString("ScraperPageSessionMissing");
            }

            return HasValidatedSession
                ? _resourceService.Format("ScraperPageSessionReadyFormat", SessionBrowserDisplayName)
                : _resourceService.Format("ScraperPageSessionPendingFormat", SessionBrowserDisplayName);
        }
    }

    public bool ShowLiveScreenshots
    {
        get => _showLiveScreenshots;
        set
        {
            if (SetProperty(ref _showLiveScreenshots, value))
            {
                OnPropertyChanged(nameof(LiveScreenshotSectionVisibility));
            }
        }
    }

    public ScraperScreenshotOverlayViewModel ScreenshotOverlay { get; }

    public ArchiveRunTimingViewModel Timing { get; }

    public string StageText
    {
        get => _stageText;
        set => SetProperty(ref _stageText, value);
    }

    public ScraperRunState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(CanOpenLiveBrowser));
                OnPropertyChanged(nameof(CanForceKill));
                OnPropertyChanged(nameof(CanPause));
                OnPropertyChanged(nameof(CanResume));
                OnPropertyChanged(nameof(CanStartScrape));
                OnPropertyChanged(nameof(CanStopAndSave));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(StateText));
            }
        }
    }

    public string StateText => State switch
    {
        ScraperRunState.Starting => "Starting",
        ScraperRunState.Running => "Running",
        ScraperRunState.Paused => "Paused",
        ScraperRunState.WaitingForIntervention => "Waiting for intervention",
        ScraperRunState.Stopping => "Stopping",
        ScraperRunState.Stopped => "Stopped",
        ScraperRunState.Completed => "Completed",
        ScraperRunState.Failed => "Failed",
        _ => "Idle",
    };

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set
        {
            if (SetProperty(ref _statusIsError, value))
            {
                OnPropertyChanged(nameof(ErrorStatusVisibility));
                OnPropertyChanged(nameof(InfoStatusVisibility));
            }
        }
    }

    public string TargetPostsText => $"Target posts: {MaxPostsToScrape}";

    public int VisiblePostCount
    {
        get => _visiblePostCount;
        set
        {
            if (SetProperty(ref _visiblePostCount, value))
            {
                OnPropertyChanged(nameof(ProgressSummaryText));
            }
        }
    }

    public void CloseScreenshotOverlay()
    {
        ScreenshotOverlay.Close();
    }

    public void Deactivate()
    {
        if (!_isInitialized)
        {
            return;
        }

        _archiveRunScheduler.ScheduledRunsChanged -= OnScheduledRunsChanged;
        _scraperRunManager.RunChanged -= OnRunChanged;
        _isInitialized = false;
    }

    public bool ForceKillScrape()
    {
        bool forceKilled = _scraperRunManager.ForceKill();
        if (!forceKilled)
        {
            SetStatus("The current web capture run cannot be force killed right now.", isError: true);
        }

        return forceKilled;
    }

    public async Task InitializeAsync(Guid? selectedProfileId = null)
    {
        if (_isInitialized)
        {
            await _archiveRunScheduler.InitializeAsync();
            await RefreshProfilesAsync(selectedProfileId);
            await RefreshSessionStatusAsync();
            RefreshScheduledRuns();
            RefreshRun();
            return;
        }

        _archiveRunScheduler.ScheduledRunsChanged += OnScheduledRunsChanged;
        _scraperRunManager.RunChanged += OnRunChanged;
        _isInitialized = true;
        await _archiveRunScheduler.InitializeAsync();
        await RefreshProfilesAsync(selectedProfileId);
        await RefreshSessionStatusAsync();
        RefreshScheduledRuns();
        RefreshRun();
    }

    public bool OpenLiveBrowser()
    {
        bool opened = _scraperRunManager.OpenLiveBrowser();
        if (!opened)
        {
            SetStatus("No active web capture run is available for live browser mode.", isError: true);
        }

        return opened;
    }

    public async Task OpenLoginBrowserAsync()
    {
        ProfileUrlValidationResult? parsedUrl = _profileUrlValidator.Validate(ProfileUrl);
        string targetUrl = parsedUrl?.NormalizedUrl ?? "https://x.com/i/flow/login";

        IsBusy = true;
        StageText = "Opening X login browser";
        try
        {
            ScraperBrowserSessionInfo sessionInfo = await _scraperBrowserSessionLauncher
                .OpenLoginBrowserAsync(
                    new ScraperBrowserLaunchOptions
                    {
                        TargetUrl = targetUrl,
                    },
                    CancellationToken.None);
            SessionBrowserDisplayName = sessionInfo.BrowserDisplayName;
            HasValidatedSession = false;
            OnPropertyChanged(nameof(SessionLoginActionsVisibility));
            SetStatus(_resourceService.Format("StatusScraperBrowserOpenedFormat", sessionInfo.BrowserDisplayName));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenScreenshotOverlay()
    {
        if (!HasSnapshot)
        {
            return;
        }

        ScreenshotOverlay.Open(LatestScreenshotPath);
    }

    public bool PauseScrape()
    {
        bool paused = _scraperRunManager.Pause();
        if (!paused)
        {
            SetStatus("The current web capture run cannot be paused right now.", isError: true);
        }

        return paused;
    }

    public void ReportUnexpectedError(string message)
    {
        SetStatus(_resourceService.Format("StatusUnexpectedErrorFormat", message), isError: true);
    }

    public async Task RefreshProfilesAsync(Guid? selectedProfileId = null)
    {
        IReadOnlyList<ArchiveProfile> profiles = await _archiveProfileRepository.GetAllAsync(CancellationToken.None);
        Profiles.Clear();
        foreach (ArchiveProfile profile in profiles.OrderBy(profile => profile.Username, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = ResolveSelectedProfile(selectedProfileId);
        OnPropertyChanged(nameof(HasSelectedProfile));
    }

    public async Task RefreshSessionStatusAsync()
    {
        ScraperBrowserSessionInfo? sessionInfo = _scraperBrowserSessionLauncher.GetCurrentSession();
        SessionBrowserDisplayName = sessionInfo?.BrowserDisplayName ?? string.Empty;
        HasValidatedSession = await _profileWebScraper.HasSessionAsync(CancellationToken.None);
        OnPropertyChanged(nameof(SessionStatusText));
        OnPropertyChanged(nameof(SessionLoginActionsVisibility));
    }

    public bool ResumeScrape()
    {
        bool resumed = _scraperRunManager.Resume();
        if (!resumed)
        {
            SetStatus("The current web capture run cannot be resumed right now.", isError: true);
        }

        return resumed;
    }

    public async Task ResetSessionAsync()
    {
        IsBusy = true;
        StageText = "Resetting web capture session";
        try
        {
            await _scraperBrowserSessionLauncher.ResetAsync(CancellationToken.None);
            SessionBrowserDisplayName = string.Empty;
            HasValidatedSession = false;
            OnPropertyChanged(nameof(SessionLoginActionsVisibility));
            SetStatus(_resourceService.GetString("StatusScraperSessionReset"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StartScrapeAsync()
    {
        WebArchiveRequest? request = await TryCreateRequestAsync(requireValidatedSession: true);
        if (request is null)
        {
            return;
        }

        bool started = await _scraperRunManager.StartAsync(request, CancellationToken.None);

        if (!started)
        {
            SetStatus("Another web capture run is already active. Stop it before starting a new one.", isError: true);
        }
    }

    public async Task ScheduleScrapeAsync()
    {
        WebArchiveRequest? request = await TryCreateRequestAsync(requireValidatedSession: false);
        if (request is null)
        {
            return;
        }

        if (!Timing.TryGetScheduledStartUtc(out DateTimeOffset? scheduledStartUtc, out string? validationError))
        {
            SetStatus(validationError ?? "Choose a future date and time for the scheduled web capture.", isError: true);
            return;
        }

        if (!scheduledStartUtc.HasValue)
        {
            SetStatus("Turn on Scheduled Start Time before scheduling a web capture run.", isError: true);
            return;
        }

        await _archiveRunScheduler.ScheduleWebCaptureAsync(request, scheduledStartUtc.Value, CancellationToken.None);
        SetStatus($"Scheduled web capture for @{request.Username} at {scheduledStartUtc.Value.LocalDateTime:g}.");
        RefreshScheduledRuns();
    }

    public async Task RemoveScheduledRunAsync(Guid runId)
    {
        bool removed = await _archiveRunScheduler.RemoveAsync(runId, CancellationToken.None);
        if (!removed)
        {
            SetStatus("That scheduled run no longer exists.", isError: true);
        }
    }

    public bool StopScrape()
    {
        bool stopped = _scraperRunManager.Stop();
        if (!stopped)
        {
            SetStatus("The current web capture run cannot be stopped right now.", isError: true);
        }

        return stopped;
    }

    public bool StopScrapeAndSave()
    {
        bool stopped = _scraperRunManager.StopAndSave();
        if (!stopped)
        {
            SetStatus("The current web capture run cannot be stopped and saved right now.", isError: true);
        }

        return stopped;
    }

    public void UpdatePageLayout(double width, double height)
    {
        double listHeight = Math.Clamp((height - 560) / 2d, 180, 420);
        DiagnosticsPanelHeight = listHeight;
        PreviewPostsPanelHeight = listHeight;
        ScreenshotOverlay.UpdateLayout(Math.Max(width - 96, 320), Math.Max(height - 192, 240));
    }

    public async Task ValidateSessionAsync()
    {
        if (_scraperBrowserSessionLauncher.GetCurrentSession() is null)
        {
            SetStatus(_resourceService.GetString("StatusScraperSignInIncomplete"), isError: true);
            return;
        }

        ProfileUrlValidationResult? parsedUrl = _profileUrlValidator.Validate(ProfileUrl);
        string targetUrl = parsedUrl?.NormalizedUrl ?? "https://x.com/home";

        IsBusy = true;
        StageText = "Validating browser session";
        try
        {
            bool isValid = await _profileWebScraper.ValidateSessionAsync(targetUrl, CancellationToken.None);
            await RefreshSessionStatusAsync();
            SetStatus(
                isValid
                    ? _resourceService.Format("StatusScraperValidationSuccessFormat", SessionBrowserDisplayName)
                    : _resourceService.GetString("StatusScraperValidationFailure"),
                isError: !isValid);
        }
        catch (InvalidOperationException exception)
        {
            HasValidatedSession = false;
            OnPropertyChanged(nameof(SessionLoginActionsVisibility));
            SetStatus(exception.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySelectedProfile(ArchiveProfile? profile)
    {
        ArchiveRootPath = profile?.ArchiveRootPath ?? string.Empty;
        ProfileUrl = profile?.ProfileUrl ?? string.Empty;
        MaxPostsToScrape = profile?.MaxPostsPerWebArchive ?? 100;
        OnPropertyChanged(nameof(CanStartScrape));
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(ProfileContextText));
        OnPropertyChanged(nameof(SelectedProfileSourceText));

        if (profile is null)
        {
            SetStatus("Select a saved profile to begin web capture.", isError: false);
        }
        else if (profile.PreferredSource != ArchiveSourceKind.WebCapture)
        {
            SetStatus("This profile is currently configured for API sync. Switch it to Web Capture in Archive Profiles to use browser archiving.", isError: false);
        }
        else
        {
            SetStatus(string.Empty);
        }
    }

    private void OnRunChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(RefreshRun);
    }

    private void OnScheduledRunsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(RefreshScheduledRuns);
    }

    private void RefreshRun()
    {
        ScraperRunSnapshot? run = _scraperRunManager.GetCurrentRun();
        if (run is null)
        {
            State = ScraperRunState.Idle;
            BlockingReason = string.Empty;
            CurrentPageTitle = string.Empty;
            CurrentUrl = string.Empty;
            HtmlSnapshotPath = string.Empty;
            LatestScreenshotPath = string.Empty;
            ScreenshotOverlay.Close();
            PreviewPosts.Clear();
            DiagnosticsEvents.Clear();
            ProgressValue = 0;
            StageText = string.Empty;
            VisiblePostCount = 0;
            CollectedPostCount = 0;
            DownloadedImageCount = 0;
            DownloadedVideoCount = 0;
            SavedPostCount = 0;
            if (SelectedProfile is null)
            {
                SetStatus("Select a saved profile to begin web capture.");
            }

            return;
        }

        State = run.State;
        BlockingReason = run.BlockingReason;
        CurrentPageTitle = run.PageTitle;
        CurrentUrl = run.CurrentUrl;
        HtmlSnapshotPath = run.HtmlSnapshotPath;
        LatestScreenshotPath = run.LiveSnapshot?.ScreenshotPath ?? run.Progress.LatestScreenshotPath;
        StageText = run.Progress.StageText;
        StatusMessage = run.StatusText;
        VisiblePostCount = run.Progress.VisiblePostCount;
        CollectedPostCount = run.Progress.CollectedPostCount;
        DownloadedImageCount = run.Progress.DownloadedImageCount;
        DownloadedVideoCount = run.Progress.DownloadedVideoCount;
        SavedPostCount = run.Progress.SavedPostCount;
        ProgressValue = run.Progress.TargetPostCount <= 0
            ? 0
            : Math.Clamp((double)Math.Max(run.Progress.CollectedPostCount, run.Progress.SavedPostCount) / run.Progress.TargetPostCount * 100, 0, 100);
        OnPropertyChanged(nameof(ProgressPercentText));

        DiagnosticsEvents.Clear();
        foreach (ScraperDiagnosticsEvent diagnosticsEvent in run.Events.OrderByDescending(item => item.TimestampUtc))
        {
            DiagnosticsEvents.Add(new ScraperDiagnosticsEventItemViewModel(diagnosticsEvent));
        }

        PreviewPosts.Clear();
        foreach (ScrapedPostRecord post in run.PreviewPosts)
        {
            PreviewPosts.Add(new ScraperPreviewPostItemViewModel(post));
        }

        SetStatus(run.StatusText, isError: run.State == ScraperRunState.Failed);
    }

    private void RefreshScheduledRuns()
    {
        ScheduledRuns.Clear();
        foreach (ScheduledArchiveRunRecord run in _archiveRunScheduler.GetRuns().Where(run => run.SourceKind == ScheduledArchiveRunSourceKind.WebCapture))
        {
            ScheduledRuns.Add(new ScheduledArchiveRunItemViewModel(run));
        }

        OnPropertyChanged(nameof(ScheduledRunsEmptyVisibility));
        OnPropertyChanged(nameof(ScheduledRunsListVisibility));
    }

    private ArchiveProfile? ResolveSelectedProfile(Guid? selectedProfileId)
    {
        if (selectedProfileId.HasValue)
        {
            ArchiveProfile? matchingProfile = Profiles.FirstOrDefault(profile => profile.ProfileId == selectedProfileId.Value);
            if (matchingProfile is not null)
            {
                return matchingProfile;
            }
        }

        return Profiles.FirstOrDefault(profile => profile.PreferredSource == ArchiveSourceKind.WebCapture)
            ?? Profiles.FirstOrDefault();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusIsError = isError && !string.IsNullOrWhiteSpace(message);
        StatusMessage = message;
        OnPropertyChanged(nameof(ErrorStatusVisibility));
        OnPropertyChanged(nameof(InfoStatusVisibility));
    }

    private async Task<WebArchiveRequest?> TryCreateRequestAsync(bool requireValidatedSession)
    {
        if (SelectedProfile is null)
        {
            SetStatus("Select a saved profile before starting web capture.", isError: true);
            return null;
        }

        if (SelectedProfile.PreferredSource != ArchiveSourceKind.WebCapture)
        {
            SetStatus("Switch this profile to Web Capture in Archive Profiles before starting a browser archive.", isError: true);
            return null;
        }

        ProfileUrlValidationResult? validation = _profileUrlValidator.Validate(ProfileUrl);
        if (validation is null)
        {
            SetStatus(_resourceService.GetString("StatusScraperInvalidUrl"), isError: true);
            return null;
        }

        if (string.IsNullOrWhiteSpace(ArchiveRootPath))
        {
            SetStatus(_resourceService.GetString("StatusProfileValidationFolder"), isError: true);
            return null;
        }

        if (MaxPostsToScrape < 1)
        {
            SetStatus(_resourceService.GetString("StatusScraperInvalidMaxPosts"), isError: true);
            return null;
        }

        if (requireValidatedSession && !HasValidatedSession)
        {
            SetStatus(_resourceService.GetString("StatusScraperSignInRequired"), isError: true);
            return null;
        }

        ArchiveProfile runProfile = new()
        {
            ArchiveRootPath = ArchiveRootPath.Trim(),
            DownloadImages = SelectedProfile.DownloadImages,
            DownloadVideos = SelectedProfile.DownloadVideos,
            IncludeOriginalPosts = SelectedProfile.IncludeOriginalPosts,
            IncludeQuotes = SelectedProfile.IncludeQuotes,
            IncludeReplies = SelectedProfile.IncludeReplies,
            IncludeReposts = SelectedProfile.IncludeReposts,
            LastSinceId = SelectedProfile.LastSinceId,
            LastSuccessfulSyncUtc = SelectedProfile.LastSuccessfulSyncUtc,
            MaxPostsPerSync = SelectedProfile.MaxPostsPerSync,
            MaxPostsPerWebArchive = MaxPostsToScrape,
            PreferredSource = ArchiveSourceKind.WebCapture,
            ProfileId = SelectedProfile.ProfileId,
            ProfileUrl = validation.NormalizedUrl,
            UserId = SelectedProfile.UserId,
            Username = validation.Username,
        };

        if (!Timing.TryGetArchiveRangeUtc(out DateTimeOffset? archiveStartUtc, out DateTimeOffset? archiveEndUtc, out string? validationError))
        {
            SetStatus(validationError ?? "Choose a valid archive range before starting web capture.", isError: true);
            return null;
        }

        await _archiveProfileRepository.SaveAsync(runProfile, CancellationToken.None);
        SelectedProfile = runProfile;
        return _webArchiveRequestFactory.Create(runProfile, SelectedExecutionMode, archiveStartUtc, archiveEndUtc);
    }
}
