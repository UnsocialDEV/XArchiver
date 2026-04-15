using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ArchiveProfileEditorViewModel : ObservableObject
{
    private const int MaximumPostsLimit = 3200;
    private const int MinimumPostsLimit = 5;

    private string _archiveRootPath = string.Empty;
    private bool _downloadImages = true;
    private bool _downloadVideos = true;
    private bool _includeOriginalPosts = true;
    private bool _includeQuotes = true;
    private bool _includeReplies = true;
    private bool _includeReposts = true;
    private int _maxPostsPerSync = 100;
    private int _maxPostsPerWebArchive = 100;
    private ArchiveSourceKind _preferredSource = ArchiveSourceKind.Api;
    private string _profileUrl = string.Empty;
    private string _username = string.Empty;

    public string ArchiveRootPath
    {
        get => _archiveRootPath;
        set => SetProperty(ref _archiveRootPath, value);
    }

    public bool DownloadImages
    {
        get => _downloadImages;
        set => SetProperty(ref _downloadImages, value);
    }

    public bool DownloadVideos
    {
        get => _downloadVideos;
        set => SetProperty(ref _downloadVideos, value);
    }

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

    public bool IsApiSource => PreferredSource == ArchiveSourceKind.Api;

    public bool IsWebCaptureSource => PreferredSource == ArchiveSourceKind.WebCapture;

    public int MaxPostsPerSync
    {
        get => _maxPostsPerSync;
        set => SetProperty(ref _maxPostsPerSync, value);
    }

    public int MaxPostsPerWebArchive
    {
        get => _maxPostsPerWebArchive;
        set => SetProperty(ref _maxPostsPerWebArchive, value);
    }

    public ArchiveSourceKind PreferredSource
    {
        get => _preferredSource;
        set
        {
            if (SetProperty(ref _preferredSource, value))
            {
                OnPropertyChanged(nameof(IsApiSource));
                OnPropertyChanged(nameof(IsWebCaptureSource));
            }
        }
    }

    public string ProfileUrl
    {
        get => _profileUrl;
        set => SetProperty(ref _profileUrl, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public void LoadProfile(ArchiveProfile? profile)
    {
        if (profile is null)
        {
            LoadNewProfile();
            return;
        }

        Username = profile.Username;
        ProfileUrl = profile.ProfileUrl ?? string.Empty;
        ArchiveRootPath = profile.ArchiveRootPath;
        PreferredSource = profile.PreferredSource;
        MaxPostsPerSync = profile.MaxPostsPerSync;
        MaxPostsPerWebArchive = profile.MaxPostsPerWebArchive;
        IncludeOriginalPosts = profile.IncludeOriginalPosts;
        IncludeReplies = profile.IncludeReplies;
        IncludeQuotes = profile.IncludeQuotes;
        IncludeReposts = profile.IncludeReposts;
        DownloadImages = profile.DownloadImages;
        DownloadVideos = profile.DownloadVideos;
    }

    public void LoadNewProfile()
    {
        Username = string.Empty;
        ProfileUrl = string.Empty;
        ArchiveRootPath = string.Empty;
        PreferredSource = ArchiveSourceKind.Api;
        MaxPostsPerSync = 100;
        MaxPostsPerWebArchive = 100;
        IncludeOriginalPosts = true;
        IncludeReplies = true;
        IncludeQuotes = true;
        IncludeReposts = true;
        DownloadImages = true;
        DownloadVideos = true;
    }

    public void SetArchiveRootPath(string path)
    {
        ArchiveRootPath = path;
    }

    public ArchiveProfile? TryCreateProfile(ArchiveProfile? existingProfile, out string? validationError)
    {
        string trimmedUsername = Username.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUsername))
        {
            validationError = "StatusProfileValidationUsername";
            return null;
        }

        if (string.IsNullOrWhiteSpace(ArchiveRootPath))
        {
            validationError = "StatusProfileValidationFolder";
            return null;
        }

        if (!HasAnySelectedPostTypes())
        {
            validationError = "StatusProfileValidationPostTypes";
            return null;
        }

        int maxPosts = PreferredSource == ArchiveSourceKind.Api ? MaxPostsPerSync : MaxPostsPerWebArchive;
        if (maxPosts < MinimumPostsLimit || maxPosts > MaximumPostsLimit)
        {
            validationError = "StatusProfileValidationMaxPostsFormat";
            return null;
        }

        validationError = null;
        return new ArchiveProfile
        {
            ArchiveRootPath = ArchiveRootPath.Trim(),
            DownloadImages = DownloadImages,
            DownloadVideos = DownloadVideos,
            IncludeOriginalPosts = IncludeOriginalPosts,
            IncludeQuotes = IncludeQuotes,
            IncludeReplies = IncludeReplies,
            IncludeReposts = IncludeReposts,
            LastSinceId = existingProfile?.LastSinceId,
            LastSuccessfulSyncUtc = existingProfile?.LastSuccessfulSyncUtc,
            MaxPostsPerSync = MaxPostsPerSync,
            MaxPostsPerWebArchive = MaxPostsPerWebArchive,
            PreferredSource = PreferredSource,
            ProfileId = existingProfile?.ProfileId ?? Guid.NewGuid(),
            ProfileUrl = string.IsNullOrWhiteSpace(ProfileUrl) ? null : ProfileUrl.Trim(),
            UserId = existingProfile?.UserId,
            Username = trimmedUsername,
        };
    }

    private bool HasAnySelectedPostTypes()
    {
        return IncludeOriginalPosts || IncludeReplies || IncludeQuotes || IncludeReposts;
    }
}
