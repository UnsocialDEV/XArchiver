using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class SyncsPage : Page
{
    private readonly IResourceService _resourceService;

    public SyncsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SyncsPageViewModel>();
        DataContext = ViewModel;
        _resourceService = App.GetService<IResourceService>();
    }

    public SyncsPageViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
    }

    private async void OnStartSessionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid sessionId)
        {
            return;
        }

        try
        {
            await ViewModel.StartAsync(sessionId);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnPauseSessionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid sessionId)
        {
            return;
        }

        try
        {
            ViewModel.Pause(sessionId);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnStopSessionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid sessionId)
        {
            return;
        }

        try
        {
            ViewModel.Stop(sessionId);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
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
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }
}
