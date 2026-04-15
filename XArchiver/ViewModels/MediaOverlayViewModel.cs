using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class MediaOverlayViewModel : ObservableObject
{
    private int _currentIndex = -1;
    private bool _isOpen;
    private readonly List<ArchivedMediaRecord> _mediaItems = [];
    private double _viewportHeight = 720;
    private double _viewportWidth = 1280;

    public bool CanMoveNext => _currentIndex >= 0 && _currentIndex < _mediaItems.Count - 1;

    public bool CanMovePrevious => _currentIndex > 0;

    public ArchivedMediaRecord? CurrentMedia => _currentIndex >= 0 && _currentIndex < _mediaItems.Count ? _mediaItems[_currentIndex] : null;

    public string CurrentMediaPath => CurrentMedia?.RelativePath ?? string.Empty;

    public string CurrentTitle => CurrentMedia is null ? string.Empty : $"{CurrentMedia.PostId} · {CurrentMedia.MediaKey}";

    public Visibility ImageVisibility => CurrentMedia is not null && (CurrentMedia.Kind == ArchiveMediaKind.Image || CurrentMedia.IsPartial) ? Visibility.Visible : Visibility.Collapsed;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public Visibility OverlayVisibility => IsOpen ? Visibility.Visible : Visibility.Collapsed;

    public string OverlayStatusText
    {
        get
        {
            if (CurrentMedia?.IsPartial == true)
            {
                return "Showing the archived preview image because the original video was not downloadable.";
            }

            return ImageVisibility == Visibility.Visible
                ? "Fit to screen. Use zoom to inspect the archived image at full resolution."
                : string.Empty;
        }
    }

    public double ViewportHeight
    {
        get => _viewportHeight;
        set => SetProperty(ref _viewportHeight, value);
    }

    public double ViewportWidth
    {
        get => _viewportWidth;
        set => SetProperty(ref _viewportWidth, value);
    }

    public Visibility VideoVisibility => CurrentMedia is not null && CurrentMedia.Kind == ArchiveMediaKind.Video && !CurrentMedia.IsPartial ? Visibility.Visible : Visibility.Collapsed;

    public void Close()
    {
        _mediaItems.Clear();
        _currentIndex = -1;
        IsOpen = false;
        NotifyStateChanged();
    }

    public void MoveNext()
    {
        if (!CanMoveNext)
        {
            return;
        }

        _currentIndex++;
        NotifyStateChanged();
    }

    public void MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return;
        }

        _currentIndex--;
        NotifyStateChanged();
    }

    public void Open(IReadOnlyList<ArchivedMediaRecord> mediaItems, ArchivedMediaRecord selectedMedia)
    {
        _mediaItems.Clear();
        _mediaItems.AddRange(mediaItems);
        _currentIndex = _mediaItems.FindIndex(media => string.Equals(media.MediaKey, selectedMedia.MediaKey, StringComparison.Ordinal));
        if (_currentIndex < 0)
        {
            _currentIndex = 0;
        }

        IsOpen = _mediaItems.Count > 0;
        NotifyStateChanged();
    }

    public void UpdateViewport(double width, double height)
    {
        double constrainedWidth = Math.Max(320, width);
        double constrainedHeight = Math.Max(240, height);

        bool changed = false;
        if (!double.IsNaN(constrainedWidth) && Math.Abs(_viewportWidth - constrainedWidth) > 0.5)
        {
            _viewportWidth = constrainedWidth;
            OnPropertyChanged(nameof(ViewportWidth));
            changed = true;
        }

        if (!double.IsNaN(constrainedHeight) && Math.Abs(_viewportHeight - constrainedHeight) > 0.5)
        {
            _viewportHeight = constrainedHeight;
            OnPropertyChanged(nameof(ViewportHeight));
            changed = true;
        }

        if (changed)
        {
            OnPropertyChanged(nameof(OverlayStatusText));
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanMoveNext));
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CurrentMedia));
        OnPropertyChanged(nameof(CurrentMediaPath));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(ImageVisibility));
        OnPropertyChanged(nameof(OverlayVisibility));
        OnPropertyChanged(nameof(OverlayStatusText));
        OnPropertyChanged(nameof(ViewportHeight));
        OnPropertyChanged(nameof(ViewportWidth));
        OnPropertyChanged(nameof(VideoVisibility));
    }
}
