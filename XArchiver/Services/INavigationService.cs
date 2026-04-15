namespace XArchiver.Services;

internal interface INavigationService
{
    event EventHandler<NavigationRequestedEventArgs>? NavigationRequested;

    void NavigateTo(string pageKey, object? parameter = null);
}
