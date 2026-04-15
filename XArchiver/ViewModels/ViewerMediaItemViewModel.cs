using Microsoft.UI.Xaml;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ViewerMediaItemViewModel
{
    public ViewerMediaItemViewModel(ArchivedMediaRecord media, ArchivedMediaDetailRecord? details)
    {
        Media = media;
        Details = details;
    }

    public ArchivedMediaDetailRecord? Details { get; }

    public string DimensionsText
    {
        get
        {
            if (Media.Width is null || Media.Height is null)
            {
                return "Unknown";
            }

            return $"{Media.Width} x {Media.Height}";
        }
    }

    public string DurationText => Media.DurationMs is null ? "Unknown" : TimeSpan.FromMilliseconds(Media.DurationMs.Value).ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    public string KindText => Media.Kind == ArchiveMediaKind.Image ? "Image" : Media.IsPartial ? "Video preview" : "Video";

    public Visibility LiveVideoPreviewVisibility => Media.Kind == ArchiveMediaKind.Video && !Media.IsPartial ? Visibility.Visible : Visibility.Collapsed;

    public ArchivedMediaRecord Media { get; }

    public string MediaPath => Media.RelativePath ?? string.Empty;

    public string MediaKeyText => Media.MediaKey;

    public string PartialStatusText => Media.IsPartial ? "Partial preview only" : "Fully downloaded";

    public string PreviewImagePath => Media.IsPartial ? Media.RelativePath ?? string.Empty : string.Empty;

    public string SourceUrlText => Media.SourceUrl;

    public Visibility ThumbnailVisibility => Media.Kind == ArchiveMediaKind.Image || Media.IsPartial ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VideoTileVisibility => Media.Kind == ArchiveMediaKind.Video && Media.IsPartial ? Visibility.Visible : Visibility.Collapsed;
}
