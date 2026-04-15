using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace XArchiver.Services;

internal sealed class ResourceService : IResourceService
{
    private readonly ResourceLoader _resourceLoader = new();

    public string Format(string key, params object[] arguments)
    {
        string format = GetString(key);
        return string.Format(CultureInfo.CurrentCulture, format, arguments);
    }

    public string GetString(string key)
    {
        string value = _resourceLoader.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }
}
