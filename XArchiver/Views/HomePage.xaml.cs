using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class HomePage : Page
{
    private readonly INavigationService _navigationService;
    private readonly IResourceService _resourceService;
    private bool _isLoaded;

    public HomePage()
    {
        InitializeComponent();
        ViewModel = App.GetService<HomePageViewModel>();
        _navigationService = App.GetService<INavigationService>();
        _resourceService = App.GetService<IResourceService>();
    }

    public HomePageViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoaded)
            {
                await ViewModel.InitializeAsync();
                return;
            }

            _isLoaded = true;
            await ViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnOpenProfilesClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Profiles");
    }

    private void OnOpenScraperClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Scraper");
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Settings");
    }

    private void OnOpenSyncsClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Syncs");
    }

    private void OnOpenViewerClick(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Viewer");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
        _isLoaded = false;
    }
}
