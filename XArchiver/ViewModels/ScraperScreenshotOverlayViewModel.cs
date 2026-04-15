using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace XArchiver.ViewModels;

public sealed class ScraperScreenshotOverlayViewModel : ObservableObject
{
    private string _currentScreenshotPath = string.Empty;
    private bool _isOpen;
    private double _previewMaxHeight = 420;
    private double _viewportHeight = 720;
    private double _viewportWidth = 1280;

    public string CurrentScreenshotPath
    {
        get => _currentScreenshotPath;
        private set => SetProperty(ref _currentScreenshotPath, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public Visibility OverlayVisibility => IsOpen ? Visibility.Visible : Visibility.Collapsed;

    public string OverlayStatusText => "Fit to screen. Use zoom to inspect the captured screenshot at full resolution.";

    public double PreviewMaxHeight
    {
        get => _previewMaxHeight;
        private set => SetProperty(ref _previewMaxHeight, value);
    }

    public double ViewportHeight
    {
        get => _viewportHeight;
        private set => SetProperty(ref _viewportHeight, value);
    }

    public double ViewportWidth
    {
        get => _viewportWidth;
        private set => SetProperty(ref _viewportWidth, value);
    }

    public void Close()
    {
        CurrentScreenshotPath = string.Empty;
        IsOpen = false;
        NotifyStateChanged();
    }

    public void Open(string screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath))
        {
            return;
        }

        CurrentScreenshotPath = screenshotPath;
        IsOpen = true;
        NotifyStateChanged();
    }

    public void UpdateLayout(double width, double height)
    {
        double constrainedWidth = Math.Max(320, width);
        double constrainedHeight = Math.Max(240, height);

        ViewportWidth = constrainedWidth;
        ViewportHeight = constrainedHeight;
        PreviewMaxHeight = Math.Clamp(constrainedHeight * 0.32, 240, 520);
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(OverlayStatusText));
        OnPropertyChanged(nameof(OverlayVisibility));
        OnPropertyChanged(nameof(ViewportHeight));
        OnPropertyChanged(nameof(ViewportWidth));
    }
}
