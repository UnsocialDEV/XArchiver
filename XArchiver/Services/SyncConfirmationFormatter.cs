using System.Text;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class SyncConfirmationFormatter : ISyncConfirmationFormatter
{
    private readonly IResourceService _resourceService;
    private readonly ISyncCostEstimator _syncCostEstimator;

    public SyncConfirmationFormatter(IResourceService resourceService, ISyncCostEstimator syncCostEstimator)
    {
        _resourceService = resourceService;
        _syncCostEstimator = syncCostEstimator;
    }

    public string FormatBody(ArchiveProfile profile, decimal costPerThousandPostReads)
    {
        SyncCostEstimate estimate = _syncCostEstimator.Estimate(profile, costPerThousandPostReads);
        StringBuilder builder = new();
        builder.AppendLine(_resourceService.Format("DialogSyncSummaryUsernameFormat", profile.Username));
        builder.AppendLine(_resourceService.Format("DialogSyncSummaryFolderFormat", profile.ArchiveRootPath));
        builder.AppendLine(_resourceService.Format("DialogSyncSummaryMaxPostsFormat", profile.MaxPostsPerSync));
        builder.AppendLine(_resourceService.Format("DialogSyncSummaryPostTypesFormat", GetSelectedPostTypes(profile)));
        builder.AppendLine(_resourceService.Format("DialogSyncSummaryMediaFormat", GetSelectedMedia(profile)));
        builder.AppendLine(
            _resourceService.Format(
                "DialogSyncSummaryEstimatedCostFormat",
                estimate.BaselineEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
                estimate.BaselineEstimatedPostReads,
                estimate.HighScanEstimatedCost.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
                estimate.HighScanEstimatedPostReads));
        builder.AppendLine(
            _resourceService.Format(
                "DialogSyncSummaryEstimatedRateFormat",
                estimate.AssumedRatePerThousandPostReads.ToString("F2", System.Globalization.CultureInfo.CurrentCulture)));
        builder.AppendLine(_resourceService.GetString(profile.LastSinceId is null ? "DialogSyncSummaryModeInitial" : "DialogSyncSummaryModeIncremental"));
        builder.AppendLine();
        builder.AppendLine(_resourceService.GetString("DialogSyncSummaryUsageNote"));
        builder.AppendLine(_resourceService.GetString("DialogSyncSummaryQueueNote"));
        builder.AppendLine(_resourceService.GetString("DialogSyncSummaryApiCallsNote"));
        builder.AppendLine(_resourceService.GetString("DialogSyncSummaryMediaNote"));
        return builder.ToString().Trim();
    }

    private string GetSelectedMedia(ArchiveProfile profile)
    {
        List<string> selections = [];

        if (profile.DownloadImages)
        {
            selections.Add(_resourceService.GetString("DialogSyncMediaImages"));
        }

        if (profile.DownloadVideos)
        {
            selections.Add(_resourceService.GetString("DialogSyncMediaVideos"));
        }

        return selections.Count == 0
            ? _resourceService.GetString("DialogSyncMediaNone")
            : string.Join(", ", selections);
    }

    private string GetSelectedPostTypes(ArchiveProfile profile)
    {
        List<string> selections = [];

        if (profile.IncludeOriginalPosts)
        {
            selections.Add(_resourceService.GetString("DialogSyncPostTypeOriginal"));
        }

        if (profile.IncludeReplies)
        {
            selections.Add(_resourceService.GetString("DialogSyncPostTypeReplies"));
        }

        if (profile.IncludeQuotes)
        {
            selections.Add(_resourceService.GetString("DialogSyncPostTypeQuotes"));
        }

        if (profile.IncludeReposts)
        {
            selections.Add(_resourceService.GetString("DialogSyncPostTypeReposts"));
        }

        return string.Join(", ", selections);
    }
}
