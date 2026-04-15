using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ProfilesPageViewModel : ObservableObject
{
    private const double MaximumEstimatedRate = 1000;
    private const double MinimumEstimatedRate = 0.01;

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IArchiveImportService _archiveImportService;
    private readonly IArchiveProfileRepository _profileRepository;
    private readonly IApiSyncRequestFactory _apiSyncRequestFactory;
    private readonly IArchiveRunScheduler _archiveRunScheduler;
    private readonly IResourceService _resourceService;
    private readonly ISyncSessionManager _syncSessionManager;
    private bool _isBusy;
    private bool _isInitialized;
    private ArchiveProfile? _selectedProfile;
    private string _statusMessage = string.Empty;

    public ProfilesPageViewModel(
        IAppSettingsRepository appSettingsRepository,
        IArchiveImportService archiveImportService,
        IArchiveProfileRepository profileRepository,
        IApiSyncRequestFactory apiSyncRequestFactory,
        IArchiveRunScheduler archiveRunScheduler,
        ISyncSessionManager syncSessionManager,
        IResourceService resourceService,
        ArchiveProfileEditorViewModel editor,
        ProfileReviewWorkspaceViewModel review)
    {
        _appSettingsRepository = appSettingsRepository;
        _archiveImportService = archiveImportService;
        _profileRepository = profileRepository;
        _apiSyncRequestFactory = apiSyncRequestFactory;
        _archiveRunScheduler = archiveRunScheduler;
        _syncSessionManager = syncSessionManager;
        _resourceService = resourceService;
        Editor = editor;
        Review = review;
        Timing = new ArchiveRunTimingViewModel();
        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    public bool CanOpenWebCapture => Editor.IsWebCaptureSource;

    public bool CanQueueApiSync => Editor.IsApiSource;

    public bool CanReviewApiPosts => Editor.IsApiSource;

    public bool IsReviewUnavailable => !CanReviewApiPosts;

    public ArchiveProfileEditorViewModel Editor { get; }

    public ArchiveRunTimingViewModel Timing { get; }

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<ArchiveProfile> Profiles { get; } = [];

    public string ReviewUnavailableMessage => Editor.IsWebCaptureSource
        ? "Review is only available for API archive profiles."
        : "Save an archive profile to review recent posts.";

    public ProfileReviewWorkspaceViewModel Review { get; }

    public ArchiveProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                Editor.LoadProfile(value);
                OnPropertyChanged(nameof(HasSelectedProfile));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _profileRepository.DeleteAsync(SelectedProfile.ProfileId, CancellationToken.None);
            ArchiveProfile? profileToRemove = Profiles.FirstOrDefault(profile => profile.ProfileId == SelectedProfile.ProfileId);
            if (profileToRemove is not null)
            {
                Profiles.Remove(profileToRemove);
            }

            SelectedProfile = Profiles.FirstOrDefault();
            if (SelectedProfile is null)
            {
                NewProfile();
            }

            await RefreshWorkspaceAsync();
            StatusMessage = _resourceService.GetString("StatusProfileDeleted");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ImportPreviousArchivesAsync(string parentFolderPath)
    {
        IsBusy = true;
        try
        {
            ArchiveImportResult importResult = await _archiveImportService.ImportAsync(parentFolderPath, CancellationToken.None);
            await LoadProfilesAsync(CancellationToken.None);

            if (importResult.ImportedProfiles.Count > 0)
            {
                ArchiveProfile importedProfile = importResult.ImportedProfiles[0];
                SelectedProfile = Profiles.FirstOrDefault(profile => profile.ProfileId == importedProfile.ProfileId)
                    ?? Profiles.FirstOrDefault(
                        profile => string.Equals(profile.Username, importedProfile.Username, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(
                                       Path.GetFullPath(profile.ArchiveRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                       Path.GetFullPath(importedProfile.ArchiveRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                       StringComparison.OrdinalIgnoreCase));
            }

            await RefreshWorkspaceAsync();
            StatusMessage = _resourceService.Format(
                "StatusImportArchivesSummaryFormat",
                importResult.ImportedCount,
                importResult.UpdatedCount,
                importResult.SkippedInvalidCount,
                importResult.SkippedDuplicateCount);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            await RefreshWorkspaceAsync();
            return;
        }

        await LoadProfilesAsync(CancellationToken.None);
        SelectedProfile = Profiles.FirstOrDefault();
        if (SelectedProfile is null)
        {
            NewProfile();
        }

        await RefreshWorkspaceAsync();
        _isInitialized = true;
    }

    public void NewProfile()
    {
        SelectedProfile = null;
        Editor.LoadNewProfile();
        StatusMessage = string.Empty;
    }

    public async Task<SyncSessionRecord> QueueSelectedAsync(ApiSyncRequest request)
    {
        IsBusy = true;
        try
        {
            await _profileRepository.SaveAsync(request.Profile, CancellationToken.None);
            ReplaceOrAddProfile(request.Profile);
            SelectedProfile = request.Profile;
            SyncSessionRecord session = _syncSessionManager.Queue(request);
            StatusMessage = _resourceService.Format("StatusSyncQueuedFormat", request.Profile.Username);
            return session;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshWorkspaceAsync()
    {
        await Review.SetRequestAsync(GetReviewRequest());
        StatusMessage = Review.StatusMessage;
        OnPropertyChanged(nameof(CanOpenWebCapture));
        OnPropertyChanged(nameof(CanQueueApiSync));
        OnPropertyChanged(nameof(CanReviewApiPosts));
        OnPropertyChanged(nameof(IsReviewUnavailable));
        OnPropertyChanged(nameof(ReviewUnavailableMessage));
    }

    public async Task SaveAsync()
    {
        ArchiveProfile? profile = TryCreateDraftProfile();
        if (profile is null)
        {
            return;
        }

        await SaveDraftAsync(profile);
        StatusMessage = _resourceService.GetString("StatusProfileSaved");
    }

    public async Task SaveDraftAsync(ArchiveProfile profile)
    {
        IsBusy = true;
        try
        {
            await _profileRepository.SaveAsync(profile, CancellationToken.None);
            ReplaceOrAddProfile(profile);
            SelectedProfile = profile;
            await RefreshWorkspaceAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetArchiveRootPath(string path)
    {
        Editor.SetArchiveRootPath(path);
    }

    public async Task<decimal?> TryGetEstimatedCostRateAsync()
    {
        AppSettings settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        double estimatedRate = Convert.ToDouble(settings.EstimatedCostPerThousandPostReads, System.Globalization.CultureInfo.InvariantCulture);
        if (estimatedRate < MinimumEstimatedRate || estimatedRate > MaximumEstimatedRate)
        {
            StatusMessage = _resourceService.Format("StatusEstimatedRateValidationFormat", MinimumEstimatedRate, MaximumEstimatedRate);
            return null;
        }

        return settings.EstimatedCostPerThousandPostReads;
    }

    public ArchiveProfile? TryCreateDraftProfile()
    {
        ArchiveProfile? profile = Editor.TryCreateProfile(SelectedProfile, out string? validationError);
        if (profile is not null)
        {
            return profile;
        }

        if (validationError == "StatusProfileValidationMaxPostsFormat")
        {
            StatusMessage = _resourceService.Format("StatusProfileValidationMaxPostsFormat", 5, 3200);
        }
        else if (!string.IsNullOrWhiteSpace(validationError))
        {
            StatusMessage = _resourceService.GetString(validationError);
        }

        return null;
    }

    public async Task<ScheduledArchiveRunRecord> ScheduleSelectedAsync(ApiSyncRequest request, DateTimeOffset scheduledStartUtc)
    {
        IsBusy = true;
        try
        {
            await _profileRepository.SaveAsync(request.Profile, CancellationToken.None);
            ReplaceOrAddProfile(request.Profile);
            SelectedProfile = request.Profile;
            ScheduledArchiveRunRecord scheduledRun = await _archiveRunScheduler.ScheduleApiSyncAsync(request, scheduledStartUtc, CancellationToken.None);
            StatusMessage = $"Scheduled API sync for @{request.Profile.Username} at {scheduledStartUtc.LocalDateTime:g}.";
            return scheduledRun;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public ApiSyncRequest? TryCreateApiSyncRequest()
    {
        ArchiveProfile? profile = TryCreateDraftProfile();
        if (profile is null)
        {
            return null;
        }

        if (!Timing.TryGetArchiveRangeUtc(out DateTimeOffset? archiveStartUtc, out DateTimeOffset? archiveEndUtc, out string? validationError))
        {
            StatusMessage = validationError ?? "Choose a valid archive range before starting the sync.";
            return null;
        }

        return _apiSyncRequestFactory.Create(profile, archiveStartUtc, archiveEndUtc);
    }

    private ApiSyncRequest? GetReviewRequest()
    {
        if (!Editor.IsApiSource)
        {
            return null;
        }

        ArchiveProfile? draft = Editor.TryCreateProfile(SelectedProfile, out _);
        if (draft is null && SelectedProfile is null)
        {
            return null;
        }

        if (!Timing.TryGetArchiveRangeUtc(out DateTimeOffset? archiveStartUtc, out DateTimeOffset? archiveEndUtc, out _))
        {
            return null;
        }

        return _apiSyncRequestFactory.Create(draft ?? SelectedProfile!, archiveStartUtc, archiveEndUtc);
    }

    private async Task LoadProfilesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ArchiveProfile> profiles = await _profileRepository.GetAllAsync(cancellationToken);
        Profiles.Clear();
        foreach (ArchiveProfile profile in profiles.OrderBy(profile => profile.Username, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(profile);
        }
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ArchiveProfileEditorViewModel.PreferredSource))
        {
            OnPropertyChanged(nameof(CanOpenWebCapture));
            OnPropertyChanged(nameof(CanQueueApiSync));
            OnPropertyChanged(nameof(CanReviewApiPosts));
            OnPropertyChanged(nameof(IsReviewUnavailable));
            OnPropertyChanged(nameof(ReviewUnavailableMessage));
        }
    }

    private void ReplaceOrAddProfile(ArchiveProfile profile)
    {
        ArchiveProfile? existingProfile = Profiles.FirstOrDefault(existing => existing.ProfileId == profile.ProfileId);
        if (existingProfile is null)
        {
            Profiles.Add(profile);
            return;
        }

        int existingIndex = Profiles.IndexOf(existingProfile);
        Profiles[existingIndex] = profile;
    }
}
