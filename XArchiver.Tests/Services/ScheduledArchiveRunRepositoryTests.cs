using System.Text.Json;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ScheduledArchiveRunRepositoryTests
{
    [TestMethod]
    public async Task SaveAsyncPersistsScheduledRunFields()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xarchiver-tests", Guid.NewGuid().ToString("N"));
        string storagePath = Path.Combine(tempDirectory, "scheduled-runs.json");

        try
        {
            ScheduledArchiveRunRepository repository = new(storagePath);
            ScheduledArchiveRunRecord run = new()
            {
                ApiSyncRequest = new ApiSyncRequest
                {
                    ArchiveEndUtc = new DateTimeOffset(2026, 4, 14, 16, 0, 0, TimeSpan.Zero),
                    ArchiveStartUtc = new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
                    Profile = new ArchiveProfile
                    {
                        ArchiveRootPath = "C:\\archives",
                        Username = "example",
                    },
                },
                ScheduledStartUtc = new DateTimeOffset(2026, 4, 14, 18, 0, 0, TimeSpan.Zero),
                SourceKind = ScheduledArchiveRunSourceKind.ApiSync,
                State = ScheduledArchiveRunState.Pending,
                StatusText = "Scheduled API sync",
            };

            await repository.SaveAsync(run, CancellationToken.None);

            string json = await File.ReadAllTextAsync(storagePath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement savedRun = document.RootElement[0];
            Assert.AreEqual((int)ScheduledArchiveRunSourceKind.ApiSync, savedRun.GetProperty("SourceKind").GetInt32());
            Assert.AreEqual("example", savedRun.GetProperty("ApiSyncRequest").GetProperty("Profile").GetProperty("Username").GetString());
            Assert.AreEqual("Scheduled API sync", savedRun.GetProperty("StatusText").GetString());
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
    public async Task DeleteAsyncRemovesPersistedRun()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xarchiver-tests", Guid.NewGuid().ToString("N"));
        string storagePath = Path.Combine(tempDirectory, "scheduled-runs.json");

        try
        {
            ScheduledArchiveRunRepository repository = new(storagePath);
            ScheduledArchiveRunRecord run = new()
            {
                ScheduledStartUtc = DateTimeOffset.UtcNow.AddHours(1),
                SourceKind = ScheduledArchiveRunSourceKind.WebCapture,
                State = ScheduledArchiveRunState.Pending,
                StatusText = "Scheduled web capture",
                WebArchiveRequest = new WebArchiveRequest
                {
                    ArchiveRootPath = "C:\\archives",
                    MaxPostsToScrape = 20,
                    ProfileUrl = "https://x.com/example",
                    Username = "example",
                },
            };

            await repository.SaveAsync(run, CancellationToken.None);
            await repository.DeleteAsync(run.RunId, CancellationToken.None);

            IReadOnlyList<ScheduledArchiveRunRecord> runs = await repository.GetAllAsync(CancellationToken.None);
            Assert.IsEmpty(runs);
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
    public async Task GetAllAsyncWhenLegacyArchiveUntilFieldExistsHydratesArchiveEndUtc()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xarchiver-tests", Guid.NewGuid().ToString("N"));
        string storagePath = Path.Combine(tempDirectory, "scheduled-runs.json");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            await File.WriteAllTextAsync(
                storagePath,
                """
                [
                  {
                    "RunId": "11111111-1111-1111-1111-111111111111",
                    "ScheduledStartUtc": "2026-04-14T18:00:00+00:00",
                    "SourceKind": 0,
                    "State": 0,
                    "StatusText": "Scheduled API sync",
                    "ApiSyncRequest": {
                      "ArchiveUntilUtc": "2026-04-14T16:00:00+00:00",
                      "Profile": {
                        "ArchiveRootPath": "C:\\archives",
                        "Username": "example"
                      }
                    }
                  }
                ]
                """);

            ScheduledArchiveRunRepository repository = new(storagePath);

            IReadOnlyList<ScheduledArchiveRunRecord> runs = await repository.GetAllAsync(CancellationToken.None);

            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(new DateTimeOffset(2026, 4, 14, 16, 0, 0, TimeSpan.Zero), runs[0].ApiSyncRequest!.ArchiveEndUtc);
            Assert.IsNull(runs[0].ApiSyncRequest!.ArchiveStartUtc);
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
