using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class WebArchiveRequestFactory : IWebArchiveRequestFactory
{
    public WebArchiveRequest Create(
        ArchiveProfile profile,
        ScraperExecutionMode executionMode,
        DateTimeOffset? archiveStartUtc,
        DateTimeOffset? archiveEndUtc)
    {
        return new WebArchiveRequest
        {
            ArchiveEndUtc = archiveEndUtc,
            ArchiveRootPath = profile.ArchiveRootPath,
            ArchiveStartUtc = archiveStartUtc,
            ExecutionMode = executionMode,
            MaxPostsToScrape = profile.MaxPostsPerWebArchive,
            ProfileUrl = profile.ProfileUrl ?? string.Empty,
            Username = profile.Username,
        };
    }
}
