using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IApiSyncRequestFactory
{
    ApiSyncRequest Create(ArchiveProfile profile, DateTimeOffset? archiveStartUtc, DateTimeOffset? archiveEndUtc);
}
