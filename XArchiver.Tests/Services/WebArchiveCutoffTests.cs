using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class WebArchiveCutoffTests
{
    [TestMethod]
    public async Task ArchiveAsyncWhenArchiveRangeIsSetSavesOnlyMatchingPosts()
    {
        DateTimeOffset archiveStart = new(2026, 4, 14, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset archiveEnd = new(2026, 4, 14, 19, 0, 0, TimeSpan.Zero);
        WebArchiveService service = new(
            new FakeProfileWebScraper(
            [
                new ScrapedPostRecord
                {
                    CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 20, 0, 0, TimeSpan.Zero),
                    PostId = "too-new",
                    RawHtml = "<article>too-new</article>",
                    SourceUrl = "https://x.com/example/status/0",
                    Text = "too new",
                    Username = "example",
                },
                new ScrapedPostRecord
                {
                    CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 17, 0, 0, TimeSpan.Zero),
                    PostId = "in-range",
                    RawHtml = "<article>in-range</article>",
                    SourceUrl = "https://x.com/example/status/1",
                    Text = "in range",
                    Username = "example",
                },
                new ScrapedPostRecord
                {
                    CreatedAtUtc = archiveStart.AddMinutes(-1),
                    PostId = "too-old",
                    RawHtml = "<article>too-old</article>",
                    SourceUrl = "https://x.com/example/status/2",
                    Text = "too old",
                    Username = "example",
                },
            ]),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                ArchiveEndUtc = archiveEnd,
                ArchiveStartUtc = archiveStart,
                MaxPostsToScrape = 5,
                ProfileUrl = "https://x.com/example",
                Username = "example",
            },
            new Progress<WebArchiveProgressSnapshot>(_ => { }),
            new FakeScraperDiagnosticsSink(),
            new ScraperPauseGate(),
            new ScraperRunControl(),
            preferVisibleBrowser: false,
            CancellationToken.None);

        Assert.IsTrue(result.WasSuccessful);
        Assert.AreEqual(1, result.SavedPostCount);
    }

    private sealed class FakeArchiveFileWriter : IArchiveFileWriter
    {
        public Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            return Task.FromResult(post);
        }
    }

    private sealed class FakeArchiveIndexRepository : IArchiveIndexRepository
    {
        public Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ArchivedPostRecord?>(null);
        }

        public Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(ArchiveProfile profile, IReadOnlyCollection<string> postIds, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        public Task InitializeAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchivedPostRecord>> QueryAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedPostRecord>>([]);
        }

        public Task<IReadOnlyList<ArchivedGalleryMediaRecord>> QueryGalleryMediaAsync(ArchiveProfile profile, ArchiveViewerFilter filter, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedGalleryMediaRecord>>([]);
        }

        public Task UpsertAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeArchiveProfileRepository : IArchiveProfileRepository
    {
        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchiveProfile>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchiveProfile>>([]);
        }

        public Task SaveAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileWebScraper : IProfileWebScraper
    {
        private readonly IReadOnlyList<ScrapedPostRecord> _posts;

        public FakeProfileWebScraper(IReadOnlyList<ScrapedPostRecord> posts)
        {
            _posts = posts;
        }

        public Task<bool> HasSessionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ScrapedPostRecord>> ScrapeAsync(
            WebArchiveRequest request,
            IProgress<WebArchiveProgressSnapshot> progress,
            IScraperDiagnosticsSink diagnosticsSink,
            IScraperPauseGate pauseGate,
            IScraperRunControl runControl,
            bool preferVisibleBrowser,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_posts);
        }

        public Task<bool> ValidateSessionAsync(string profileUrl, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class FakeScraperDiagnosticsSink : IScraperDiagnosticsSink
    {
        public string DiagnosticsDirectory { get; } = Path.Combine(Path.GetTempPath(), "xarchiver-tests");

        public void ReportDiscoveredPost(ScrapedPostRecord post)
        {
        }

        public void ReportEvent(ScraperDiagnosticsEvent diagnosticsEvent)
        {
        }

        public void ReportLiveSnapshot(ScraperLiveSnapshot snapshot)
        {
        }
    }
}
