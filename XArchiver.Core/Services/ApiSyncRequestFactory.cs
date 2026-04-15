using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ApiSyncRequestFactory : IApiSyncRequestFactory
{
    public ApiSyncRequest Create(ArchiveProfile profile, DateTimeOffset? archiveStartUtc, DateTimeOffset? archiveEndUtc)
    {
        return new ApiSyncRequest
        {
            ArchiveEndUtc = archiveEndUtc,
            ArchiveStartUtc = archiveStartUtc,
            Profile = CloneProfile(profile),
        };
    }

    private static ArchiveProfile CloneProfile(ArchiveProfile profile)
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
            MaxPostsPerSync = profile.MaxPostsPerSync,
            MaxPostsPerWebArchive = profile.MaxPostsPerWebArchive,
            PreferredSource = profile.PreferredSource,
            ProfileId = profile.ProfileId,
            ProfileUrl = profile.ProfileUrl,
            UserId = profile.UserId,
            Username = profile.Username,
        };
    }
}
