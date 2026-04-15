using Microsoft.Data.Sqlite;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ArchiveImportServiceTests
{
    [TestMethod]
    public async Task ImportAsyncWhenParentContainsMultipleValidArchivesImportsAll()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            await CreateArchiveAsync(tempRoot, "alice", "alice-id", "1");
            await CreateArchiveAsync(tempRoot, "bob", "bob-id", "2");

            ArchiveProfileRepository repository = CreateRepository(tempRoot);
            ArchiveImportService service = new(new ArchiveInspectionService(), repository);

            ArchiveImportResult result = await service.ImportAsync(tempRoot, CancellationToken.None);
            IReadOnlyList<ArchiveProfile> profiles = await repository.GetAllAsync(CancellationToken.None);

            Assert.AreEqual(2, result.ImportedCount);
            Assert.AreEqual(0, result.UpdatedCount);
            Assert.AreEqual(0, result.SkippedInvalidCount);
            Assert.AreEqual(0, result.SkippedDuplicateCount);
            Assert.HasCount(2, profiles);
            Assert.IsTrue(profiles.Any(profile => profile.Username == "alice" && profile.UserId == "alice-id"));
            Assert.IsTrue(profiles.Any(profile => profile.Username == "bob" && profile.UserId == "bob-id"));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [TestMethod]
    public async Task ImportAsyncWhenExistingProfileMatchesArchiveUpdatesInPlace()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            await CreateArchiveAsync(tempRoot, "alice", "resolved-user", "1");
            ArchiveProfileRepository repository = CreateRepository(tempRoot);

            ArchiveProfile existingProfile = new()
            {
                ArchiveRootPath = tempRoot,
                DownloadImages = false,
                DownloadVideos = false,
                IncludeOriginalPosts = false,
                IncludeQuotes = true,
                IncludeReplies = false,
                IncludeReposts = true,
                LastSinceId = "checkpoint",
                LastSuccessfulSyncUtc = DateTimeOffset.UtcNow,
                MaxPostsPerSync = 77,
                ProfileId = Guid.NewGuid(),
                UserId = null,
                Username = "alice",
            };
            await repository.SaveAsync(existingProfile, CancellationToken.None);

            ArchiveImportService service = new(new ArchiveInspectionService(), repository);
            ArchiveImportResult result = await service.ImportAsync(tempRoot, CancellationToken.None);
            IReadOnlyList<ArchiveProfile> profiles = await repository.GetAllAsync(CancellationToken.None);

            Assert.AreEqual(0, result.ImportedCount);
            Assert.AreEqual(1, result.UpdatedCount);
            Assert.HasCount(1, profiles);
            Assert.AreEqual(existingProfile.ProfileId, profiles[0].ProfileId);
            Assert.AreEqual("resolved-user", profiles[0].UserId);
            Assert.AreEqual(77, profiles[0].MaxPostsPerSync);
            Assert.IsFalse(profiles[0].DownloadImages);
            Assert.AreEqual("checkpoint", profiles[0].LastSinceId);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [TestMethod]
    public async Task ImportAsyncWhenCandidatesAreInvalidSkipsThem()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string missingDbArchive = Path.Combine(tempRoot, "missing-db");
            Directory.CreateDirectory(Path.Combine(missingDbArchive, "text"));

            string brokenArchive = Path.Combine(tempRoot, "broken");
            Directory.CreateDirectory(brokenArchive);
            await File.WriteAllTextAsync(Path.Combine(brokenArchive, "archive.db"), "not a sqlite database");

            string wrongSchemaArchive = Path.Combine(tempRoot, "wrong-schema");
            Directory.CreateDirectory(wrongSchemaArchive);
            await CreateWrongSchemaDatabaseAsync(Path.Combine(wrongSchemaArchive, "archive.db"));

            ArchiveProfileRepository repository = CreateRepository(tempRoot);
            ArchiveImportService service = new(new ArchiveInspectionService(), repository);

            ArchiveImportResult result = await service.ImportAsync(tempRoot, CancellationToken.None);
            IReadOnlyList<ArchiveProfile> profiles = await repository.GetAllAsync(CancellationToken.None);

            Assert.AreEqual(0, result.ImportedCount);
            Assert.AreEqual(0, result.UpdatedCount);
            Assert.AreEqual(3, result.SkippedInvalidCount);
            Assert.HasCount(0, profiles);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [TestMethod]
    public async Task ImportAsyncWhenArchiveIsEmptyUsesFolderNameFallback()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            ArchiveProfile emptyArchive = new()
            {
                ArchiveRootPath = tempRoot,
                Username = "empty-user",
            };

            ArchiveIndexRepository repository = new();
            await repository.InitializeAsync(emptyArchive, CancellationToken.None);

            ArchiveProfileRepository profileRepository = CreateRepository(tempRoot);
            ArchiveImportService service = new(new ArchiveInspectionService(), profileRepository);

            ArchiveImportResult result = await service.ImportAsync(tempRoot, CancellationToken.None);
            IReadOnlyList<ArchiveProfile> profiles = await profileRepository.GetAllAsync(CancellationToken.None);

            Assert.AreEqual(1, result.ImportedCount);
            Assert.HasCount(1, profiles);
            Assert.AreEqual("empty-user", profiles[0].Username);
            Assert.IsNull(profiles[0].UserId);
            Assert.AreEqual(100, profiles[0].MaxPostsPerSync);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static ArchiveProfileRepository CreateRepository(string tempRoot)
    {
        return new ArchiveProfileRepository(Path.Combine(tempRoot, "profiles.json"));
    }

    private static string CreateTempRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static async Task CreateArchiveAsync(string archiveRootPath, string username, string userId, string postId)
    {
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = archiveRootPath,
            Username = username,
            UserId = userId,
        };

        ArchiveIndexRepository repository = new();
        await repository.InitializeAsync(profile, CancellationToken.None);
        await repository.UpsertAsync(
            profile,
            new ArchivedPostRecord
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                PostId = postId,
                PostType = ArchivePostType.Original,
                ProfileId = profile.ProfileId,
                Text = $"post {postId}",
                UserId = userId,
                Username = username,
            },
            CancellationToken.None);
    }

    private static async Task CreateWrongSchemaDatabaseAsync(string databasePath)
    {
        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE SomethingElse (Id INTEGER PRIMARY KEY);";
        await command.ExecuteNonQueryAsync();
    }

    private static void DeleteDirectory(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
