using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class PostReviewService : IPostReviewService
{
    private const int PreviewPageSize = 100;

    private readonly IXCredentialStore _credentialStore;
    private readonly IXApiClient _xApiClient;
    private readonly IArchiveIndexRepository _archiveIndexRepository;

    public PostReviewService(
        IXCredentialStore credentialStore,
        IXApiClient xApiClient,
        IArchiveIndexRepository archiveIndexRepository)
    {
        _credentialStore = credentialStore;
        _xApiClient = xApiClient;
        _archiveIndexRepository = archiveIndexRepository;
    }

    public async Task<PreviewPageResult> LoadPageAsync(ApiSyncRequest request, string? paginationToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArchiveProfile profile = request.Profile;
        string? bearerToken = await _credentialStore.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException("Missing X credential.");
        }

        XUserProfile user = await _xApiClient.GetUserAsync(profile.Username, bearerToken, cancellationToken).ConfigureAwait(false);

        List<PreviewPostRecord> matchingPosts = [];
        int scannedPostReads = 0;
        string? nextToken = paginationToken;
        bool isFirstRequest = true;

        while (matchingPosts.Count < PreviewPageSize)
        {
            PreviewPageResult page = await _xApiClient
                .GetUserPreviewPostsAsync(
                    user,
                    bearerToken,
                    request.ArchiveStartUtc,
                    request.ArchiveEndUtc,
                    isFirstRequest ? paginationToken : nextToken,
                    PreviewPageSize,
                    cancellationToken)
                .ConfigureAwait(false);

            scannedPostReads += page.ScannedPostReads;
            bool reachedBoundary = false;

            foreach (PreviewPostRecord post in page.Posts.Where(candidate => MatchesFilter(profile, candidate.PostType)))
            {
                if (!MatchesArchiveRange(post.CreatedAtUtc, request))
                {
                    if (request.ArchiveStartUtc.HasValue && post.CreatedAtUtc < request.ArchiveStartUtc.Value)
                    {
                        reachedBoundary = true;
                    }

                    continue;
                }

                matchingPosts.Add(FilterMedia(post, profile));

                if (matchingPosts.Count >= PreviewPageSize)
                {
                    break;
                }
            }

            if (reachedBoundary || string.IsNullOrWhiteSpace(page.NextToken))
            {
                nextToken = null;
                break;
            }

            nextToken = page.NextToken;
            isFirstRequest = false;
        }

        IReadOnlySet<string> archivedPostIds = await _archiveIndexRepository
            .GetArchivedPostIdsAsync(profile, matchingPosts.Select(post => post.PostId).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        foreach (PreviewPostRecord post in matchingPosts)
        {
            post.IsAlreadyArchived = archivedPostIds.Contains(post.PostId);
            post.IsSelected = false;
        }

        return new PreviewPageResult
        {
            NextToken = nextToken,
            Posts = matchingPosts,
            ScannedPostReads = scannedPostReads,
        };
    }

    private static PreviewPostRecord FilterMedia(PreviewPostRecord post, ArchiveProfile profile)
    {
        return new PreviewPostRecord
        {
            ConversationId = post.ConversationId,
            CreatedAtUtc = post.CreatedAtUtc,
            InReplyToUserId = post.InReplyToUserId,
            IsAlreadyArchived = post.IsAlreadyArchived,
            IsSelected = post.IsSelected,
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
                    media => new PreviewMediaRecord
                    {
                        DurationMs = media.DurationMs,
                        Height = media.Height,
                        IsPartial = media.IsPartial,
                        Kind = media.Kind,
                        MediaKey = media.MediaKey,
                        PostId = media.PostId,
                        PreviewImageUrl = media.PreviewImageUrl,
                        SourceUrl = media.SourceUrl,
                        Width = media.Width,
                    })
                .ToList(),
            PostId = post.PostId,
            PostType = post.PostType,
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
}
