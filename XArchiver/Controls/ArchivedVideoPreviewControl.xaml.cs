using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using XArchiver.Services;

namespace XArchiver.Controls;

public sealed partial class ArchivedVideoPreviewControl : UserControl
{
    public static readonly DependencyProperty MediaPathProperty = DependencyProperty.Register(
        nameof(MediaPath),
        typeof(string),
        typeof(ArchivedVideoPreviewControl),
        new PropertyMetadata(string.Empty, OnMediaPathChanged));

    private readonly IVideoThumbnailCache _thumbnailCache;
    private CancellationTokenSource? _thumbnailLoadCancellation;

    public ArchivedVideoPreviewControl()
    {
        _thumbnailCache = App.GetService<IVideoThumbnailCache>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string MediaPath
    {
        get => (string)GetValue(MediaPathProperty);
        set => SetValue(MediaPathProperty, value);
    }

    private static void OnMediaPathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ArchivedVideoPreviewControl control)
        {
            _ = control.HandleMediaPathChangedAsync();
        }
    }

    private async Task HandleMediaPathChangedAsync()
    {
        await LoadThumbnailAsync();
    }

    private async Task LoadThumbnailAsync()
    {
        _thumbnailLoadCancellation?.Cancel();
        _thumbnailLoadCancellation?.Dispose();
        _thumbnailLoadCancellation = new CancellationTokenSource();

        string path = MediaPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            ThumbnailImage.Source = null;
            ThumbnailImage.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            string? thumbnailPath = await _thumbnailCache.GetThumbnailPathAsync(path, _thumbnailLoadCancellation.Token);
            if (_thumbnailLoadCancellation.IsCancellationRequested || !string.Equals(path, MediaPath, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
            {
                ThumbnailImage.Source = new BitmapImage(new Uri(thumbnailPath));
                ThumbnailImage.Visibility = Visibility.Visible;
            }
            else
            {
                ThumbnailImage.Source = null;
                ThumbnailImage.Visibility = Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ThumbnailImage.Source is null && !string.IsNullOrWhiteSpace(MediaPath))
        {
            _ = LoadThumbnailAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _thumbnailLoadCancellation?.Cancel();
        _thumbnailLoadCancellation?.Dispose();
        _thumbnailLoadCancellation = null;
        PlayOverlay.Visibility = Visibility.Visible;
    }
}
