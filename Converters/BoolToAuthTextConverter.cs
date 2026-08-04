using System.Globalization;

namespace MAUI_IOT.Converters;

public sealed class BoolToAuthTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAuthenticated)
            return isAuthenticated ? "Trusted" : "Unknown";
        return "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
