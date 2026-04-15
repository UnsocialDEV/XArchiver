using XArchiver.Core.Models;

namespace XArchiver.Services;

public interface IReviewCostFormatter
{
    string FormatArchiveConfirmation(ArchiveProfile profile, int selectedPostCount, decimal costPerThousandPostReads);

    string FormatPreviewConfirmation(ArchiveProfile profile, decimal costPerThousandPostReads, bool isLoadMore);

    string FormatPreviewEstimate(ArchiveProfile profile, decimal costPerThousandPostReads);
}
