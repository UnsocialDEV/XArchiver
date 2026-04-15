namespace XArchiver.Services;

internal sealed class NavigationRequestedEventArgs : EventArgs
{
    public NavigationRequestedEventArgs(string pageKey, object? parameter)
    {
        PageKey = pageKey;
        Parameter = parameter;
    }

    public string PageKey { get; }

    public object? Parameter { get; }
}
