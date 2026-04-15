using Microsoft.UI.Xaml;

namespace XArchiver.Services;

internal interface IWindowContext
{
    IntPtr GetWindowHandle();

    XamlRoot? GetXamlRoot();

    void SetWindow(Window window);
}
