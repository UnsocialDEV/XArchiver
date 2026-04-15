using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ReviewPageViewModel : ObservableObject
{
    private const double MaximumEstimatedRate = 1000;
    private const double MinimumEstimatedRate = 0.01;

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IArchiveProfileRepository _profileRepository;
    private readonly IManualArchiveService _manualArchiveService;
    private readonly IPostReviewService _postReviewService;
    private readonly IResourceService _resourceService;
    private readonly IReviewCostFormatter _reviewCostFormatter;
    private decimal _estimatedCostPerThousandPostReads = 5.02m;
    private string _filterSummaryText = string.Empty;
    private bool _isBusy;
    private string? _nextToken;
    private string _previewEstimateText = string.Empty;
    private ArchiveProfile? _selectedProfile;
    private ReviewPostItemViewModel? _selectedPost;
    private string _selectedPostCreatedAtText = string.Empty;
    private string _selectedPostMetricsSummary = string.Empty;
    private string _selectedPostText = string.Empty;
    private string _selectedPostTypeText = string.Empty;
    private string _statusMessage = string.Empty;

    public ReviewPageViewModel(
        IAppSettingsRepository appSettingsRepository,
        IArchiveProfileRepository profileRepository,
        IPostReviewService postReviewService,
        IManualArchiveService manualArchiveService,
        IReviewCostFormatter reviewCostFormatter,
        IResourceService resourceService)
    {
        _appSettingsRepository = appSettingsRepository;
        _profileRepository = profileRepository;
        _postReviewService = postReviewService;
        _manualArchiveService = manualArchiveService;
        _reviewCostFormatter = reviewCostFormatter;
        _resourceService = resourceService;
    }

    public bool CanArchiveSelected => Posts.Any(post => post.IsSelected && !post.IsAlreadyArchived);

    public bool CanLoadMore => !string.IsNullOrWhiteSpace(_nextToken);

    public bool CanSelectVisible => Posts.Any(post => !post.IsAlreadyArchived);

    public string FilterSummaryText
    {
        get => _filterSummaryText;
        set => SetProperty(ref _filterSummaryText, value);
    }

    public bool HasSelectedImages => SelectedImages.Count > 0;

    public bool HasSelectedPost => SelectedPost is not null;

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool HasSelectedVideos => SelectedVideos.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<ArchiveProfile> Profiles { get; } = [];

    public ObservableCollection<ReviewPostItemViewModel> Posts { get; } = [];

    public string PreviewEstimateText
    {
        get => _previewEstimateText;
        set => SetProperty(ref _previewEstimateText, value);
    }

    public ArchiveProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                OnSelectedProfileChanged(value);
            }
        }
    }

    public ObservableCollection<PreviewMediaRecord> SelectedImages { get; } = [];

    public ReviewPostItemViewModel? SelectedPost
    {
        get => _selectedPost;
        set
        {
            if (SetProperty(ref _selectedPost, value))
            {
                OnSelectedPostChanged(value);
            }
        }
    }

    public string SelectedPostCreatedAtText
    {
        get => _selectedPostCreatedAtText;
        set => SetProperty(ref _selectedPostCreatedAtText, value);
    }

    public string SelectedPostMetricsSummary
    {
        get => _selectedPostMetricsSummary;
        set => SetProperty(ref _selectedPostMetricsSummary, value);
    }

    public string SelectedPostText
    {
        get => _selectedPostText;
        set => SetProperty(ref _selectedPostText, value);
    }

    public string SelectedPostTypeText
    {
        get => _selectedPostTypeText;
        set => SetProperty(ref _selectedPostTypeText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<PreviewMediaRecord> SelectedVideos { get; } = [];

    public decimal? TryGetEstimatedCostRate()
    {
        if (_estimatedCostPerThousandPostReads < (decimal)MinimumEstimatedRate ||
            _estimatedCostPerThousandPostReads > (decimal)MaximumEstimatedRate)
        {
            StatusMessage = _resourceService.Format("StatusEstimatedRateValidationFormat", MinimumEstimatedRate, MaximumEstimatedRate);
            return null;
        }

        return _estimatedCostPerThousandPostReads;
    }

    public async Task ArchiveSelectedAsync()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = _resourceService.GetString("StatusReviewNoProfile");
            return;
        }

        IsBusy = true;
        try
        {
            ManualArchiveResult result = await _manualArchiveService
                .ArchiveSelectedAsync(SelectedProfile, Posts.Select(post => post.Post).ToList(), CancellationToken.None);

            foreach (ReviewPostItemViewModel post in Posts.Where(post => post.IsSelected))
            {
                post.IsAlreadyArchived = true;
                post.IsSelected = false;
            }

            RefreshSelectionState();
            StatusMessage = _resourceService.Format(
                "StatusReviewArchiveSuccessFormat",
                result.ArchivedPostCount,
                result.DownloadedImageCount,
                result.DownloadedVideoCount,
                result.SkippedAlreadyArchivedCount);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearSelection()
    {
        foreach (ReviewPostItemViewModel post in Posts)
        {
            post.IsSelected = false;
        }

        RefreshSelectionState();
    }

    public string GetArchiveConfirmationText()
    {
        if (SelectedProfile is null)
        {
            return string.Empty;
        }

        int selectedCount = Posts.Count(post => post.IsSelected && !post.IsAlreadyArchived);
        decimal rate = TryGetEstimatedCostRate() ?? _estimatedCostPerThousandPostReads;
        return _reviewCostFormatter.FormatArchiveConfirmation(SelectedProfile, selectedCount, rate);
    }

    public string GetPreviewConfirmationText(bool isLoadMore)
    {
        if (SelectedProfile is null)
        {
            return string.Empty;
        }

        decimal rate = TryGetEstimatedCostRate() ?? _estimatedCostPerThousandPostReads;
        return _reviewCostFormatter.FormatPreviewConfirmation(SelectedProfile, rate, isLoadMore);
    }

    public async Task InitializeAsync(Guid? selectedProfileId)
    {
        AppSettings settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        _estimatedCostPerThousandPostReads = settings.EstimatedCostPerThousandPostReads;

        IReadOnlyList<ArchiveProfile> profiles = await _profileRepository.GetAllAsync(CancellationToken.None);
        Profiles.Clear();
        foreach (ArchiveProfile profile in profiles.OrderBy(profile => profile.Username, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = ResolveSelectedProfile(selectedProfileId);
        if (SelectedProfile is null)
        {
            StatusMessage = _resourceService.GetString("StatusReviewNoProfile");
        }
    }

    public async Task LoadMoreAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(_nextToken))
        {
            return;
        }

        await LoadPageAsync(_nextToken, appendResults: true);
    }

    public async Task LoadRecentPostsAsync()
    {
        ResetReviewSession();

        if (SelectedProfile is null)
        {
            StatusMessage = _resourceService.GetString("StatusReviewNoProfile");
            return;
        }

        await LoadPageAsync(null, appendResults: false);
    }

    public void SelectAllVisible()
    {
        foreach (ReviewPostItemViewModel post in Posts.Where(post => !post.IsAlreadyArchived))
        {
            post.IsSelected = true;
        }

        RefreshSelectionState();
    }

    private async Task LoadPageAsync(string? paginationToken, bool appendResults)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            PreviewPageResult result = await _postReviewService.LoadPageAsync(
                new ApiSyncRequest
                {
                    Profile = SelectedProfile,
                },
                paginationToken,
                CancellationToken.None);

            foreach (PreviewPostRecord post in result.Posts)
            {
                ReviewPostItemViewModel item = new(post);
                item.SelectionStateChanged += OnReviewPostSelectionStateChanged;
                Posts.Add(item);
            }

            _nextToken = result.NextToken;
            if (!appendResults)
            {
                SelectedPost = Posts.FirstOrDefault();
            }

            RefreshSelectionState();
            StatusMessage = _resourceService.Format("StatusReviewLoadedFormat", result.Posts.Count, result.ScannedPostReads);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnReviewPostSelectionStateChanged(object? sender, EventArgs e)
    {
        RefreshSelectionState();
    }

    private void OnSelectedPostChanged(ReviewPostItemViewModel? value)
    {
        SelectedImages.Clear();
        SelectedVideos.Clear();

        if (value is null)
        {
            SelectedPostText = string.Empty;
            SelectedPostCreatedAtText = string.Empty;
            SelectedPostTypeText = string.Empty;
            SelectedPostMetricsSummary = string.Empty;
            OnPropertyChanged(nameof(HasSelectedPost));
            OnPropertyChanged(nameof(HasSelectedImages));
            OnPropertyChanged(nameof(HasSelectedVideos));
            return;
        }

        SelectedPostText = value.Post.Text;
        SelectedPostCreatedAtText = value.Post.CreatedAtUtc.ToLocalTime().ToString("f", System.Globalization.CultureInfo.CurrentCulture);
        SelectedPostTypeText = _resourceService.GetString($"PostType{value.Post.PostType}");
        SelectedPostMetricsSummary = _resourceService.Format(
            "MetricsSummaryFormat",
            value.Post.LikeCount,
            value.Post.ReplyCount,
            value.Post.RepostCount,
            value.Post.QuoteCount);

        foreach (PreviewMediaRecord media in value.Post.Media.Where(media => media.Kind == ArchiveMediaKind.Image))
        {
            SelectedImages.Add(media);
        }

        foreach (PreviewMediaRecord media in value.Post.Media.Where(media => media.Kind == ArchiveMediaKind.Video))
        {
            SelectedVideos.Add(media);
        }

        OnPropertyChanged(nameof(HasSelectedPost));
        OnPropertyChanged(nameof(HasSelectedImages));
        OnPropertyChanged(nameof(HasSelectedVideos));
    }

    private void OnSelectedProfileChanged(ArchiveProfile? profile)
    {
        ResetReviewSession();
        FilterSummaryText = profile is null ? string.Empty : BuildFilterSummary(profile);
        PreviewEstimateText = profile is null
            ? string.Empty
            : _reviewCostFormatter.FormatPreviewEstimate(profile, _estimatedCostPerThousandPostReads);

        OnPropertyChanged(nameof(HasSelectedProfile));
    }

    private void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(CanArchiveSelected));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(CanSelectVisible));
    }

    private void ResetReviewSession()
    {
        foreach (ReviewPostItemViewModel post in Posts)
        {
            post.SelectionStateChanged -= OnReviewPostSelectionStateChanged;
        }

        Posts.Clear();
        SelectedPost = null;
        _nextToken = null;
        RefreshSelectionState();
    }

    private ArchiveProfile? ResolveSelectedProfile(Guid? selectedProfileId)
    {
        if (selectedProfileId is not null)
        {
            ArchiveProfile? matchingProfile = Profiles.FirstOrDefault(profile => profile.ProfileId == selectedProfileId.Value);
            if (matchingProfile is not null)
            {
                return matchingProfile;
            }
        }

        return Profiles.FirstOrDefault();
    }

    private string BuildFilterSummary(ArchiveProfile profile)
    {
        List<string> postTypes = [];
        if (profile.IncludeOriginalPosts)
        {
            postTypes.Add(_resourceService.GetString("DialogSyncPostTypeOriginal"));
        }

        if (profile.IncludeReplies)
        {
            postTypes.Add(_resourceService.GetString("DialogSyncPostTypeReplies"));
        }

        if (profile.IncludeQuotes)
        {
            postTypes.Add(_resourceService.GetString("DialogSyncPostTypeQuotes"));
        }

        if (profile.IncludeReposts)
        {
            postTypes.Add(_resourceService.GetString("DialogSyncPostTypeReposts"));
        }

        List<string> media = [];
        if (profile.DownloadImages)
        {
            media.Add(_resourceService.GetString("DialogSyncMediaImages"));
        }

        if (profile.DownloadVideos)
        {
            media.Add(_resourceService.GetString("DialogSyncMediaVideos"));
        }

        if (media.Count == 0)
        {
            media.Add(_resourceService.GetString("DialogSyncMediaNone"));
        }

        return _resourceService.Format("ReviewFilterSummaryFormat", string.Join(", ", postTypes), string.Join(", ", media));
    }
}
