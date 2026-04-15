using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class ViewerPage : Page
{
    private readonly IResourceService _resourceService;
    private bool _isLoaded;

    public ViewerPage()
    {
        ViewModel = App.GetService<ViewerPageViewModel>();
        _resourceService = App.GetService<IResourceService>();
        InitializeComponent();
        ViewModel.MediaOverlay.PropertyChanged += OnMediaOverlayPropertyChanged;
        IsTabStop = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ViewerPageViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoaded)
            {
                return;
            }

            _isLoaded = true;
            await ViewModel.InitializeAsync();
            UpdateViewerLayoutMetrics();
            Focus(FocusState.Programmatic);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnGalleryItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not ViewerGalleryItemViewModel galleryItem)
            {
                return;
            }

            await ViewModel.OpenGalleryItemAsync(galleryItem);
            Focus(FocusState.Programmatic);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnMediaOverlayPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MediaOverlayViewModel.CurrentMediaPath) or nameof(MediaOverlayViewModel.IsOpen))
        {
            _ = DispatcherQueue.TryEnqueue(ResetOverlayImageView);
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewerLayoutMetrics();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.MediaOverlay.PropertyChanged -= OnMediaOverlayPropertyChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnOpenMediaClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewerMediaItemViewModel mediaItem })
        {
            return;
        }

        ViewModel.OpenMedia(mediaItem);
        Focus(FocusState.Programmatic);
    }

    private async void OnOpenMetadataFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.Details.OpenMetadataFileAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnOverlayCloseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseOverlay();
    }

    private void OnOverlayNextClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveOverlayNext();
    }

    private void OnOverlayPreviousClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveOverlayPrevious();
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.MediaOverlay.IsOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                ViewModel.CloseOverlay();
                e.Handled = true;
                break;
            case VirtualKey.Left:
                ViewModel.MoveOverlayPrevious();
                e.Handled = true;
                break;
            case VirtualKey.Right:
                ViewModel.MoveOverlayNext();
                e.Handled = true;
                break;
        }
    }

    private async void OnPostSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await ViewModel.SelectPostAsync(PostsListView.SelectedItem as ViewerPostItemViewModel);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            await ViewModel.RefreshAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.RefreshAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void ResetOverlayImageView()
    {
        if (OverlayImageScrollViewer is null)
        {
            return;
        }

        OverlayImageScrollViewer.ChangeView(0, 0, 1f, true);
    }

    private void UpdateViewerLayoutMetrics()
    {
        ViewModel.Details.UpdateMediaPreviewMaxHeight(ActualHeight);
        ViewModel.MediaOverlay.UpdateViewport(Math.Max(ActualWidth - 96, 320), Math.Max(ActualHeight - 192, 240));
        ViewModel.Gallery.UpdateLayout(Math.Max(ViewerDetailsPane.ActualWidth - 64, 160));
    }
}
