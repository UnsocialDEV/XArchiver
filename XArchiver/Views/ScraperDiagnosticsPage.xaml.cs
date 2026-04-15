using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class ScraperDiagnosticsPage : Page
{
    private Guid? _selectedProfileId;

    public ScraperDiagnosticsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<ScraperPageViewModel>();
        DataContext = ViewModel;
    }

    public ScraperPageViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _selectedProfileId = e.Parameter is Guid profileId ? profileId : null;
    }

    private void OnCloseScreenshotClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseScreenshotOverlay();
    }

    private void OnForceKillScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.ForceKillScrape();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync(_selectedProfileId);
            UpdatePageLayout();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnOpenLiveBrowserClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenLiveBrowser();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnOpenLoginBrowserClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.OpenLoginBrowserAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnOpenScreenshotClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenScreenshotOverlay();
        ResetOverlayScreenshotView();
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.ScreenshotOverlay.IsOpen)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseScreenshotOverlay();
            e.Handled = true;
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageLayout();
    }

    private void OnPauseScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.PauseScrape();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnResetSessionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.ResetSessionAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnResumeScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.ResumeScrape();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnScheduleScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.ScheduleScrapeAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnStartScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.StartScrapeAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnStopScrapeAndSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.StopScrapeAndSave();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnStopScrapeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.StopScrape();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private async void OnRemoveScheduledRunClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid runId)
        {
            return;
        }

        try
        {
            await ViewModel.RemoveScheduledRunAsync(runId);
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private async void OnValidateSessionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.ValidateSessionAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportUnexpectedError(exception.Message);
        }
    }

    private void ResetOverlayScreenshotView()
    {
        OverlayScreenshotScrollViewer?.ChangeView(0, 0, 1f, true);
    }

    private void UpdatePageLayout()
    {
        ViewModel.UpdatePageLayout(ActualWidth, ActualHeight);
    }
}
