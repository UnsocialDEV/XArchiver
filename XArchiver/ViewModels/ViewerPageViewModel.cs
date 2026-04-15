using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Utilities;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ViewerPageViewModel : ObservableObject
{
    private readonly IArchiveIndexRepository _archiveIndexRepository;
    private readonly IArchiveProfileRepository _profileRepository;
    private readonly IResourceService _resourceService;
    private bool _includeOriginalPosts = true;
    private bool _includeQuotes = true;
    private bool _includeReplies = true;
    private bool _includeReposts = true;
    private bool _includeTextPosts = true;
    private bool _includeImagePosts = true;
    private bool _includeVideoPosts = true;
    private bool _isBusy;
    private bool _isInitialized;
    private string _gallerySummaryText = string.Empty;
    private string _resultsSummaryText = string.Empty;
    private string _searchText = string.Empty;
    private ArchiveProfile? _selectedProfile;
    private ViewerGalleryItemViewModel? _selectedGalleryItem;
    private ViewerPostItemViewModel? _selectedPost;
    private string _statusMessage = string.Empty;

    public ViewerPageViewModel(
        IArchiveProfileRepository profileRepository,
        IArchiveIndexRepository archiveIndexRepository,
        ViewerDetailsViewModel details,
        MediaOverlayViewModel mediaOverlay,
        IResourceService resourceService,
        IVideoThumbnailCache videoThumbnailCache)
    {
        _profileRepository = profileRepository;
        _archiveIndexRepository = archiveIndexRepository;
        Details = details;
        Gallery = new ViewerGalleryViewModel(videoThumbnailCache);
        MediaOverlay = mediaOverlay;
        _resourceService = resourceService;
    }

    public ViewerDetailsViewModel Details { get; }

    public ViewerGalleryViewModel Gallery { get; }

    public bool IncludeOriginalPosts
    {
        get => _includeOriginalPosts;
        set => SetProperty(ref _includeOriginalPosts, value);
    }

    public bool IncludeQuotes
    {
        get => _includeQuotes;
        set => SetProperty(ref _includeQuotes, value);
    }

    public bool IncludeReplies
    {
        get => _includeReplies;
        set => SetProperty(ref _includeReplies, value);
    }

    public bool IncludeReposts
    {
        get => _includeReposts;
        set => SetProperty(ref _includeReposts, value);
    }

    public bool IncludeTextPosts
    {
        get => _includeTextPosts;
        set => SetProperty(ref _includeTextPosts, value);
    }

    public bool IncludeImagePosts
    {
        get => _includeImagePosts;
        set => SetProperty(ref _includeImagePosts, value);
    }

    public bool IncludeVideoPosts
    {
        get => _includeVideoPosts;
        set => SetProperty(ref _includeVideoPosts, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string GallerySummaryText
    {
        get => _gallerySummaryText;
        set => SetProperty(ref _gallerySummaryText, value);
    }

    public MediaOverlayViewModel MediaOverlay { get; }

    public ObservableCollection<ArchiveProfile> Profiles { get; } = [];

    public ObservableCollection<ViewerPostItemViewModel> Posts { get; } = [];

    public string ResultsSummaryText
    {
        get => _resultsSummaryText;
        set => SetProperty(ref _resultsSummaryText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public ViewerGalleryItemViewModel? SelectedGalleryItem
    {
        get => _selectedGalleryItem;
        set => SetProperty(ref _selectedGalleryItem, value);
    }

    public ArchiveProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public ViewerPostItemViewModel? SelectedPost
    {
        get => _selectedPost;
        set => SetProperty(ref _selectedPost, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        IReadOnlyList<ArchiveProfile> profiles = await _profileRepository.GetAllAsync(CancellationToken.None);
        Profiles.Clear();
        foreach (ArchiveProfile profile in profiles.OrderBy(profile => profile.Username, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
        _isInitialized = true;

        if (SelectedProfile is null)
        {
            StatusMessage = _resourceService.GetString("StatusViewerNoProfile");
            ResultsSummaryText = string.Empty;
            return;
        }

        await RefreshAsync();
    }

    public void CloseOverlay()
    {
        MediaOverlay.Close();
    }

    public void MoveOverlayNext()
    {
        MediaOverlay.MoveNext();
    }

    public void MoveOverlayPrevious()
    {
        MediaOverlay.MovePrevious();
    }

    public void OpenMedia(ViewerMediaItemViewModel mediaItem)
    {
        if (Details.MediaItems.Count == 0)
        {
            return;
        }

        MediaOverlay.Open(Details.MediaItems.Select(item => item.Media).ToList(), mediaItem.Media);
    }

    public async Task OpenGalleryItemAsync(ViewerGalleryItemViewModel? galleryItem)
    {
        SelectedGalleryItem = galleryItem;
        Gallery.SelectedItem = galleryItem;

        if (galleryItem is null || SelectedProfile is null)
        {
            await SelectPostRecordAsync(null);
            return;
        }

        ArchivedPostRecord? post = await _archiveIndexRepository.GetPostAsync(SelectedProfile, galleryItem.Item.ParentPostId, CancellationToken.None);
        if (post is null)
        {
            await SelectPostRecordAsync(null);
            return;
        }

        HydratePaths([post], ArchivePathBuilder.GetProfileRoot(SelectedProfile));
        await SelectPostRecordAsync(post);

        ArchivedMediaRecord selectedMedia = post.Media.FirstOrDefault(media => string.Equals(media.MediaKey, galleryItem.Item.Media.MediaKey, StringComparison.Ordinal))
            ?? galleryItem.Item.Media;
        MediaOverlay.Open(post.Media, selectedMedia);
    }

    public async Task RefreshAsync()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = _resourceService.GetString("StatusViewerNoProfile");
            ResultsSummaryText = string.Empty;
            GallerySummaryText = string.Empty;
            Posts.Clear();
            Gallery.Clear();
            await SelectPostAsync(null);
            return;
        }

        IsBusy = true;
        try
        {
            ArchiveViewerFilter filter = new()
            {
                IncludeOriginalPosts = IncludeOriginalPosts,
                IncludeQuotes = IncludeQuotes,
                IncludeReplies = IncludeReplies,
                IncludeReposts = IncludeReposts,
                IncludeTextPosts = IncludeTextPosts,
                IncludeImagePosts = IncludeImagePosts,
                IncludeVideoPosts = IncludeVideoPosts,
                SearchText = SearchText,
            };

            await RefreshPostsAsync(filter);
            await RefreshGalleryAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectPostAsync(ViewerPostItemViewModel? post)
    {
        SelectedPost = post;
        MediaOverlay.Close();
        await Details.LoadAsync(post?.Post);
    }

    private async Task RefreshGalleryAsync()
    {
        ArchiveViewerFilter galleryFilter = new()
        {
            IncludeOriginalPosts = true,
            IncludeQuotes = true,
            IncludeReplies = true,
            IncludeReposts = true,
            IncludeTextPosts = false,
            IncludeImagePosts = true,
            IncludeVideoPosts = true,
            SearchText = string.Empty,
        };

        IReadOnlyList<ArchivedGalleryMediaRecord> galleryItems = await _archiveIndexRepository.QueryGalleryMediaAsync(SelectedProfile!, galleryFilter, CancellationToken.None);
        string profileRoot = ArchivePathBuilder.GetProfileRoot(SelectedProfile!);
        HydrateGalleryPaths(galleryItems, profileRoot);

        Gallery.Load(galleryItems);
        SelectedGalleryItem = Gallery.SelectedItem;
        GallerySummaryText = _resourceService.Format("ViewerGalleryResultsSummaryFormat", Gallery.Items.Count);
    }

    private async Task RefreshPostsAsync(ArchiveViewerFilter filter)
    {
        IReadOnlyList<ArchivedPostRecord> posts = await _archiveIndexRepository.QueryAsync(SelectedProfile!, filter, CancellationToken.None);
        string profileRoot = ArchivePathBuilder.GetProfileRoot(SelectedProfile!);
        HydratePaths(posts, profileRoot);

        Posts.Clear();
        foreach (ArchivedPostRecord post in posts)
        {
            Posts.Add(new ViewerPostItemViewModel(post));
        }

        ResultsSummaryText = _resourceService.Format("ViewerResultsSummaryFormat", Posts.Count);
        StatusMessage = _resourceService.Format("StatusViewerLoadedFormat", Posts.Count);
        await SelectPostAsync(Posts.FirstOrDefault());
    }

    private async Task SelectPostRecordAsync(ArchivedPostRecord? post)
    {
        MediaOverlay.Close();
        if (post is null)
        {
            SelectedPost = null;
            await Details.LoadAsync(null);
            return;
        }

        ViewerPostItemViewModel? matchingPost = Posts.FirstOrDefault(item => string.Equals(item.Post.PostId, post.PostId, StringComparison.Ordinal));
        SelectedPost = matchingPost ?? new ViewerPostItemViewModel(post);
        await Details.LoadAsync(post);
    }

    private static void HydrateGalleryPaths(IReadOnlyList<ArchivedGalleryMediaRecord> mediaItems, string profileRoot)
    {
        foreach (ArchivedGalleryMediaRecord mediaItem in mediaItems)
        {
            if (!string.IsNullOrWhiteSpace(mediaItem.Media.RelativePath) && !Path.IsPathRooted(mediaItem.Media.RelativePath))
            {
                mediaItem.Media.RelativePath = Path.Combine(profileRoot, mediaItem.Media.RelativePath);
            }
        }
    }

    private static void HydratePaths(IReadOnlyList<ArchivedPostRecord> posts, string profileRoot)
    {
        foreach (ArchivedPostRecord post in posts)
        {
            if (!string.IsNullOrWhiteSpace(post.MetadataRelativePath) && !Path.IsPathRooted(post.MetadataRelativePath))
            {
                post.MetadataRelativePath = Path.Combine(profileRoot, post.MetadataRelativePath);
            }

            if (!string.IsNullOrWhiteSpace(post.TextRelativePath) && !Path.IsPathRooted(post.TextRelativePath))
            {
                post.TextRelativePath = Path.Combine(profileRoot, post.TextRelativePath);
            }

            foreach (ArchivedMediaRecord media in post.Media)
            {
                if (!string.IsNullOrWhiteSpace(media.RelativePath) && !Path.IsPathRooted(media.RelativePath))
                {
                    media.RelativePath = Path.Combine(profileRoot, media.RelativePath);
                }
            }
        }
    }
}
