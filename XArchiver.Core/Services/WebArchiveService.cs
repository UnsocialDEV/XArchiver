using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class WebArchiveService : IWebArchiveService
{
    private readonly IArchiveFileWriter _archiveFileWriter;
    private readonly IArchiveIndexRepository _archiveIndexRepository;
    private readonly IArchiveProfileRepository _archiveProfileRepository;
    private readonly IProfileWebScraper _profileWebScraper;
    private readonly ScrapedPostArchiveMapper _scrapedPostArchiveMapper;

    public WebArchiveService(
        IProfileWebScraper profileWebScraper,
        IArchiveFileWriter archiveFileWriter,
        IArchiveIndexRepository archiveIndexRepository,
        IArchiveProfileRepository archiveProfileRepository,
        ScrapedPostArchiveMapper scrapedPostArchiveMapper)
    {
        _profileWebScraper = profileWebScraper;
        _archiveFileWriter = archiveFileWriter;
        _archiveIndexRepository = archiveIndexRepository;
        _archiveProfileRepository = archiveProfileRepository;
        _scrapedPostArchiveMapper = scrapedPostArchiveMapper;
    }

    public async Task<WebArchiveResult> ArchiveAsync(
        WebArchiveRequest request,
        IProgress<WebArchiveProgressSnapshot> progress,
        IScraperDiagnosticsSink diagnosticsSink,
        IScraperPauseGate pauseGate,
        IScraperRunControl runControl,
        bool preferVisibleBrowser,
        CancellationToken cancellationToken)
    {
        try
        {
            bool isPartialSaveRequested = runControl.IsStopAndSaveRequested;
            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Archive",
                    Message = $"Starting scrape archive for @{request.Username}.",
                    StageText = "Starting scrape",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = request.ProfileUrl,
                });

            IReadOnlyList<ScrapedPostRecord> scrapedPosts = await _profileWebScraper
                .ScrapeAsync(request, progress, diagnosticsSink, pauseGate, runControl, preferVisibleBrowser, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<ScrapedPostRecord> filteredPosts = scrapedPosts
                .Where(post => MatchesArchiveRange(post.CreatedAtUtc, request))
                .ToList();

            isPartialSaveRequested = runControl.IsStopAndSaveRequested;
            bool wasConservativeStop = runControl.HasConservativeStopRequested;
            string conservativeStopReason = runControl.ConservativeStopReason;

            if (filteredPosts.Count == 0)
            {
                if (scrapedPosts.Count > 0 && (request.ArchiveStartUtc.HasValue || request.ArchiveEndUtc.HasValue))
                {
                    diagnosticsSink.ReportEvent(
                        new ScraperDiagnosticsEvent
                        {
                            Category = "Archive",
                            Message = "No scraped posts matched the selected archive range.",
                            Severity = ScraperDiagnosticsSeverity.Warning,
                            StageText = "Up to date",
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Url = request.ProfileUrl,
                        });

                    return new WebArchiveResult
                    {
                        SavedPostCount = 0,
                        WasSuccessful = true,
                    };
                }

                if (wasConservativeStop)
                {
                    diagnosticsSink.ReportEvent(
                        new ScraperDiagnosticsEvent
                        {
                            Category = "Archive",
                            Message = conservativeStopReason,
                            Severity = ScraperDiagnosticsSeverity.Warning,
                            StageText = "Conservative stop completed",
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Url = request.ProfileUrl,
                        });

                    return new WebArchiveResult
                    {
                        ConservativeStopReason = conservativeStopReason,
                        ErrorMessage = conservativeStopReason,
                        WasConservativeStop = true,
                        WasSuccessful = false,
                    };
                }

                if (isPartialSaveRequested)
                {
                    diagnosticsSink.ReportEvent(
                        new ScraperDiagnosticsEvent
                        {
                            Category = "Archive",
                            Message = "Scraper stop-and-save completed before any posts were collected.",
                            Severity = ScraperDiagnosticsSeverity.Warning,
                            StageText = "Partial save completed",
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Url = request.ProfileUrl,
                        });

                    return new WebArchiveResult
                    {
                        SavedPostCount = 0,
                        WasPartialSave = true,
                        WasSuccessful = true,
                    };
                }

                diagnosticsSink.ReportEvent(
                    new ScraperDiagnosticsEvent
                    {
                        Category = "Archive",
                        Message = "Scraper completed without finding any posts to archive.",
                        Severity = ScraperDiagnosticsSeverity.Warning,
                        StageText = "Archive failed",
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = request.ProfileUrl,
                    });

                return new WebArchiveResult
                {
                    ErrorMessage = "No posts were found on that profile page.",
                    WasSuccessful = false,
                };
            }

            ArchiveProfile archiveProfile = await ResolveProfileAsync(request, cancellationToken).ConfigureAwait(false);
            await _archiveIndexRepository.InitializeAsync(archiveProfile, cancellationToken).ConfigureAwait(false);

            int savedPostCount = 0;
            int downloadedImageCount = 0;
            int downloadedVideoCount = 0;
            string saveStageText = isPartialSaveRequested
                ? "Saving partial archive"
                : wasConservativeStop
                    ? "Saving conservative archive"
                    : "Saving archive";

            foreach (ScrapedPostRecord scrapedPost in filteredPosts.Take(request.MaxPostsToScrape))
            {
                await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(
                    new WebArchiveProgressSnapshot
                    {
                        CurrentUrl = scrapedPost.SourceUrl,
                        CollectedPostCount = filteredPosts.Count,
                        DownloadedImageCount = downloadedImageCount,
                        DownloadedVideoCount = downloadedVideoCount,
                        RunState = ScraperRunState.Running,
                        SavedPostCount = savedPostCount,
                        StageText = saveStageText,
                        TargetPostCount = request.MaxPostsToScrape,
                        VisiblePostCount = filteredPosts.Count,
                    });

                diagnosticsSink.ReportEvent(
                    new ScraperDiagnosticsEvent
                    {
                        Category = "Archive",
                        Message = $"Saving archived post {scrapedPost.PostId}.",
                        StageText = saveStageText,
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Url = scrapedPost.SourceUrl,
                    });

                ArchivedPostRecord archivedPost = await _archiveFileWriter
                    .WriteAsync(archiveProfile, _scrapedPostArchiveMapper.Map(archiveProfile, scrapedPost), cancellationToken)
                    .ConfigureAwait(false);

                await _archiveIndexRepository.UpsertAsync(archiveProfile, archivedPost, cancellationToken).ConfigureAwait(false);

                downloadedImageCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Image);
                downloadedVideoCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Video);
                savedPostCount++;
            }

            archiveProfile.LastSuccessfulSyncUtc = DateTimeOffset.UtcNow;
            await _archiveProfileRepository.SaveAsync(archiveProfile, cancellationToken).ConfigureAwait(false);

            progress.Report(
                new WebArchiveProgressSnapshot
                {
                    CurrentUrl = request.ProfileUrl,
                    CollectedPostCount = filteredPosts.Count,
                    DownloadedImageCount = downloadedImageCount,
                    DownloadedVideoCount = downloadedVideoCount,
                    RunState = ScraperRunState.Completed,
                    SavedPostCount = savedPostCount,
                    StageText = isPartialSaveRequested
                        ? "Partial save completed"
                        : wasConservativeStop
                            ? "Conservative stop completed"
                            : "Completed",
                    TargetPostCount = request.MaxPostsToScrape,
                    VisiblePostCount = filteredPosts.Count,
                });

            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Archive",
                    Message = isPartialSaveRequested
                        ? $"Partial archive save completed with {savedPostCount} posts."
                        : wasConservativeStop
                            ? $"Conservative-mode archive save completed with {savedPostCount} posts. {conservativeStopReason}"
                        : $"Archive save completed with {savedPostCount} posts.",
                    StageText = isPartialSaveRequested
                        ? "Partial save completed"
                        : wasConservativeStop
                            ? "Conservative stop completed"
                            : "Completed",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = request.ProfileUrl,
                });

            return new WebArchiveResult
            {
                ArchiveProfile = archiveProfile,
                ConservativeStopReason = wasConservativeStop ? conservativeStopReason : null,
                DownloadedImageCount = downloadedImageCount,
                DownloadedVideoCount = downloadedVideoCount,
                ErrorMessage = isPartialSaveRequested && savedPostCount == 0
                    ? "Scrape stopped before any posts were saved."
                    : null,
                SavedPostCount = savedPostCount,
                WasConservativeStop = wasConservativeStop,
                WasPartialSave = isPartialSaveRequested,
                WasSuccessful = true,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            diagnosticsSink.ReportEvent(
                new ScraperDiagnosticsEvent
                {
                    Category = "Archive",
                    Message = exception.Message,
                    Severity = ScraperDiagnosticsSeverity.Error,
                    StageText = "Failed",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Url = request.ProfileUrl,
                });

            return new WebArchiveResult
            {
                ErrorMessage = exception.Message,
                WasSuccessful = false,
            };
        }
    }

    private async Task<ArchiveProfile> ResolveProfileAsync(WebArchiveRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArchiveProfile> existingProfiles = await _archiveProfileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        string normalizedRoot = NormalizePath(request.ArchiveRootPath);
        ArchiveProfile? existingProfile = existingProfiles.FirstOrDefault(
            profile => string.Equals(profile.Username, request.Username, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(NormalizePath(profile.ArchiveRootPath), normalizedRoot, StringComparison.OrdinalIgnoreCase));

        if (existingProfile is not null)
        {
            existingProfile.ArchiveRootPath = request.ArchiveRootPath;
            existingProfile.Username = request.Username;
            return existingProfile;
        }

        return new ArchiveProfile
        {
            ArchiveRootPath = request.ArchiveRootPath,
            DownloadImages = true,
            DownloadVideos = true,
            IncludeOriginalPosts = true,
            IncludeQuotes = true,
            IncludeReplies = true,
            IncludeReposts = true,
            MaxPostsPerWebArchive = request.MaxPostsToScrape,
            MaxPostsPerSync = request.MaxPostsToScrape,
            PreferredSource = ArchiveSourceKind.WebCapture,
            Username = request.Username,
            ProfileUrl = request.ProfileUrl,
            UserId = request.Username,
        };
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool MatchesArchiveRange(DateTimeOffset createdAtUtc, WebArchiveRequest request)
    {
        if (request.ArchiveStartUtc.HasValue && createdAtUtc < request.ArchiveStartUtc.Value)
        {
            return false;
        }

        if (request.ArchiveEndUtc.HasValue && createdAtUtc >= request.ArchiveEndUtc.Value)
        {
            return false;
        }

        return true;
    }
}
