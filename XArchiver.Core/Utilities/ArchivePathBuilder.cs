using System.Globalization;
using XArchiver.Core.Models;

namespace XArchiver.Core.Utilities;

public static class ArchivePathBuilder
{
    public static string GetDatabasePath(ArchiveProfile profile)
    {
        return Path.Combine(GetProfileRoot(profile), "archive.db");
    }

    public static string GetMediaRelativePath(ArchivedMediaRecord media, DateTimeOffset createdAtUtc, string postId)
    {
        string directory = media.Kind == ArchiveMediaKind.Image ? "images" : "videos";
        string extension = GetMediaExtension(media);
        return Path.Combine(
            directory,
            createdAtUtc.ToString("yyyy", CultureInfo.InvariantCulture),
            createdAtUtc.ToString("MM", CultureInfo.InvariantCulture),
            $"{FileNameSanitizer.SanitizeSegment(postId)}_{FileNameSanitizer.SanitizeSegment(media.MediaKey)}{extension}");
    }

    public static string GetMetadataRelativePath(string postId)
    {
        return Path.Combine("metadata", $"{FileNameSanitizer.SanitizeSegment(postId)}.json");
    }

    public static string GetProfileRoot(ArchiveProfile profile)
    {
        return Path.Combine(profile.ArchiveRootPath, FileNameSanitizer.SanitizeSegment(profile.Username));
    }

    public static string GetTextRelativePath(DateTimeOffset createdAtUtc, string postId)
    {
        string timestamp = createdAtUtc.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        return Path.Combine(
            "text",
            createdAtUtc.ToString("yyyy", CultureInfo.InvariantCulture),
            createdAtUtc.ToString("MM", CultureInfo.InvariantCulture),
            $"{timestamp}_{FileNameSanitizer.SanitizeSegment(postId)}.txt");
    }

    private static string GetMediaExtension(ArchivedMediaRecord media)
    {
        if (Uri.TryCreate(media.SourceUrl, UriKind.Absolute, out Uri? sourceUri))
        {
            string extension = Path.GetExtension(sourceUri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }
        }

        return media.Kind == ArchiveMediaKind.Image ? ".jpg" : ".mp4";
    }
}
