using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 布尔值转可见性；ConverterParameter=invert 时取反。
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// true 显示，false 折叠。
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 可见性转回布尔值。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
