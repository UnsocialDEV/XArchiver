namespace XArchiver.Services;

internal sealed class NavigationService : INavigationService
{
    public event EventHandler<NavigationRequestedEventArgs>? NavigationRequested;

    public void NavigateTo(string pageKey, object? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
        {
            return;
        }

        NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs(pageKey, parameter));
    }
}
