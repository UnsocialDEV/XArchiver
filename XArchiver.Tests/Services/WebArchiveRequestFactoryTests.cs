using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class WebArchiveRequestFactoryTests
{
    [TestMethod]
    public void CreateMapsSavedProfileIntoRequest()
    {
        ArchiveProfile profile = new()
        {
            ArchiveRootPath = "C:\\archives\\example",
            MaxPostsPerWebArchive = 175,
            PreferredSource = ArchiveSourceKind.WebCapture,
            ProfileUrl = "https://x.com/example",
            Username = "example",
        };
        WebArchiveRequestFactory factory = new();
        DateTimeOffset archiveStart = new(2026, 4, 14, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset archiveEnd = new(2026, 4, 14, 16, 0, 0, TimeSpan.Zero);

        WebArchiveRequest request = factory.Create(profile, ScraperExecutionMode.Conservative, archiveStart, archiveEnd);

        Assert.AreEqual("C:\\archives\\example", request.ArchiveRootPath);
        Assert.AreEqual(archiveEnd, request.ArchiveEndUtc);
        Assert.AreEqual(archiveStart, request.ArchiveStartUtc);
        Assert.AreEqual(ScraperExecutionMode.Conservative, request.ExecutionMode);
        Assert.AreEqual(175, request.MaxPostsToScrape);
        Assert.AreEqual("https://x.com/example", request.ProfileUrl);
        Assert.AreEqual("example", request.Username);
    }
}
