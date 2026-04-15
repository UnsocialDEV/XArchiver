using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ProfileReviewWorkspaceViewModel : ObservableObject
{
    private const double MaximumEstimatedRate = 1000;
    private const double MinimumEstimatedRate = 0.01;

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IManualArchiveService _manualArchiveService;
    private readonly IPostReviewService _postReviewService;
    private readonly IResourceService _resourceService;
    private readonly IReviewCostFormatter _reviewCostFormatter;
    private ApiSyncRequest? _activeRequest;
    private decimal _estimatedCostPerThousandPostReads = 5.02m;
    private string _filterSummaryText = string.Empty;
    private bool _isBusy;
    private string? _nextToken;
    private string _previewEstimateText = string.Empty;
    private ReviewPostItemViewModel? _selectedPost;
    private string _selectedPostCreatedAtText = string.Empty;
    private string _selectedPostMetricsSummary = string.Empty;
    private string _selectedPostText = string.Empty;
    private string _selectedPostTypeText = string.Empty;
    private string _statusMessage = string.Empty;

    public ProfileReviewWorkspaceViewModel(
        IAppSettingsRepository appSettingsRepository,
        IPostReviewService postReviewService,
        IManualArchiveService manualArchiveService,
        IReviewCostFormatter reviewCostFormatter,
        IResourceService resourceService)
    {
        _appSettingsRepository = appSettingsRepository;
        _postReviewService = postReviewService;
        _manualArchiveService = manualArchiveService;
        _reviewCostFormatter = reviewCostFormatter;
        _resourceService = resourceService;
    }

    public ApiSyncRequest? ActiveRequest => _activeRequest;

    public bool CanArchiveSelected => Posts.Any(post => post.IsSelected && !post.IsAlreadyArchived);

    public bool CanLoadMore => !string.IsNullOrWhiteSpace(_nextToken);

    public bool CanSelectVisible => Posts.Any(post => !post.IsAlreadyArchived);

    public string FilterSummaryText
    {
        get => _filterSummaryText;
        private set => SetProperty(ref _filterSummaryText, value);
    }

    public bool HasSelectedImages => SelectedImages.Count > 0;

    public bool HasSelectedPost => SelectedPost is not null;

    public bool HasSelectedVideos => SelectedVideos.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<ReviewPostItemViewModel> Posts { get; } = [];

    public string PreviewEstimateText
    {
        get => _previewEstimateText;
        private set => SetProperty(ref _previewEstimateText, value);
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
        private set => SetProperty(ref _selectedPostCreatedAtText, value);
    }

    public string SelectedPostMetricsSummary
    {
        get => _selectedPostMetricsSummary;
        private set => SetProperty(ref _selectedPostMetricsSummary, value);
    }

    public string SelectedPostText
    {
        get => _selectedPostText;
        private set => SetProperty(ref _selectedPostText, value);
    }

    public string SelectedPostTypeText
    {
        get => _selectedPostTypeText;
        private set => SetProperty(ref _selectedPostTypeText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<PreviewMediaRecord> SelectedVideos { get; } = [];

    public async Task SetRequestAsync(ApiSyncRequest? request)
    {
        _activeRequest = request;
        ResetReviewSession();

        if (request is null)
        {
            StatusMessage = _resourceService.GetString("StatusReviewNoProfile");
            return;
        }

        AppSettings settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        _estimatedCostPerThousandPostReads = settings.EstimatedCostPerThousandPostReads;
        ArchiveProfile profile = request.Profile;
        FilterSummaryText = BuildFilterSummary(profile);
        PreviewEstimateText = _reviewCostFormatter.FormatPreviewEstimate(profile, _estimatedCostPerThousandPostReads);
        StatusMessage = string.Empty;
    }

    public async Task ArchiveSelectedAsync()
    {
        if (_activeRequest is null)
        {
            StatusMessage = _resourceService.GetString("StatusReviewNoProfile");
            return;
        }

        IsBusy = true;
        try
        {
            ManualArchiveResult result = await _manualArchiveService
                .ArchiveSelectedAsync(_activeRequest.Profile, Posts.Select(post => post.Post).ToList(), CancellationToken.None);

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
        if (_activeRequest is null)
        {
            return string.Empty;
        }

        int selectedCount = Posts.Count(post => post.IsSelected && !post.IsAlreadyArchived);
        decimal rate = TryGetEstimatedCostRate() ?? _estimatedCostPerThousandPostReads;
        return _reviewCostFormatter.FormatArchiveConfirmation(_activeRequest.Profile, selectedCount, rate);
    }

    public string GetPreviewConfirmationText(bool isLoadMore)
    {
        if (_activeRequest is null)
        {
            return string.Empty;
        }

        decimal rate = TryGetEstimatedCostRate() ?? _estimatedCostPerThousandPostReads;
        return _reviewCostFormatter.FormatPreviewConfirmation(_activeRequest.Profile, rate, isLoadMore);
    }

    public async Task LoadMoreAsync()
    {
        if (_activeRequest is null || string.IsNullOrWhiteSpace(_nextToken))
        {
            return;
        }

        await LoadPageAsync(_nextToken, appendResults: true);
    }

    public async Task LoadRecentPostsAsync()
    {
        ResetReviewSession();

        if (_activeRequest is null)
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

    private static string BuildFilterSummary(ArchiveProfile profile)
    {
        List<string> postTypes = [];
        if (profile.IncludeOriginalPosts)
        {
            postTypes.Add("original posts");
        }

        if (profile.IncludeReplies)
        {
            postTypes.Add("replies");
        }

        if (profile.IncludeQuotes)
        {
            postTypes.Add("quote posts");
        }

        if (profile.IncludeReposts)
        {
            postTypes.Add("reposts");
        }

        List<string> media = [];
        if (profile.DownloadImages)
        {
            media.Add("images");
        }

        if (profile.DownloadVideos)
        {
            media.Add("videos");
        }

        string postTypeText = postTypes.Count == 0 ? "none" : string.Join(", ", postTypes);
        string mediaText = media.Count == 0 ? "none" : string.Join(", ", media);
        return $"Post types: {postTypeText}. Media kept: {mediaText}.";
    }

    private async Task LoadPageAsync(string? paginationToken, bool appendResults)
    {
        if (_activeRequest is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            PreviewPageResult page = await _postReviewService.LoadPageAsync(_activeRequest, paginationToken, CancellationToken.None);

            if (!appendResults)
            {
                Posts.Clear();
            }

            foreach (PreviewPostRecord post in page.Posts)
            {
                ReviewPostItemViewModel postViewModel = new(post);
                postViewModel.SelectionStateChanged += OnPostSelectionStateChanged;
                Posts.Add(postViewModel);
            }

            _nextToken = page.NextToken;
            PreviewEstimateText = _reviewCostFormatter.FormatPreviewEstimate(_activeRequest.Profile, _estimatedCostPerThousandPostReads);
            StatusMessage = _resourceService.Format("StatusReviewLoadedFormat", page.Posts.Count, page.ScannedPostReads);
            OnPropertyChanged(nameof(CanLoadMore));
            RefreshSelectionState();
            SelectedPost = Posts.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPostSelectionStateChanged(object? sender, EventArgs e)
    {
        RefreshSelectionState();
    }

    private void OnSelectedPostChanged(ReviewPostItemViewModel? value)
    {
        SelectedImages.Clear();
        SelectedVideos.Clear();

        if (value is null)
        {
            SelectedPostCreatedAtText = string.Empty;
            SelectedPostMetricsSummary = string.Empty;
            SelectedPostText = string.Empty;
            SelectedPostTypeText = string.Empty;
            OnPropertyChanged(nameof(HasSelectedImages));
            OnPropertyChanged(nameof(HasSelectedPost));
            OnPropertyChanged(nameof(HasSelectedVideos));
            return;
        }

        SelectedPostCreatedAtText = value.CreatedAtText;
        SelectedPostMetricsSummary = _resourceService.Format(
            "MetricsSummaryFormat",
            value.Post.LikeCount,
            value.Post.ReplyCount,
            value.Post.RepostCount,
            value.Post.QuoteCount);
        SelectedPostText = value.Post.Text;
        SelectedPostTypeText = value.PostTypeText;

        foreach (PreviewMediaRecord image in value.Post.Media.Where(media => media.Kind == ArchiveMediaKind.Image))
        {
            SelectedImages.Add(image);
        }

        foreach (PreviewMediaRecord video in value.Post.Media.Where(media => media.Kind == ArchiveMediaKind.Video))
        {
            SelectedVideos.Add(video);
        }

        OnPropertyChanged(nameof(HasSelectedImages));
        OnPropertyChanged(nameof(HasSelectedPost));
        OnPropertyChanged(nameof(HasSelectedVideos));
    }

    private void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(CanArchiveSelected));
        OnPropertyChanged(nameof(CanSelectVisible));
    }

    private void ResetReviewSession()
    {
        foreach (ReviewPostItemViewModel post in Posts)
        {
            post.SelectionStateChanged -= OnPostSelectionStateChanged;
        }

        Posts.Clear();
        SelectedPost = null;
        _nextToken = null;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(CanArchiveSelected));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(CanSelectVisible));
    }

    private decimal? TryGetEstimatedCostRate()
    {
        if (_estimatedCostPerThousandPostReads < (decimal)MinimumEstimatedRate ||
            _estimatedCostPerThousandPostReads > (decimal)MaximumEstimatedRate)
        {
            StatusMessage = _resourceService.Format("StatusEstimatedRateValidationFormat", MinimumEstimatedRate, MaximumEstimatedRate);
            return null;
        }

        return _estimatedCostPerThousandPostReads;
    }
}
