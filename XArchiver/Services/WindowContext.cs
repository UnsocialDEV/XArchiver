using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace XArchiver.Services;

internal sealed class WindowContext : IWindowContext
{
    private Window? _window;

    public IntPtr GetWindowHandle()
    {
        return _window is null ? IntPtr.Zero : WindowNative.GetWindowHandle(_window);
    }

    public XamlRoot? GetXamlRoot()
    {
        return _window?.Content.XamlRoot;
    }

    public void SetWindow(Window window)
    {
        _window = window;
    }
}
