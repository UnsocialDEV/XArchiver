using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver.Views;

public sealed partial class ProfilesPage : Page
{
    private readonly IXCredentialStore _credentialStore;
    private readonly IFolderAccessService _folderAccessService;
    private readonly INavigationService _navigationService;
    private readonly IResourceService _resourceService;
    private readonly ISyncConfirmationFormatter _syncConfirmationFormatter;
    private bool _isLoaded;

    public ProfilesPage()
    {
        ViewModel = App.GetService<ProfilesPageViewModel>();
        _credentialStore = App.GetService<IXCredentialStore>();
        _folderAccessService = App.GetService<IFolderAccessService>();
        _navigationService = App.GetService<INavigationService>();
        _resourceService = App.GetService<IResourceService>();
        _syncConfirmationFormatter = App.GetService<ISyncConfirmationFormatter>();
        InitializeComponent();
    }

    public ProfilesPageViewModel ViewModel { get; }

    private async Task<bool> EnsureCredentialAsync()
    {
        if (await _credentialStore.HasCredentialAsync(CancellationToken.None))
        {
            return true;
        }

        string? token = await PromptForCredentialAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        await _credentialStore.SaveCredentialAsync(token, CancellationToken.None);
        return true;
    }

    private async void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? folderPath = await _folderAccessService.PickFolderAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                ViewModel.SetArchiveRootPath(folderPath);
            }
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedProfile is null)
            {
                return;
            }

            bool confirmed = await ShowDeleteConfirmationAsync(ViewModel.SelectedProfile);
            if (confirmed)
            {
                await ViewModel.DeleteSelectedAsync();
                SyncSourceSelection();
            }
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnImportArchivesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? folderPath = await _folderAccessService.PickFolderAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            await ViewModel.ImportPreviousArchivesAsync(folderPath);
            SyncSourceSelection();
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
                await ViewModel.InitializeAsync();
                SyncSourceSelection();
                return;
            }

            _isLoaded = true;
            await ViewModel.InitializeAsync();
            SyncSourceSelection();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        ViewModel.NewProfile();
        ProfilesListView.SelectedItem = null;
        SyncSourceSelection();
    }

    private async void OnOpenWebCaptureClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ArchiveProfile? profile = ViewModel.TryCreateDraftProfile();
            if (profile is null)
            {
                return;
            }

            await ViewModel.SaveDraftAsync(profile);
            _navigationService.NavigateTo("Scraper", profile.ProfileId);
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnProfileSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.Editor.PreferredSource = ProfileSourceComboBox.SelectedIndex == 1
            ? ArchiveSourceKind.WebCapture
            : ArchiveSourceKind.Api;
    }

    private async void OnProfilesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await ViewModel.RefreshWorkspaceAsync();
            SyncSourceSelection();
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnQueueApiSyncClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await EnsureCredentialAsync())
            {
                ViewModel.StatusMessage = _resourceService.GetString("StatusSyncMissingCredential");
                return;
            }

            ApiSyncRequest? request = ViewModel.TryCreateApiSyncRequest();
            if (request is null)
            {
                return;
            }

            decimal? costPerThousandPostReads = await ViewModel.TryGetEstimatedCostRateAsync();
            if (costPerThousandPostReads is null)
            {
                return;
            }

            bool confirmed = await ShowSyncConfirmationAsync(request.Profile, costPerThousandPostReads.Value);
            if (!confirmed)
            {
                ViewModel.StatusMessage = _resourceService.GetString("StatusSyncCanceled");
                return;
            }

            await ViewModel.QueueSelectedAsync(request);
            _navigationService.NavigateTo("Syncs");
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnScheduleApiSyncClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ApiSyncRequest? request = ViewModel.TryCreateApiSyncRequest();
            if (request is null)
            {
                return;
            }

            if (!ViewModel.Timing.TryGetScheduledStartUtc(out DateTimeOffset? scheduledStartUtc, out string? validationError))
            {
                ViewModel.StatusMessage = validationError ?? "Choose a future date and time for the scheduled run.";
                return;
            }

            if (!scheduledStartUtc.HasValue)
            {
                ViewModel.StatusMessage = "Turn on Scheduled Start Time before scheduling an API sync.";
                return;
            }

            await ViewModel.ScheduleSelectedAsync(request, scheduledStartUtc.Value);
            _navigationService.NavigateTo("Syncs");
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnReviewArchiveSelectedClick(object sender, RoutedEventArgs e)
    {
        try
        {
            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewArchiveTitle"),
                ViewModel.Review.GetArchiveConfirmationText(),
                _resourceService.GetString("DialogReviewArchivePrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.Review.ArchiveSelectedAsync();
            ViewModel.StatusMessage = ViewModel.Review.StatusMessage;
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnReviewClearSelectionClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Review.ClearSelection();
    }

    private async void OnReviewLoadMoreClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await EnsureCredentialAsync())
            {
                return;
            }

            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewLoadTitle"),
                ViewModel.Review.GetPreviewConfirmationText(isLoadMore: true),
                _resourceService.GetString("DialogReviewLoadPrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.Review.LoadMoreAsync();
            ViewModel.StatusMessage = ViewModel.Review.StatusMessage;
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private async void OnReviewLoadRecentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ArchiveProfile? draft = ViewModel.TryCreateDraftProfile();
            if (draft is not null)
            {
                await ViewModel.SaveDraftAsync(draft);
            }

            if (!await EnsureCredentialAsync())
            {
                return;
            }

            bool confirmed = await ShowConfirmationDialogAsync(
                _resourceService.GetString("DialogReviewLoadTitle"),
                ViewModel.Review.GetPreviewConfirmationText(isLoadMore: false),
                _resourceService.GetString("DialogReviewLoadPrimary"));

            if (!confirmed)
            {
                return;
            }

            await ViewModel.Review.LoadRecentPostsAsync();
            ViewModel.StatusMessage = ViewModel.Review.StatusMessage;
        }
        catch (Exception exception)
        {
            ViewModel.StatusMessage = _resourceService.Format("StatusUnexpectedErrorFormat", exception.Message);
        }
    }

    private void OnReviewSelectAllVisibleClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Review.SelectAllVisible();
    }

    private async void OnSaveProfileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveAsync();
            SyncSourceSelection();
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

    private async Task<bool> ShowDeleteConfirmationAsync(ArchiveProfile profile)
    {
        ContentDialog dialog = new()
        {
            CloseButtonText = _resourceService.GetString("DialogCancel"),
            Content = new TextBlock
            {
                Text = _resourceService.Format("DialogDeleteMessageFormat", profile.Username),
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonText = _resourceService.GetString("DialogDeletePrimary"),
            Title = _resourceService.GetString("DialogDeleteTitle"),
            XamlRoot = XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowSyncConfirmationAsync(ArchiveProfile profile, decimal costPerThousandPostReads)
    {
        ContentDialog dialog = new()
        {
            CloseButtonText = _resourceService.GetString("DialogCancel"),
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new TextBlock
                {
                    Text = _syncConfirmationFormatter.FormatBody(profile, costPerThousandPostReads),
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            },
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = _resourceService.GetString("DialogSyncPrimary"),
            Title = _resourceService.GetString("DialogSyncTitle"),
            XamlRoot = XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private void SyncSourceSelection()
    {
        if (ProfileSourceComboBox is null)
        {
            return;
        }

        ProfileSourceComboBox.SelectedIndex = ViewModel.Editor.PreferredSource == ArchiveSourceKind.WebCapture ? 1 : 0;
    }
}
