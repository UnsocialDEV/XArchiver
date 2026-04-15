using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ManualArchiveService : IManualArchiveService
{
    private readonly IArchiveFileWriter _archiveFileWriter;
    private readonly IArchiveIndexRepository _archiveIndexRepository;

    public ManualArchiveService(
        IArchiveFileWriter archiveFileWriter,
        IArchiveIndexRepository archiveIndexRepository)
    {
        _archiveFileWriter = archiveFileWriter;
        _archiveIndexRepository = archiveIndexRepository;
    }

    public async Task<ManualArchiveResult> ArchiveSelectedAsync(
        ArchiveProfile profile,
        IReadOnlyList<PreviewPostRecord> posts,
        CancellationToken cancellationToken)
    {
        await _archiveIndexRepository.InitializeAsync(profile, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PreviewPostRecord> selectedPosts = posts
            .Where(post => post.IsSelected)
            .GroupBy(post => post.PostId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        IReadOnlySet<string> archivedPostIds = await _archiveIndexRepository
            .GetArchivedPostIdsAsync(profile, selectedPosts.Select(post => post.PostId).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        int archivedPostCount = 0;
        int downloadedImageCount = 0;
        int downloadedVideoCount = 0;
        int skippedAlreadyArchivedCount = 0;

        foreach (PreviewPostRecord post in selectedPosts)
        {
            if (post.IsAlreadyArchived || archivedPostIds.Contains(post.PostId))
            {
                skippedAlreadyArchivedCount++;
                continue;
            }

            ArchivedPostRecord archivedPost = await _archiveFileWriter
                .WriteAsync(profile, MapToArchivedPost(profile, post), cancellationToken)
                .ConfigureAwait(false);

            await _archiveIndexRepository.UpsertAsync(profile, archivedPost, cancellationToken).ConfigureAwait(false);

            downloadedImageCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Image);
            downloadedVideoCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Video);
            archivedPostCount++;
        }

        return new ManualArchiveResult
        {
            ArchivedPostCount = archivedPostCount,
            DownloadedImageCount = downloadedImageCount,
            DownloadedVideoCount = downloadedVideoCount,
            SkippedAlreadyArchivedCount = skippedAlreadyArchivedCount,
        };
    }

    private static ArchivedPostRecord MapToArchivedPost(ArchiveProfile profile, PreviewPostRecord post)
    {
        return new ArchivedPostRecord
        {
            ArchivedAtUtc = null,
            ConversationId = post.ConversationId,
            CreatedAtUtc = post.CreatedAtUtc,
            InReplyToUserId = post.InReplyToUserId,
            LikeCount = post.LikeCount,
            MediaDetails = post.MediaDetails
                .Select(
                    detail => new ArchivedMediaDetailRecord
                    {
                        DurationMs = detail.DurationMs,
                        Height = detail.Height,
                        MediaKey = detail.MediaKey,
                        MediaType = detail.MediaType,
                        PreviewImageUrl = detail.PreviewImageUrl,
                        Url = detail.Url,
                        Variants = detail.Variants
                            .Select(
                                variant => new ArchivedMediaVariantRecord
                                {
                                    BitRate = variant.BitRate,
                                    ContentType = variant.ContentType,
                                    Url = variant.Url,
                                })
                            .ToList(),
                        Width = detail.Width,
                    })
                .ToList(),
            Media = post.Media
                .Select(
                    media => new ArchivedMediaRecord
                    {
                        DurationMs = media.DurationMs,
                        Height = media.Height,
                        IsPartial = media.IsPartial,
                        Kind = media.Kind,
                        MediaKey = media.MediaKey,
                        PostId = media.PostId,
                        SourceUrl = media.SourceUrl,
                        Width = media.Width,
                    })
                .ToList(),
            PostId = post.PostId,
            PostType = post.PostType,
            ProfileId = profile.ProfileId,
            QuoteCount = post.QuoteCount,
            RawPayloadJson = post.RawPayloadJson,
            ReferencedPosts = post.ReferencedPosts
                .Select(
                    reference => new ArchivedReferencedPostRecord
                    {
                        ReferenceType = reference.ReferenceType,
                        ReferencedPostId = reference.ReferencedPostId,
                    })
                .ToList(),
            ReplyCount = post.ReplyCount,
            RepostCount = post.RepostCount,
            Text = post.Text,
            UserId = post.UserId,
            Username = post.Username,
        };
    }
}
