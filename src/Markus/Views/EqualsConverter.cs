using System.Globalization;
using Avalonia.Data.Converters;

namespace Markus.Views;

internal sealed class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new EqualsConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is { } match)
        {
            return match;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
