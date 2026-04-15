using System.Text;
using System.Text.Json;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Utilities;

namespace XArchiver.Core.Services;

public sealed class ArchiveFileWriter : IArchiveFileWriter
{
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IArchiveMetadataBuilder _archiveMetadataBuilder;
    private readonly IMediaDownloader _mediaDownloader;

    public ArchiveFileWriter(IMediaDownloader mediaDownloader, IArchiveMetadataBuilder archiveMetadataBuilder)
    {
        _mediaDownloader = mediaDownloader;
        _archiveMetadataBuilder = archiveMetadataBuilder;
    }

    public async Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
    {
        string profileRoot = ArchivePathBuilder.GetProfileRoot(profile);
        string textRelativePath = ArchivePathBuilder.GetTextRelativePath(post.CreatedAtUtc, post.PostId);
        string metadataRelativePath = ArchivePathBuilder.GetMetadataRelativePath(post.PostId);
        string textPath = Path.Combine(profileRoot, textRelativePath);
        string metadataPath = Path.Combine(profileRoot, metadataRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(textPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);

        await File.WriteAllTextAsync(textPath, post.Text, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        ArchivedPostRecord clonedPost = ClonePost(post);
        clonedPost.ArchivedAtUtc = DateTimeOffset.UtcNow;
        clonedPost.TextRelativePath = textRelativePath;
        clonedPost.MetadataRelativePath = metadataRelativePath;

        foreach (ArchivedMediaRecord media in clonedPost.Media)
        {
            string mediaRelativePath = ArchivePathBuilder.GetMediaRelativePath(media, clonedPost.CreatedAtUtc, clonedPost.PostId);
            string mediaPath = Path.Combine(profileRoot, mediaRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(mediaPath)!);
            await _mediaDownloader.DownloadAsync(new Uri(media.SourceUrl), mediaPath, cancellationToken).ConfigureAwait(false);
            media.RelativePath = mediaRelativePath;
        }

        ArchivedPostMetadataDocument metadataDocument = _archiveMetadataBuilder.Build(clonedPost);
        string metadata = JsonSerializer.Serialize(metadataDocument, MetadataSerializerOptions);
        await File.WriteAllTextAsync(metadataPath, metadata, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return clonedPost;
    }

    private static ArchivedPostRecord ClonePost(ArchivedPostRecord post)
    {
        return new ArchivedPostRecord
        {
            ArchivedAtUtc = post.ArchivedAtUtc,
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
                        RelativePath = media.RelativePath,
                        SourceUrl = media.SourceUrl,
                        Width = media.Width,
                    })
                .ToList(),
            MetadataRelativePath = post.MetadataRelativePath,
            PostId = post.PostId,
            PostType = post.PostType,
            ProfileId = post.ProfileId,
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
            TextRelativePath = post.TextRelativePath,
            UserId = post.UserId,
            Username = post.Username,
        };
    }
}
