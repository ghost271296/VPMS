using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VPMS.Models;

namespace VPMS.Converters;

/// <summary>
/// Collapses the element unless the bound InputType matches the parameter.
/// Parameter values: "Boolean", "Text", "Numeric", "Date", "MultiChoice"
/// </summary>
public class InputTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is QueryInputType qt && parameter is string param)
        {
            if (param == "Text" && qt is QueryInputType.Text or QueryInputType.Numeric)
                return Visibility.Visible;
            return qt.ToString().Equals(param, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
