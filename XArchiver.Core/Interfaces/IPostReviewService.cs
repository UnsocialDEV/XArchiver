using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IPostReviewService
{
    Task<PreviewPageResult> LoadPageAsync(ApiSyncRequest request, string? paginationToken, CancellationToken cancellationToken);
}
