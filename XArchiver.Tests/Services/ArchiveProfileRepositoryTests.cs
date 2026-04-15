using System.Text.Json;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ArchiveProfileRepositoryTests
{
    [TestMethod]
    public async Task GetAllAsyncWhenLegacyProfileJsonIsLoadedAppliesNewSourceDefaults()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xarchiver-tests", Guid.NewGuid().ToString("N"));
        string storagePath = Path.Combine(tempDirectory, "profiles.json");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string legacyJson = """
                [
                  {
                    "ProfileId": "11111111-1111-1111-1111-111111111111",
                    "Username": "example",
                    "UserId": "12345",
                    "ArchiveRootPath": "C:\\archives\\example",
                    "MaxPostsPerSync": 250,
                    "IncludeOriginalPosts": true,
                    "IncludeReplies": false,
                    "IncludeQuotes": true,
                    "IncludeReposts": false,
                    "DownloadImages": true,
                    "DownloadVideos": false
                  }
                ]
                """;
            await File.WriteAllTextAsync(storagePath, legacyJson);

            ArchiveProfileRepository repository = new(storagePath);

            IReadOnlyList<ArchiveProfile> profiles = await repository.GetAllAsync(CancellationToken.None);

            Assert.HasCount(1, profiles);
            ArchiveProfile profile = profiles[0];
            Assert.AreEqual(ArchiveSourceKind.Api, profile.PreferredSource);
            Assert.IsNull(profile.ProfileUrl);
            Assert.AreEqual(100, profile.MaxPostsPerWebArchive);
            Assert.AreEqual(250, profile.MaxPostsPerSync);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task SaveAsyncPersistsNewSourceFields()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xarchiver-tests", Guid.NewGuid().ToString("N"));
        string storagePath = Path.Combine(tempDirectory, "profiles.json");

        try
        {
            ArchiveProfileRepository repository = new(storagePath);
            ArchiveProfile profile = new()
            {
                ArchiveRootPath = "C:\\archives\\example",
                MaxPostsPerSync = 300,
                MaxPostsPerWebArchive = 180,
                PreferredSource = ArchiveSourceKind.WebCapture,
                ProfileUrl = "https://x.com/example",
                Username = "example",
            };

            await repository.SaveAsync(profile, CancellationToken.None);

            string json = await File.ReadAllTextAsync(storagePath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement savedProfile = document.RootElement[0];
            Assert.AreEqual((int)ArchiveSourceKind.WebCapture, savedProfile.GetProperty("PreferredSource").GetInt32());
            Assert.AreEqual("https://x.com/example", savedProfile.GetProperty("ProfileUrl").GetString());
            Assert.AreEqual(180, savedProfile.GetProperty("MaxPostsPerWebArchive").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
