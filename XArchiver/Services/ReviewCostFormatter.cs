using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ReviewCostFormatter : IReviewCostFormatter
{
    private const int PreviewPageSize = 100;

    private readonly IResourceService _resourceService;
    private readonly ISyncCostEstimator _syncCostEstimator;

    public ReviewCostFormatter(IResourceService resourceService, ISyncCostEstimator syncCostEstimator)
    {
        _resourceService = resourceService;
        _syncCostEstimator = syncCostEstimator;
    }

    public string FormatArchiveConfirmation(ArchiveProfile profile, int selectedPostCount, decimal costPerThousandPostReads)
    {
        SyncCostEstimate estimate = _syncCostEstimator.Estimate(CreateEstimateProfile(profile, Math.Max(1, selectedPostCount)), costPerThousandPostReads);
        return _resourceService.Format(
            "DialogReviewArchiveConfirmationFormat",
            selectedPostCount,
            estimate.BaselineEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
            estimate.HighScanEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture));
    }

    public string FormatPreviewConfirmation(ArchiveProfile profile, decimal costPerThousandPostReads, bool isLoadMore)
    {
        SyncCostEstimate estimate = _syncCostEstimator.Estimate(CreateEstimateProfile(profile, PreviewPageSize), costPerThousandPostReads);
        return _resourceService.Format(
            isLoadMore ? "DialogReviewLoadMoreConfirmationFormat" : "DialogReviewLoadConfirmationFormat",
            profile.Username,
            estimate.BaselineEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
            estimate.HighScanEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture));
    }

    public string FormatPreviewEstimate(ArchiveProfile profile, decimal costPerThousandPostReads)
    {
        SyncCostEstimate estimate = _syncCostEstimator.Estimate(CreateEstimateProfile(profile, PreviewPageSize), costPerThousandPostReads);
        return _resourceService.Format(
            "ReviewPreviewEstimateFormat",
            estimate.BaselineEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
            estimate.HighScanEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture));
    }

    private static ArchiveProfile CreateEstimateProfile(ArchiveProfile profile, int maxPostsPerSync)
    {
        return new ArchiveProfile
        {
            ArchiveRootPath = profile.ArchiveRootPath,
            DownloadImages = profile.DownloadImages,
            DownloadVideos = profile.DownloadVideos,
            IncludeOriginalPosts = profile.IncludeOriginalPosts,
            IncludeQuotes = profile.IncludeQuotes,
            IncludeReplies = profile.IncludeReplies,
            IncludeReposts = profile.IncludeReposts,
            LastSinceId = profile.LastSinceId,
            LastSuccessfulSyncUtc = profile.LastSuccessfulSyncUtc,
            MaxPostsPerWebArchive = profile.MaxPostsPerWebArchive,
            MaxPostsPerSync = maxPostsPerSync,
            PreferredSource = profile.PreferredSource,
            ProfileId = profile.ProfileId,
            ProfileUrl = profile.ProfileUrl,
            UserId = profile.UserId,
            Username = profile.Username,
        };
    }
}
