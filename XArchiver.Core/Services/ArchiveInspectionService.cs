using System.Globalization;
using Microsoft.Data.Sqlite;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ArchiveInspectionService : IArchiveInspectionService
{
    public async Task<DiscoveredArchiveRecord?> InspectAsync(string archiveFolderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(archiveFolderPath) || !Directory.Exists(archiveFolderPath))
        {
            return null;
        }

        string databasePath = Path.Combine(archiveFolderPath, "archive.db");
        if (!File.Exists(databasePath))
        {
            return null;
        }

        DirectoryInfo? archiveFolder = Directory.GetParent(databasePath);
        DirectoryInfo? archiveRoot = archiveFolder?.Parent;
        if (archiveFolder is null || archiveRoot is null)
        {
            return null;
        }

        try
        {
            await using SqliteConnection connection = new($"Data Source={databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await HasPostsTableAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            (int archivedPostCount, DateTimeOffset? latestArchivedPostUtc) = await ReadArchiveSummaryAsync(connection, cancellationToken).ConfigureAwait(false);
            (string? username, string? userId, Guid? profileId) = await ReadArchiveIdentityAsync(connection, cancellationToken).ConfigureAwait(false);

            return new DiscoveredArchiveRecord
            {
                ArchivedPostCount = archivedPostCount,
                ArchiveFolderPath = archiveFolder.FullName,
                ArchiveRootPath = archiveRoot.FullName,
                LatestArchivedPostUtc = latestArchivedPostUtc,
                ProfileId = profileId,
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                Username = string.IsNullOrWhiteSpace(username) ? archiveFolder.Name : username,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or FormatException)
        {
            return null;
        }
    }

    private static async Task<bool> HasPostsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Posts' LIMIT 1;";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is not null;
    }

    private static async Task<(string? Username, string? UserId, Guid? ProfileId)> ReadArchiveIdentityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT Username, UserId, ProfileId
            FROM Posts
            WHERE Username IS NOT NULL AND Username <> ''
            ORDER BY CreatedAtUtc DESC
            LIMIT 1;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (null, null, null);
        }

        Guid? profileId = null;
        if (!reader.IsDBNull(2))
        {
            string profileIdText = reader.GetString(2);
            if (!string.IsNullOrWhiteSpace(profileIdText) && Guid.TryParse(profileIdText, out Guid parsedProfileId))
            {
                profileId = parsedProfileId;
            }
        }

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            profileId);
    }

    private static async Task<(int ArchivedPostCount, DateTimeOffset? LatestArchivedPostUtc)> ReadArchiveSummaryAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), MAX(CreatedAtUtc) FROM Posts;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0, null);
        }

        DateTimeOffset? latestArchivedPostUtc = null;
        if (!reader.IsDBNull(1))
        {
            string latestCreatedText = reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(latestCreatedText))
            {
                latestArchivedPostUtc = DateTimeOffset.Parse(latestCreatedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
        }

        return (reader.GetInt32(0), latestArchivedPostUtc);
    }
}
