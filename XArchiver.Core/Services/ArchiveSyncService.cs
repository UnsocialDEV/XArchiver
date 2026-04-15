using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Utilities;

namespace XArchiver.Core.Services;

public sealed class ArchiveSyncService : IArchiveSyncService
{
    private const int MaximumApiPageSize = 100;
    private const int MinimumApiPageSize = 5;

    private readonly IXCredentialStore _credentialStore;
    private readonly IXApiClient _xApiClient;
    private readonly IArchiveFileWriter _archiveFileWriter;
    private readonly IArchiveIndexRepository _archiveIndexRepository;

    public ArchiveSyncService(
        IXCredentialStore credentialStore,
        IXApiClient xApiClient,
        IArchiveFileWriter archiveFileWriter,
        IArchiveIndexRepository archiveIndexRepository)
    {
        _credentialStore = credentialStore;
        _xApiClient = xApiClient;
        _archiveFileWriter = archiveFileWriter;
        _archiveIndexRepository = archiveIndexRepository;
    }

    public async Task<SyncResult> SyncAsync(
        ApiSyncRequest request,
        IProgress<SyncProgressSnapshot> progress,
        ISyncPauseGate pauseGate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArchiveProfile profile = request.Profile;
        int requestedPostLimit = Math.Max(1, profile.MaxPostsPerSync);
        int archivedPostCount = 0;
        int imageCount = 0;
        int videoCount = 0;
        int partialMediaCount = 0;
        int scannedPageCount = 0;
        bool hasArchiveRange = request.ArchiveStartUtc.HasValue || request.ArchiveEndUtc.HasValue;
        string? paginationToken = null;
        string? highestSinceId = hasArchiveRange ? null : profile.LastSinceId;
        string? requestedSinceId = hasArchiveRange ? null : profile.LastSinceId;

        void Report(string stageText)
        {
            progress.Report(
                new SyncProgressSnapshot
                {
                    ArchivedPostCount = archivedPostCount,
                    DownloadedImageCount = imageCount,
                    DownloadedVideoCount = videoCount,
                    PartialMediaCount = partialMediaCount,
                    ScannedPageCount = scannedPageCount,
                    StageText = stageText,
                    TargetPostCount = requestedPostLimit,
                });
        }

        Report("Checking credential");

        if (!await _credentialStore.HasCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            Report("Missing credential");
            return new SyncResult
            {
                Status = SyncStatus.MissingCredential,
            };
        }

        string? bearerToken = await _credentialStore.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            Report("Missing credential");
            return new SyncResult
            {
                Status = SyncStatus.MissingCredential,
            };
        }

        try
        {
            await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            Report("Authenticating");
            XUserProfile user = await _xApiClient.GetUserAsync(profile.Username, bearerToken, cancellationToken).ConfigureAwait(false);

            await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            Report("Preparing archive");
            await _archiveIndexRepository.InitializeAsync(profile, cancellationToken).ConfigureAwait(false);

            while (archivedPostCount < requestedPostLimit)
            {
                await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                Report("Scanning posts");

                int pageSize = GetPageSize(requestedPostLimit, archivedPostCount);
                XTimelinePage timelinePage = await _xApiClient
                    .GetUserPostsAsync(
                        user,
                        bearerToken,
                        requestedSinceId,
                        request.ArchiveStartUtc,
                        request.ArchiveEndUtc,
                        paginationToken,
                        pageSize,
                        cancellationToken)
                    .ConfigureAwait(false);
                scannedPageCount++;
                Report("Scanning posts");

                if (timelinePage.Posts.Count == 0)
                {
                    break;
                }

                bool reachedBoundary = false;
                foreach (ArchivedPostRecord post in timelinePage.Posts.Where(candidate => MatchesFilter(profile, candidate.PostType)))
                {
                    if (!MatchesArchiveRange(post.CreatedAtUtc, request))
                    {
                        if (request.ArchiveStartUtc.HasValue && post.CreatedAtUtc < request.ArchiveStartUtc.Value)
                        {
                            reachedBoundary = true;
                        }

                        continue;
                    }

                    await pauseGate.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                    Report("Writing files");

                    ArchivedPostRecord postToArchive = ApplyProfileDetails(FilterMedia(post, profile), profile, user.UserId);
                    ArchivedPostRecord archivedPost = await _archiveFileWriter.WriteAsync(profile, postToArchive, cancellationToken).ConfigureAwait(false);
                    await _archiveIndexRepository.UpsertAsync(profile, archivedPost, cancellationToken).ConfigureAwait(false);

                    imageCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Image);
                    videoCount += archivedPost.Media.Count(media => media.Kind == ArchiveMediaKind.Video);
                    partialMediaCount += archivedPost.Media.Count(media => media.IsPartial);
                    highestSinceId = PostIdComparer.Max(highestSinceId, archivedPost.PostId);
                    archivedPostCount++;
                    Report("Writing files");

                    if (archivedPostCount >= requestedPostLimit)
                    {
                        break;
                    }
                }

                if (reachedBoundary || string.IsNullOrWhiteSpace(timelinePage.NextToken))
                {
                    break;
                }

                paginationToken = timelinePage.NextToken;
            }

            ArchiveProfile updatedProfile = CloneProfile(profile);
            updatedProfile.UserId = user.UserId;
            updatedProfile.LastSinceId = hasArchiveRange
                ? profile.LastSinceId
                : highestSinceId ?? profile.LastSinceId;
            updatedProfile.LastSuccessfulSyncUtc = DateTimeOffset.UtcNow;

