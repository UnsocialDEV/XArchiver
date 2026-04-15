using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XArchiver.Core.Interfaces;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class ReviewPage : Page
{
    private readonly IXCredentialStore _credentialStore;
    private readonly IResourceService _resourceService;

    public ReviewPage()
    {
        ViewModel = App.GetService<ReviewPageViewModel>();
        _credentialStore = App.GetService<IXCredentialStore>();
        _resourceService = App.GetService<IResourceService>();
        InitializeComponent();
    }

    public ReviewPageViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            Guid? selectedProfileId = e.Parameter is Guid profileId ? profileId : null;

            await ViewModel.InitializeAsync(selectedProfileId);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnArchiveSelectedClick(object sender, RoutedEventArgs e)
    {
        try
        {
            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewArchiveTitle"),
                ViewModel.GetArchiveConfirmationText(),
                _resourceService.GetString("DialogReviewArchivePrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.ArchiveSelectedAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnClearSelectionClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearSelection();
    }

    private async void OnLoadMoreClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await EnsureCredentialAsync())
            {
                return;
            }

            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewLoadTitle"),
                ViewModel.GetPreviewConfirmationText(isLoadMore: true),
                _resourceService.GetString("DialogReviewLoadPrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.LoadMoreAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnLoadRecentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await EnsureCredentialAsync())
            {
                return;
            }

            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewLoadTitle"),
                ViewModel.GetPreviewConfirmationText(isLoadMore: false),
                _resourceService.GetString("DialogReviewLoadPrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.LoadRecentPostsAsync();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnSelectAllVisibleClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAllVisible();
    }

    private async Task<bool> EnsureCredentialAsync()
    {
        if (await _credentialStore.HasCredentialAsync(CancellationToken.None))
        {
            return true;
        }

        ViewModel.StatusMessage = _resourceService.GetString("StatusSyncMissingCredential");
        return false;
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText)
    {
        ContentDialog dialog = new()
        {
            CloseButtonText = _resourceService.GetString("DialogCancel"),
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = primaryButtonText,
            Title = title,
            XamlRoot = XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
