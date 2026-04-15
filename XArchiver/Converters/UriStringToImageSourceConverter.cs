using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XArchiver.Converters;

public sealed class UriStringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string uriText || string.IsNullOrWhiteSpace(uriText))
        {
            return null;
        }

        return Uri.TryCreate(uriText, UriKind.Absolute, out Uri? uri) ? new BitmapImage(uri) : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