            Report(GetCompletionStageText(archivedPostCount));

            return new SyncResult
            {
                ArchivedPostCount = archivedPostCount,
                DownloadedImageCount = imageCount,
                DownloadedVideoCount = videoCount,
                PartialMediaCount = partialMediaCount,
                ScannedPageCount = scannedPageCount,
                Status = SyncStatus.Success,
                UpdatedProfile = updatedProfile,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            Report("Failed");
            return new SyncResult
            {
                ErrorMessage = exception.Message,
                ScannedPageCount = scannedPageCount,
                Status = SyncStatus.Failed,
            };
        }
    }

    private static ArchivedPostRecord ApplyProfileDetails(ArchivedPostRecord post, ArchiveProfile profile, string userId)
    {
        post.ProfileId = profile.ProfileId;
        post.UserId = userId;
        return post;
    }

    private static ArchiveProfile CloneProfile(ArchiveProfile profile)
    {
        return new ArchiveProfile
        {
            ArchiveRootPath = profile.ArchiveRootPath,
            DownloadImages = profile.DownloadImages,
            DownloadVideos = profile.DownloadVideos,
            IncludeOriginalPosts = profile.IncludeOriginalPosts,
            IncludeQuotes = profile.IncludeQuotes,
            IncludeReplies = profile.IncludeReplies,
            IncludeReposts = profile.IncludeReposts,
            LastSinceId = profile.LastSinceId,
            LastSuccessfulSyncUtc = profile.LastSuccessfulSyncUtc,
            MaxPostsPerWebArchive = profile.MaxPostsPerWebArchive,
            MaxPostsPerSync = profile.MaxPostsPerSync,
            PreferredSource = profile.PreferredSource,
            ProfileId = profile.ProfileId,
            ProfileUrl = profile.ProfileUrl,
            UserId = profile.UserId,
            Username = profile.Username,
        };
    }

    private static ArchivedPostRecord FilterMedia(ArchivedPostRecord post, ArchiveProfile profile)
    {
        return new ArchivedPostRecord
        {
            ArchivedAtUtc = post.ArchivedAtUtc,
            ConversationId = post.ConversationId,
            CreatedAtUtc = post.CreatedAtUtc,
            InReplyToUserId = post.InReplyToUserId,
            LikeCount = post.LikeCount,
            MediaDetails = post.MediaDetails
                .Select(
                    detail => new ArchivedMediaDetailRecord
                    {
                        DurationMs = detail.DurationMs,
                        Height = detail.Height,
                        MediaKey = detail.MediaKey,
                        MediaType = detail.MediaType,
                        PreviewImageUrl = detail.PreviewImageUrl,
                        Url = detail.Url,
                        Variants = detail.Variants
                            .Select(
                                variant => new ArchivedMediaVariantRecord
                                {
                                    BitRate = variant.BitRate,
                                    ContentType = variant.ContentType,
                                    Url = variant.Url,
                                })
                            .ToList(),
                        Width = detail.Width,
                    })
                .ToList(),
            Media = post.Media
                .Where(media => (profile.DownloadImages && media.Kind == ArchiveMediaKind.Image) || (profile.DownloadVideos && media.Kind == ArchiveMediaKind.Video))
                .Select(
                    media => new ArchivedMediaRecord
                    {
                        DurationMs = media.DurationMs,
                        Height = media.Height,
                        IsPartial = media.IsPartial,
                        Kind = media.Kind,
                        MediaKey = media.MediaKey,
                        PostId = media.PostId,
                        RelativePath = media.RelativePath,
                        SourceUrl = media.SourceUrl,
                        Width = media.Width,
                    })
                .ToList(),
            MetadataRelativePath = post.MetadataRelativePath,
            PostId = post.PostId,
            PostType = post.PostType,
            ProfileId = post.ProfileId,
            QuoteCount = post.QuoteCount,
            RawPayloadJson = post.RawPayloadJson,
            ReferencedPosts = post.ReferencedPosts
                .Select(
                    reference => new ArchivedReferencedPostRecord
                    {
                        ReferenceType = reference.ReferenceType,
                        ReferencedPostId = reference.ReferencedPostId,
                    })
                .ToList(),
            ReplyCount = post.ReplyCount,
            RepostCount = post.RepostCount,
            Text = post.Text,
            TextRelativePath = post.TextRelativePath,
            UserId = post.UserId,
            Username = post.Username,
        };
    }

    private static bool MatchesFilter(ArchiveProfile profile, ArchivePostType postType)
    {
        return postType switch
        {
            ArchivePostType.Original => profile.IncludeOriginalPosts,
            ArchivePostType.Reply => profile.IncludeReplies,
            ArchivePostType.Quote => profile.IncludeQuotes,
            ArchivePostType.Repost => profile.IncludeReposts,
            _ => false,
        };
    }

    private static bool MatchesArchiveRange(DateTimeOffset createdAtUtc, ApiSyncRequest request)
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

    private static int GetPageSize(int requestedPostLimit, int archivedPostCount)
    {
        int remainingPosts = requestedPostLimit - archivedPostCount;
        int boundedPageSize = Math.Min(MaximumApiPageSize, remainingPosts);
        return Math.Max(MinimumApiPageSize, boundedPageSize);
    }

    private static string GetCompletionStageText(int archivedPostCount)
    {
        return archivedPostCount > 0
            ? "Completed"
            : "No new posts matched this sync";
    }
}
