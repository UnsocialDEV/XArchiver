using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IWebArchiveRequestFactory
{
    WebArchiveRequest Create(
        ArchiveProfile profile,
        ScraperExecutionMode executionMode,
        DateTimeOffset? archiveStartUtc,
        DateTimeOffset? archiveEndUtc);
}
