using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XArchiver.Services;
using XArchiver.ViewModels;
using XArchiver.Views;

namespace XArchiver;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly Dictionary<string, Type> _pageMap = new(StringComparer.Ordinal)
    {
        ["Home"] = typeof(HomePage),
        ["Profiles"] = typeof(ProfilesPage),
        ["Scraper"] = typeof(ScraperDiagnosticsPage),
        ["Settings"] = typeof(SettingsPage),
        ["Syncs"] = typeof(SyncsPage),
        ["Viewer"] = typeof(ViewerPage),
    };

    public MainWindow()
    {
        ViewModel = App.GetService<MainWindowViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        ContentFrame.Navigated += OnContentFrameNavigated;
        _navigationService.NavigationRequested += OnNavigationRequested;
        Closed += OnClosed;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        HomeNavigationItem.IsSelected = true;
        NavigateToPage("Home");
    }

    public MainWindowViewModel ViewModel { get; }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _navigationService.NavigationRequested -= OnNavigationRequested;
        Closed -= OnClosed;
    }

    private void NavigateToPage(string pageKey, object? parameter = null)
    {
        if (_pageMap.TryGetValue(pageKey, out Type? pageType) && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, parameter);
        }
    }

    private void OnNavigationRequested(object? sender, NavigationRequestedEventArgs e)
    {
        NavigateToPage(e.PageKey, e.Parameter);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateToPage("Settings");
            return;
        }

        if (args.SelectedItemContainer?.Tag is string pageKey)
        {
            NavigateToPage(pageKey);
        }
    }

    private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(HomePage))
        {
            RootNavigationView.SelectedItem = HomeNavigationItem;
        }
        else if (e.SourcePageType == typeof(ProfilesPage))
        {
            RootNavigationView.SelectedItem = ProfilesNavigationItem;
        }
        else if (e.SourcePageType == typeof(ScraperDiagnosticsPage))
        {
            RootNavigationView.SelectedItem = ScraperNavigationItem;
        }
        else if (e.SourcePageType == typeof(SyncsPage))
        {
            RootNavigationView.SelectedItem = SyncsNavigationItem;
        }
        else if (e.SourcePageType == typeof(SettingsPage))
        {
            RootNavigationView.SelectedItem = RootNavigationView.SettingsItem;
        }
        else if (e.SourcePageType == typeof(ViewerPage))
        {
            RootNavigationView.SelectedItem = ViewerNavigationItem;
        }
    }
}
