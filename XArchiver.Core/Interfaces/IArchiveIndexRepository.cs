using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveIndexRepository
{
    Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(
        ArchiveProfile profile,
        IReadOnlyCollection<string> postIds,
        CancellationToken cancellationToken);

    Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken);

    Task InitializeAsync(ArchiveProfile profile, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArchivedGalleryMediaRecord>> QueryGalleryMediaAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken);

    Task UpsertAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArchivedPostRecord>> QueryAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken);
}
