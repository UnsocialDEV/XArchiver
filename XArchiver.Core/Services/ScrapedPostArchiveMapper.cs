using System.Text.Json;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ScrapedPostArchiveMapper
{
    public ArchivedPostRecord Map(ArchiveProfile profile, ScrapedPostRecord scrapedPost)
    {
        return new ArchivedPostRecord
        {
            CreatedAtUtc = scrapedPost.CreatedAtUtc,
            Media = scrapedPost.Media
                .Where(media => !string.IsNullOrWhiteSpace(media.SourceUrl))
                .Select(
                    media => new ArchivedMediaRecord
                    {
                        DurationMs = media.DurationMs,
                        IsPartial = media.IsPartial,
                        Kind = media.Kind,
                        MediaKey = media.MediaKey,
                        PostId = scrapedPost.PostId,
                        SourceUrl = media.SourceUrl,
                        Height = media.Height,
                        Width = media.Width,
                    })
                .ToList(),
            PostId = scrapedPost.PostId,
            PostType = ArchivePostType.Original,
            ProfileId = profile.ProfileId,
            RawPayloadJson = JsonSerializer.Serialize(scrapedPost),
            Text = scrapedPost.Text,
            UserId = scrapedPost.Username,
            Username = scrapedPost.Username,
        };
    }
}
