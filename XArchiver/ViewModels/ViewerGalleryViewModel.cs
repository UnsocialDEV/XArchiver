using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ViewerGalleryViewModel : ObservableObject
{
    private readonly IVideoThumbnailCache _thumbnailCache;
    private CancellationTokenSource? _thumbnailLoadCancellation;
    private IReadOnlyList<ViewerGalleryItemViewModel> _items = [];
    private double _previewHeight = 128;
    private ViewerGalleryItemViewModel? _selectedItem;
    private double _tileWidth = 168;

    public ViewerGalleryViewModel(IVideoThumbnailCache thumbnailCache)
    {
        _thumbnailCache = thumbnailCache;
    }

    public IReadOnlyList<ViewerGalleryItemViewModel> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public double PreviewHeight
    {
        get => _previewHeight;
        set => SetProperty(ref _previewHeight, value);
    }

    public ViewerGalleryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public double TileWidth
    {
        get => _tileWidth;
        set => SetProperty(ref _tileWidth, value);
    }

    public void Clear()
    {
        _thumbnailLoadCancellation?.Cancel();
        _thumbnailLoadCancellation?.Dispose();
        _thumbnailLoadCancellation = null;
        Items = [];
        SelectedItem = null;
    }

    public void Load(IReadOnlyList<ArchivedGalleryMediaRecord> items)
    {
        _thumbnailLoadCancellation?.Cancel();
        _thumbnailLoadCancellation?.Dispose();
        _thumbnailLoadCancellation = new CancellationTokenSource();

        List<ViewerGalleryItemViewModel> galleryItems = items.Select(item => new ViewerGalleryItemViewModel(item)).ToList();
        Items = galleryItems;
        SelectedItem = Items.FirstOrDefault();
        _ = WarmVideoThumbnailsAsync(galleryItems, _thumbnailLoadCancellation.Token);
    }

    public void UpdateLayout(double availableWidth)
    {
        double usableWidth = Math.Max(availableWidth - 16, 160);
        int columnCount = Math.Max(1, (int)Math.Floor((usableWidth + 12) / 180));
        double width = Math.Clamp((usableWidth - ((columnCount - 1) * 12)) / columnCount, 140, 220);

        TileWidth = width;
        PreviewHeight = Math.Clamp(width * 0.72, 120, 180);
    }

    private async Task WarmVideoThumbnailsAsync(IReadOnlyList<ViewerGalleryItemViewModel> items, CancellationToken cancellationToken)
    {
        using SemaphoreSlim concurrencyGate = new(2, 2);
        List<Task> thumbnailTasks = [];

        foreach (ViewerGalleryItemViewModel item in items)
        {
            if (item.Item.Media.Kind != ArchiveMediaKind.Video)
            {
                continue;
            }

            thumbnailTasks.Add(LoadVideoThumbnailAsync(item, concurrencyGate, cancellationToken));
        }

        try
        {
            await Task.WhenAll(thumbnailTasks);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadVideoThumbnailAsync(
        ViewerGalleryItemViewModel item,
        SemaphoreSlim concurrencyGate,
        CancellationToken cancellationToken)
    {
        await concurrencyGate.WaitAsync(cancellationToken);
        try
        {
            await item.LoadVideoThumbnailAsync(_thumbnailCache, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            concurrencyGate.Release();
        }
    }
}
