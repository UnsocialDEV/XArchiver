using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class WebArchiveServiceTests
{
    [TestMethod]
    public async Task ArchiveAsyncWhenScrapeSucceedsWritesArchiveAndSavesProfile()
    {
        FakeProfileWebScraper scraper = new(
        [
            new ScrapedPostRecord
            {
                CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 16, 0, 0, TimeSpan.Zero),
                Media =
                [
                    new ScrapedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Image,
                        MediaKey = "post-1_image_0",
                        SourceUrl = "https://pbs.twimg.com/media/a.jpg",
                    },
                ],
                PostId = "post-1",
                RawHtml = "<article>one</article>",
                SourceUrl = "https://x.com/example/status/1",
                Text = "first",
                Username = "example",
            },
            new ScrapedPostRecord
            {
                CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 17, 0, 0, TimeSpan.Zero),
                Media =
                [
                    new ScrapedMediaRecord
                    {
                        Kind = ArchiveMediaKind.Video,
                        MediaKey = "post-2_video_0",
                        SourceUrl = "https://video.twimg.com/test.mp4",
                    },
                ],
                PostId = "post-2",
                RawHtml = "<article>two</article>",
                SourceUrl = "https://x.com/example/status/2",
                Text = "second",
                Username = "example",
            },
        ]);
        FakeArchiveFileWriter archiveFileWriter = new();
        FakeArchiveIndexRepository archiveIndexRepository = new();
        FakeArchiveProfileRepository archiveProfileRepository = new();
        WebArchiveService service = new(
            scraper,
            archiveFileWriter,
            archiveIndexRepository,
            archiveProfileRepository,
            new ScrapedPostArchiveMapper());

        List<WebArchiveProgressSnapshot> progressSnapshots = [];
        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                MaxPostsToScrape = 5,
                ProfileUrl = "https://x.com/example",
                Username = "example",
            },
            new Progress<WebArchiveProgressSnapshot>(snapshot => progressSnapshots.Add(snapshot)),
            new FakeScraperDiagnosticsSink(),
            new ScraperPauseGate(),
            new ScraperRunControl(),
            preferVisibleBrowser: false,
            CancellationToken.None);

        Assert.IsTrue(result.WasSuccessful);
        Assert.AreEqual(2, result.SavedPostCount);
        Assert.AreEqual(1, result.DownloadedImageCount);
        Assert.AreEqual(1, result.DownloadedVideoCount);
        Assert.HasCount(2, archiveFileWriter.WrittenPosts);
        ArchiveProfile savedProfile = archiveProfileRepository.SavedProfiles.Single();
        Assert.AreEqual("example", savedProfile.Username);
        Assert.AreEqual(ArchiveSourceKind.WebCapture, savedProfile.PreferredSource);
        Assert.AreEqual("https://x.com/example", savedProfile.ProfileUrl);
        Assert.AreEqual(5, savedProfile.MaxPostsPerWebArchive);
        Assert.HasCount(2, archiveIndexRepository.UpsertedPosts);
        Assert.IsTrue(progressSnapshots.Any(snapshot => snapshot.StageText == "Saving archive"));
        Assert.AreEqual("Completed", progressSnapshots[^1].StageText);
    }

    [TestMethod]
    public async Task ArchiveAsyncWhenNoPostsAreScrapedReturnsFailure()
    {
        WebArchiveService service = new(
            new FakeProfileWebScraper([]),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
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

        Assert.IsFalse(result.WasSuccessful);
        Assert.AreEqual("No posts were found on that profile page.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ArchiveAsyncWhenStopAndSaveRequestedReturnsPartialSuccess()
    {
        WebArchiveService service = new(
            new FakeProfileWebScraper(
            [
                new ScrapedPostRecord
                {
                    CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 18, 0, 0, TimeSpan.Zero),
                    PostId = "post-3",
                    RawHtml = "<article>partial</article>",
                    SourceUrl = "https://x.com/example/status/3",
                    Text = "partial",
                    Username = "example",
                },
            ]),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());
        ScraperRunControl runControl = new();
        runControl.RequestStopAndSave();

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                MaxPostsToScrape = 5,
                ProfileUrl = "https://x.com/example",
                Username = "example",
            },
            new Progress<WebArchiveProgressSnapshot>(_ => { }),
            new FakeScraperDiagnosticsSink(),
            new ScraperPauseGate(),
            runControl,
            preferVisibleBrowser: false,
            CancellationToken.None);

        Assert.IsTrue(result.WasSuccessful);
        Assert.IsTrue(result.WasPartialSave);
        Assert.AreEqual(1, result.SavedPostCount);
    }

    [TestMethod]
    public async Task ArchiveAsyncWhenStopAndSaveRequestedWithoutPostsReturnsEmptyPartialSuccess()
    {
        WebArchiveService service = new(
            new FakeProfileWebScraper([]),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());
        ScraperRunControl runControl = new();
        runControl.RequestStopAndSave();

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                MaxPostsToScrape = 5,
                ProfileUrl = "https://x.com/example",
                Username = "example",
            },
            new Progress<WebArchiveProgressSnapshot>(_ => { }),
            new FakeScraperDiagnosticsSink(),
            new ScraperPauseGate(),
            runControl,
            preferVisibleBrowser: false,
            CancellationToken.None);

        Assert.IsTrue(result.WasSuccessful);
        Assert.IsTrue(result.WasPartialSave);
        Assert.AreEqual(0, result.SavedPostCount);
    }

    [TestMethod]
    public async Task ArchiveAsyncWhenConservativeStopOccursWithoutPostsReturnsWarningFailure()
    {
        WebArchiveService service = new(
            new FakeProfileWebScraper(
                [],
                runControl => runControl.RequestConservativeStop("Conservative mode stopped early after repeated blocking gates.")),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                ExecutionMode = ScraperExecutionMode.Conservative,
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

        Assert.IsFalse(result.WasSuccessful);
        Assert.IsTrue(result.WasConservativeStop);
        Assert.AreEqual("Conservative mode stopped early after repeated blocking gates.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ArchiveAsyncWhenConservativeStopOccursAfterPostsReturnsSavedSuccess()
    {
        WebArchiveService service = new(
            new FakeProfileWebScraper(
            [
                new ScrapedPostRecord
                {
                    CreatedAtUtc = new DateTimeOffset(2026, 4, 14, 19, 0, 0, TimeSpan.Zero),
                    PostId = "post-4",
                    RawHtml = "<article>conservative</article>",
                    SourceUrl = "https://x.com/example/status/4",
                    Text = "conservative",
                    Username = "example",
                },
            ],
                runControl => runControl.RequestConservativeStop("Conservative mode stopped early after repeated route recovery events.")),
            new FakeArchiveFileWriter(),
            new FakeArchiveIndexRepository(),
            new FakeArchiveProfileRepository(),
            new ScrapedPostArchiveMapper());

        WebArchiveResult result = await service.ArchiveAsync(
            new WebArchiveRequest
            {
                ArchiveRootPath = "C:\\archives",
                ExecutionMode = ScraperExecutionMode.Conservative,
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
        Assert.IsTrue(result.WasConservativeStop);
        Assert.AreEqual(1, result.SavedPostCount);
    }

    private sealed class FakeArchiveFileWriter : IArchiveFileWriter
    {
        public List<ArchivedPostRecord> WrittenPosts { get; } = [];

        public Task<ArchivedPostRecord> WriteAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            post.ArchivedAtUtc = DateTimeOffset.UtcNow;
            WrittenPosts.Add(post);
            return Task.FromResult(post);
        }
    }

    private sealed class FakeArchiveIndexRepository : IArchiveIndexRepository
    {
        public List<ArchivedPostRecord> UpsertedPosts { get; } = [];

        public Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ArchivedPostRecord?>(null);
        }

        public Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(
            ArchiveProfile profile,
            IReadOnlyCollection<string> postIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        public Task InitializeAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchivedPostRecord>> QueryAsync(
            ArchiveProfile profile,
            ArchiveViewerFilter filter,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedPostRecord>>([]);
        }

        public Task<IReadOnlyList<ArchivedGalleryMediaRecord>> QueryGalleryMediaAsync(
            ArchiveProfile profile,
            ArchiveViewerFilter filter,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchivedGalleryMediaRecord>>([]);
        }

        public Task UpsertAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
        {
            UpsertedPosts.Add(post);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeArchiveProfileRepository : IArchiveProfileRepository
    {
        public List<ArchiveProfile> SavedProfiles { get; } = [];

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArchiveProfile>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ArchiveProfile>>(SavedProfiles.ToList());
        }

        public Task SaveAsync(ArchiveProfile profile, CancellationToken cancellationToken)
        {
            ArchiveProfile? existing = SavedProfiles.FirstOrDefault(saved => saved.ProfileId == profile.ProfileId);
            if (existing is null)
            {
                SavedProfiles.Add(profile);
            }
            else
            {
                int index = SavedProfiles.IndexOf(existing);
                SavedProfiles[index] = profile;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileWebScraper : IProfileWebScraper
    {
        private readonly Action<IScraperRunControl>? _onScrapeStart;
        private readonly IReadOnlyList<ScrapedPostRecord> _posts;

        public FakeProfileWebScraper(IReadOnlyList<ScrapedPostRecord> posts, Action<IScraperRunControl>? onScrapeStart = null)
        {
            _onScrapeStart = onScrapeStart;
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
            _onScrapeStart?.Invoke(runControl);

            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Test",
                    Message = "Scrape started.",
                    StageText = "Scrolling",
                    Url = request.ProfileUrl,
                });

            progress.Report(
                new WebArchiveProgressSnapshot
                {
                    CollectedPostCount = _posts.Count,
                    CurrentUrl = request.ProfileUrl,
                    RunState = ScraperRunState.Running,
                    StageText = "Scrolling",
                    TargetPostCount = request.MaxPostsToScrape,
                    VisiblePostCount = _posts.Count,
                });

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
