using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IXApiClient
{
    Task<XUserProfile> GetUserAsync(string username, string bearerToken, CancellationToken cancellationToken);

    Task<PreviewPageResult> GetUserPreviewPostsAsync(
        XUserProfile user,
        string bearerToken,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        string? paginationToken,
        int pageSize,
        CancellationToken cancellationToken);

    Task<XTimelinePage> GetUserPostsAsync(
        XUserProfile user,
        string bearerToken,
        string? sinceId,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        string? paginationToken,
        int pageSize,
        CancellationToken cancellationToken);
}
