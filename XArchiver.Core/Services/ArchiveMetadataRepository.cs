using System.Text.Json;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ArchiveMetadataRepository : IArchiveMetadataRepository
{
    public async Task<ArchivedPostMetadataReadResult> LoadAsync(string? metadataFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadataFilePath) || !File.Exists(metadataFilePath))
        {
            return new ArchivedPostMetadataReadResult();
        }

        string rawJson = await File.ReadAllTextAsync(metadataFilePath, cancellationToken).ConfigureAwait(false);
        ArchivedPostMetadataDocument? document = TryDeserializeExtended(rawJson);
        bool isExtendedMetadata = document is not null;

        if (document is null)
        {
            ArchivedPostRecord? legacyPost = JsonSerializer.Deserialize<ArchivedPostRecord>(rawJson);
            if (legacyPost is not null)
            {
                document = new ArchivedPostMetadataDocument
                {
                    SchemaVersion = 1,
                    Post = legacyPost,
                };
            }
        }

        if (document is not null)
        {
            HydratePostPaths(document.Post, metadataFilePath);
        }

        return new ArchivedPostMetadataReadResult
        {
            Document = document,
            HasMetadataFile = true,
            IsExtendedMetadata = isExtendedMetadata,
            MetadataFilePath = metadataFilePath,
            RawJson = rawJson,
        };
    }

    private static void HydratePostPaths(ArchivedPostRecord post, string metadataFilePath)
    {
        string metadataDirectory = Path.GetDirectoryName(metadataFilePath) ?? string.Empty;
        string? profileRoot = Directory.GetParent(metadataDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(profileRoot))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(post.MetadataRelativePath) && !Path.IsPathRooted(post.MetadataRelativePath))
        {
            post.MetadataRelativePath = Path.Combine(profileRoot, post.MetadataRelativePath);
        }

        if (!string.IsNullOrWhiteSpace(post.TextRelativePath) && !Path.IsPathRooted(post.TextRelativePath))
        {
            post.TextRelativePath = Path.Combine(profileRoot, post.TextRelativePath);
        }

        foreach (ArchivedMediaRecord media in post.Media)
        {
            if (!string.IsNullOrWhiteSpace(media.RelativePath) && !Path.IsPathRooted(media.RelativePath))
            {
                media.RelativePath = Path.Combine(profileRoot, media.RelativePath);
            }
        }
    }

    private static ArchivedPostMetadataDocument? TryDeserializeExtended(string rawJson)
    {
        using JsonDocument document = JsonDocument.Parse(rawJson);
        if (!document.RootElement.TryGetProperty(nameof(ArchivedPostMetadataDocument.SchemaVersion), out _))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ArchivedPostMetadataDocument>(rawJson);
    }
}
