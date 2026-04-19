using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VPMS.Models;

namespace VPMS.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s == "Invert";
        bool bVal = value is bool b && b;
        if (invert) bVal = !bVal;
        // Also handle non-null / non-zero as truthy
        if (value is int i) bVal = invert ? i == 0 : i > 0;
        if (value is string str) bVal = invert ? string.IsNullOrEmpty(str) : !string.IsNullOrEmpty(str);
        return bVal ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class PercentageToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;
        if (values[0] is double pct && values[1] is double totalWidth)
            return Math.Clamp(pct / 100.0 * totalWidth, 0, totalWidth);
        return 0.0;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IssueSeverity sev)
        {
            return sev switch
            {
                IssueSeverity.Critical => new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28)),
                IssueSeverity.High => new SolidColorBrush(Color.FromRgb(0xE8, 0x5D, 0x04)),
                IssueSeverity.Medium => new SolidColorBrush(Color.FromRgb(0xF4, 0xA2, 0x61)),
                IssueSeverity.Low => new SolidColorBrush(Color.FromRgb(0x2D, 0x9E, 0x5F)),
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class HealthScoreToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double score)
        {
            return score >= 80
                ? new SolidColorBrush(Color.FromRgb(0x2D, 0x9E, 0x5F))
                : score >= 60
                    ? new SolidColorBrush(Color.FromRgb(0xF4, 0xA2, 0x61))
                    : score >= 40
                        ? new SolidColorBrush(Color.FromRgb(0xE8, 0x5D, 0x04))
                        : new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28));
        }
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class RiskToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double risk)
        {
            return risk >= 0.7
                ? new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28))
                : risk >= 0.4
                    ? new SolidColorBrush(Color.FromRgb(0xE8, 0x5D, 0x04))
                    : risk >= 0.2
                        ? new SolidColorBrush(Color.FromRgb(0xF4, 0xA2, 0x61))
                        : new SolidColorBrush(Color.FromRgb(0x2D, 0x9E, 0x5F));
        }
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class RiskToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double risk && parameter is string maxStr && double.TryParse(maxStr, out double max))
            return Math.Clamp(risk * max, 0, max);
        if (value is double r)
            return Math.Clamp(r * 200, 0, 200);
        return 0.0;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? (b ? "Yes" : "No") : "—";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && s.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

public class DoubleToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? $"{d * 100:F0}%" : "—";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ActionPriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ActionPriority p)
        {
            return p switch
            {
                ActionPriority.Immediate => new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28)),
                ActionPriority.Scheduled => new SolidColorBrush(Color.FromRgb(0xF4, 0xA2, 0x61)),
                ActionPriority.Monitor => new SolidColorBrush(Color.FromRgb(0x2D, 0x9E, 0x5F)),
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
