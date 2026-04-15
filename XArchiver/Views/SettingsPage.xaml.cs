using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class SettingsPage : Page
{
    private readonly IResourceService _resourceService;
    private bool _isLoaded;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsPageViewModel>();
        _resourceService = App.GetService<IResourceService>();
        InitializeComponent();
    }

    public SettingsPageViewModel ViewModel { get; }

    private async void OnClearCredentialClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.DeleteCredentialAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

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
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnManageCredentialClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? token = await PromptForCredentialAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewModel.StatusMessage = _resourceService.GetString("StatusTokenRequired");
                return;
            }

            await ViewModel.SaveCredentialAsync(token);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnSaveEstimatedRateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveEstimatedRateAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async Task<string?> PromptForCredentialAsync()
    {
        PasswordBox passwordBox = new()
        {
            PlaceholderText = _resourceService.GetString("DialogCredentialPlaceholder"),
        };

        TextBlock descriptionBlock = new()
        {
            Text = _resourceService.GetString("DialogCredentialDescription"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

        ContentDialog dialog = new()
        {
            CloseButtonText = _resourceService.GetString("DialogCancel"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    descriptionBlock,
                    passwordBox,
                },
            },
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = _resourceService.GetString("DialogCredentialPrimary"),
            Title = _resourceService.GetString("DialogCredentialTitle"),
            XamlRoot = XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? passwordBox.Password.Trim() : null;
    }
}
