using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed partial class ViewerGalleryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _thumbnailPath = string.Empty;

    public ViewerGalleryItemViewModel(ArchivedGalleryMediaRecord item)
    {
        Item = item;

        if (Item.Media.Kind == ArchiveMediaKind.Image && !string.IsNullOrWhiteSpace(Item.Media.RelativePath))
        {
            ThumbnailPath = Item.Media.RelativePath;
        }
    }

    public string CreatedAtText => Item.CreatedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    public string KindText => Item.Media.Kind == ArchiveMediaKind.Image ? "Image" : "Video";

    public ArchivedGalleryMediaRecord Item { get; }

    public string MediaPath => Item.Media.RelativePath ?? string.Empty;

    public Visibility PartialBadgeVisibility => Item.Media.IsPartial ? Visibility.Visible : Visibility.Collapsed;

    public string PartialStatusText => Item.Media.IsPartial ? "Partial preview" : string.Empty;

    public Visibility ThumbnailVisibility => string.IsNullOrWhiteSpace(ThumbnailPath) ? Visibility.Collapsed : Visibility.Visible;

    public string UsernameText => $"@{Item.Username}";

    public Visibility VideoTileVisibility => Item.Media.Kind == ArchiveMediaKind.Video ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadVideoThumbnailAsync(IVideoThumbnailCache thumbnailCache, CancellationToken cancellationToken)
    {
        if (Item.Media.Kind != ArchiveMediaKind.Video || string.IsNullOrWhiteSpace(MediaPath))
        {
            return;
        }

        string? thumbnailPath = await thumbnailCache.GetThumbnailPathAsync(MediaPath, cancellationToken);
        if (cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(thumbnailPath))
        {
            return;
        }

        ThumbnailPath = thumbnailPath;
        OnPropertyChanged(nameof(ThumbnailVisibility));
    }

    partial void OnThumbnailPathChanged(string value)
    {
        OnPropertyChanged(nameof(ThumbnailVisibility));
    }
}
