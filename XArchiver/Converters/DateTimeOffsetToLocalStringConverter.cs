using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace XArchiver.Converters;

public sealed class DateTimeOffsetToLocalStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
