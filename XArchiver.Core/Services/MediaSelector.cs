using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class MediaSelector : IMediaSelector
{
    public IReadOnlyList<ArchivedMediaRecord> SelectMedia(string postId, IReadOnlyList<XMediaDefinition> mediaDefinitions)
    {
        List<ArchivedMediaRecord> selectedMedia = [];

        foreach (XMediaDefinition definition in mediaDefinitions)
        {
            ArchivedMediaRecord? selectedRecord = definition.Type switch
            {
                "photo" => CreateImage(postId, definition),
                "animated_gif" => CreateVideo(postId, definition),
                "video" => CreateVideo(postId, definition),
                _ => null,
            };

            if (selectedRecord is not null)
            {
                selectedMedia.Add(selectedRecord);
            }
        }

        return selectedMedia;
    }

    private static ArchivedMediaRecord? CreateImage(string postId, XMediaDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Url))
        {
            return null;
        }

        return new ArchivedMediaRecord
        {
            Height = definition.Height,
            Kind = ArchiveMediaKind.Image,
            MediaKey = definition.MediaKey,
            PostId = postId,
            SourceUrl = definition.Url,
            Width = definition.Width,
        };
    }

    private static ArchivedMediaRecord? CreateVideo(string postId, XMediaDefinition definition)
    {
        XMediaVariant? selectedVariant = definition.Variants
            .Where(variant => string.Equals(variant.ContentType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(variant => variant.BitRate ?? 0)
            .FirstOrDefault();

        if (selectedVariant is not null)
        {
            return new ArchivedMediaRecord
            {
                DurationMs = definition.DurationMs,
                Height = definition.Height,
                Kind = ArchiveMediaKind.Video,
                MediaKey = definition.MediaKey,
                PostId = postId,
                SourceUrl = selectedVariant.Url,
                Width = definition.Width,
            };
        }

        if (string.IsNullOrWhiteSpace(definition.PreviewImageUrl))
        {
            return null;
        }

        return new ArchivedMediaRecord
        {
            DurationMs = definition.DurationMs,
            Height = definition.Height,
            IsPartial = true,
            Kind = ArchiveMediaKind.Video,
            MediaKey = definition.MediaKey,
            PostId = postId,
            SourceUrl = definition.PreviewImageUrl,
            Width = definition.Width,
        };
    }
}
