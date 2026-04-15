using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class HomePageViewModel : ObservableObject
{
    private readonly IArchiveProfileRepository _archiveProfileRepository;
    private readonly IXCredentialStore _credentialStore;
    private readonly IResourceService _resourceService;
    private readonly IScraperSessionStore _scraperSessionStore;
    private readonly ISyncSessionManager _syncSessionManager;
    private string _archiveProfileBreakdownText = string.Empty;
    private bool _isInitialized;
    private string _credentialStatusText = string.Empty;
    private string _profileStatusText = string.Empty;
    private string _recentActivityText = string.Empty;
    private string _scraperStatusText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _syncStatusText = string.Empty;

    public HomePageViewModel(
        IArchiveProfileRepository archiveProfileRepository,
        IXCredentialStore credentialStore,
        IScraperSessionStore scraperSessionStore,
        ISyncSessionManager syncSessionManager,
        IResourceService resourceService)
    {
        _archiveProfileRepository = archiveProfileRepository;
        _credentialStore = credentialStore;
        _scraperSessionStore = scraperSessionStore;
        _syncSessionManager = syncSessionManager;
        _resourceService = resourceService;
    }

    public string CredentialStatusText
    {
        get => _credentialStatusText;
        set => SetProperty(ref _credentialStatusText, value);
    }

    public string ArchiveProfileBreakdownText
    {
        get => _archiveProfileBreakdownText;
        set => SetProperty(ref _archiveProfileBreakdownText, value);
    }

    public string ProfileStatusText
    {
        get => _profileStatusText;
        set => SetProperty(ref _profileStatusText, value);
    }

    public string RecentActivityText
    {
        get => _recentActivityText;
        set => SetProperty(ref _recentActivityText, value);
    }

    public string ScraperStatusText
    {
        get => _scraperStatusText;
        set => SetProperty(ref _scraperStatusText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SyncStatusText
    {
        get => _syncStatusText;
        set => SetProperty(ref _syncStatusText, value);
    }

    public async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _syncSessionManager.SessionsChanged += OnSessionsChanged;
            _isInitialized = true;
        }

        await RefreshAsync();
    }

    public void Deactivate()
    {
        if (!_isInitialized)
        {
            return;
        }

        _syncSessionManager.SessionsChanged -= OnSessionsChanged;
        _isInitialized = false;
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        UpdateSyncStatus();
    }

    private async Task RefreshAsync()
    {
        IReadOnlyList<ArchiveProfile> profiles = await _archiveProfileRepository.GetAllAsync(CancellationToken.None);
        ProfileStatusText = _resourceService.Format("HomeStatusProfilesFormat", profiles.Count);
        int apiCount = profiles.Count(profile => profile.PreferredSource == ArchiveSourceKind.Api);
        int webCaptureCount = profiles.Count(profile => profile.PreferredSource == ArchiveSourceKind.WebCapture);
        ArchiveProfileBreakdownText = $"{apiCount} API profiles · {webCaptureCount} web capture profiles";
        DateTimeOffset? lastArchiveActivity = profiles
            .Where(profile => profile.LastSuccessfulSyncUtc.HasValue)
            .Select(profile => profile.LastSuccessfulSyncUtc)
            .OrderByDescending(value => value)
            .FirstOrDefault();
        RecentActivityText = lastArchiveActivity.HasValue
            ? $"Last successful archive activity: {lastArchiveActivity.Value.ToLocalTime():f}"
            : "No completed archive runs yet.";

        bool hasCredential = await _credentialStore.HasCredentialAsync(CancellationToken.None);
        CredentialStatusText = _resourceService.GetString(hasCredential ? "HomeStatusCredentialReady" : "HomeStatusCredentialMissing");

        ScraperBrowserSessionInfo? scraperSession = _scraperSessionStore.GetSessionInfo();
        ScraperStatusText = GetScraperStatus(scraperSession);

        UpdateSyncStatus();
        StatusMessage = string.Empty;
    }

    private string GetScraperStatus(ScraperBrowserSessionInfo? scraperSession)
    {
        if (scraperSession is null || !scraperSession.IsInitialized)
        {
            return _resourceService.GetString("HomeStatusScraperMissing");
        }

        if (scraperSession.IsValidated)
        {
            return _resourceService.Format("HomeStatusScraperReadyFormat", scraperSession.BrowserDisplayName);
        }

        return _resourceService.Format("HomeStatusScraperPendingFormat", scraperSession.BrowserDisplayName);
    }

    private void UpdateSyncStatus()
    {
        IReadOnlyList<SyncSessionRecord> sessions = _syncSessionManager.GetSessions();
        int runningCount = sessions.Count(session => session.State is SyncSessionState.Running or SyncSessionState.Starting);
        int queuedCount = sessions.Count(session => session.State == SyncSessionState.Queued);
        int pausedCount = sessions.Count(session => session.State is SyncSessionState.Paused or SyncSessionState.Pausing);

        SyncStatusText = runningCount == 0 && queuedCount == 0 && pausedCount == 0
            ? _resourceService.GetString("HomeStatusSyncsIdle")
            : _resourceService.Format("HomeStatusSyncsFormat", runningCount, queuedCount, pausedCount);
    }
}
