using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ArchiveImportService : IArchiveImportService
{
    private static readonly string[] ArchiveContentDirectories = ["images", "metadata", "text", "videos"];

    private readonly IArchiveInspectionService _archiveInspectionService;
    private readonly IArchiveProfileRepository _archiveProfileRepository;

    public ArchiveImportService(
        IArchiveInspectionService archiveInspectionService,
        IArchiveProfileRepository archiveProfileRepository)
    {
        _archiveInspectionService = archiveInspectionService;
        _archiveProfileRepository = archiveProfileRepository;
    }

    public async Task<ArchiveImportResult> ImportAsync(string parentFolderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentFolderPath) || !Directory.Exists(parentFolderPath))
        {
            return new ArchiveImportResult();
        }

        List<ArchiveProfile> existingProfiles = (await _archiveProfileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        Dictionary<string, ArchiveProfile> profilesByKey = existingProfiles.ToDictionary(CreateKey, StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> existingProfileIds = existingProfiles.Select(profile => profile.ProfileId).ToHashSet();
        HashSet<string> seenDiscoveredKeys = new(StringComparer.OrdinalIgnoreCase);
        List<ArchiveProfile> importedProfiles = [];

        int importedCount = 0;
        int skippedDuplicateCount = 0;
        int skippedInvalidCount = 0;
        int updatedCount = 0;

        foreach (string candidateFolderPath in EnumerateCandidateArchiveFolders(parentFolderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(Path.Combine(candidateFolderPath, "archive.db")))
            {
                skippedInvalidCount++;
                continue;
            }

            DiscoveredArchiveRecord? discoveredArchive = await _archiveInspectionService
                .InspectAsync(candidateFolderPath, cancellationToken)
                .ConfigureAwait(false);

            if (discoveredArchive is null)
            {
                skippedInvalidCount++;
                continue;
            }

            string discoveredKey = CreateKey(discoveredArchive.ArchiveRootPath, discoveredArchive.Username);
            if (!seenDiscoveredKeys.Add(discoveredKey))
            {
                skippedDuplicateCount++;
                continue;
            }

            if (profilesByKey.TryGetValue(discoveredKey, out ArchiveProfile? existingProfile))
            {
                ArchiveProfile updatedProfile = UpdateExistingProfile(existingProfile, discoveredArchive);
                await _archiveProfileRepository.SaveAsync(updatedProfile, cancellationToken).ConfigureAwait(false);
                profilesByKey[discoveredKey] = updatedProfile;
                importedProfiles.Add(updatedProfile);
                updatedCount++;
                continue;
            }

            ArchiveProfile importedProfile = CreateImportedProfile(discoveredArchive, existingProfileIds);
            await _archiveProfileRepository.SaveAsync(importedProfile, cancellationToken).ConfigureAwait(false);
            profilesByKey[discoveredKey] = importedProfile;
            existingProfileIds.Add(importedProfile.ProfileId);
            importedProfiles.Add(importedProfile);
            importedCount++;
        }

        return new ArchiveImportResult
        {
            ImportedCount = importedCount,
            ImportedProfiles = importedProfiles,
            SkippedDuplicateCount = skippedDuplicateCount,
            SkippedInvalidCount = skippedInvalidCount,
            UpdatedCount = updatedCount,
        };
    }

    private static ArchiveProfile CreateImportedProfile(DiscoveredArchiveRecord archive, HashSet<Guid> existingProfileIds)
    {
        Guid profileId = archive.ProfileId is Guid storedProfileId && !existingProfileIds.Contains(storedProfileId)
            ? storedProfileId
            : Guid.NewGuid();

        return new ArchiveProfile
        {
            ArchiveRootPath = archive.ArchiveRootPath,
            DownloadImages = true,
            DownloadVideos = true,
            IncludeOriginalPosts = true,
            IncludeQuotes = true,
            IncludeReplies = true,
            IncludeReposts = true,
            LastSinceId = null,
            LastSuccessfulSyncUtc = null,
            MaxPostsPerWebArchive = 100,
            MaxPostsPerSync = 100,
            PreferredSource = ArchiveSourceKind.Api,
            ProfileId = profileId,
            ProfileUrl = $"https://x.com/{archive.Username}",
            UserId = archive.UserId,
            Username = archive.Username,
        };
    }

    private static string CreateKey(ArchiveProfile profile)
    {
        return CreateKey(profile.ArchiveRootPath, profile.Username);
    }

    private static string CreateKey(string archiveRootPath, string username)
    {
        string normalizedArchiveRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(archiveRootPath));
        return $"{normalizedArchiveRoot}|{username.Trim()}";
    }

    private static IEnumerable<string> EnumerateCandidateArchiveFolders(string parentFolderPath)
    {
        Queue<string> pendingDirectories = new();
        pendingDirectories.Enqueue(Path.GetFullPath(parentFolderPath));

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Dequeue();
            IEnumerable<string> childDirectories;

            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string childDirectory in childDirectories)
            {
                pendingDirectories.Enqueue(childDirectory);
            }

            bool hasArchiveDatabase = File.Exists(Path.Combine(currentDirectory, "archive.db"));
            bool hasArchiveContentDirectory = ArchiveContentDirectories.Any(
                directoryName => Directory.Exists(Path.Combine(currentDirectory, directoryName)));

            if (hasArchiveDatabase || hasArchiveContentDirectory)
            {
                yield return currentDirectory;
            }
        }
    }

    private static ArchiveProfile UpdateExistingProfile(ArchiveProfile existingProfile, DiscoveredArchiveRecord archive)
    {
        return new ArchiveProfile
        {
            ArchiveRootPath = archive.ArchiveRootPath,
            DownloadImages = existingProfile.DownloadImages,
            DownloadVideos = existingProfile.DownloadVideos,
            IncludeOriginalPosts = existingProfile.IncludeOriginalPosts,
            IncludeQuotes = existingProfile.IncludeQuotes,
            IncludeReplies = existingProfile.IncludeReplies,
            IncludeReposts = existingProfile.IncludeReposts,
            LastSinceId = existingProfile.LastSinceId,
            LastSuccessfulSyncUtc = existingProfile.LastSuccessfulSyncUtc,
            MaxPostsPerWebArchive = existingProfile.MaxPostsPerWebArchive,
            MaxPostsPerSync = existingProfile.MaxPostsPerSync,
            PreferredSource = existingProfile.PreferredSource,
            ProfileId = existingProfile.ProfileId,
            ProfileUrl = existingProfile.ProfileUrl ?? $"https://x.com/{archive.Username}",
            UserId = archive.UserId ?? existingProfile.UserId,
            Username = archive.Username,
        };
    }
}
