using System.Text.Json;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ArchiveProfileRepository : IArchiveProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _storagePath;

    public ArchiveProfileRepository(string storagePath)
    {
        _storagePath = storagePath;
    }

    public async Task<IReadOnlyList<ArchiveProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(_storagePath);
        List<ArchiveProfile>? profiles = await JsonSerializer.DeserializeAsync<List<ArchiveProfile>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return profiles ?? [];
    }

    public async Task SaveAsync(ArchiveProfile profile, CancellationToken cancellationToken)
    {
        List<ArchiveProfile> profiles = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        int existingIndex = profiles.FindIndex(existingProfile => existingProfile.ProfileId == profile.ProfileId);
        if (existingIndex >= 0)
        {
            profiles[existingIndex] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        await SaveProfilesAsync(profiles, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken)
    {
        List<ArchiveProfile> profiles = (await GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(profile => profile.ProfileId != profileId)
            .ToList();

        await SaveProfilesAsync(profiles, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveProfilesAsync(List<ArchiveProfile> profiles, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        await using FileStream stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
