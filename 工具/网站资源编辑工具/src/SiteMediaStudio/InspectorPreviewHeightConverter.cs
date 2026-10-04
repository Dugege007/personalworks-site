using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 按检视栏实际尺寸与图片比例计算预览高度。
/// </summary>
public sealed class InspectorPreviewHeightConverter : IMultiValueConverter
{
    private const double HorizontalMargin = 32;
    private const double HeightRatio = 0.50;

    /// <summary>
    /// 图片宽度跟随检视栏，高度不超过窗口客户区（<c>RootDock</c>）高度的 50%。
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3
            || values[0] is not double paneWidth
            || values[1] is not double paneHeight
            || values[2] is not BitmapSource bitmap
            || paneWidth <= HorizontalMargin
            || paneHeight <= 0
            || bitmap.PixelWidth <= 0)
        {
            return 0.0;
        }

        var contentWidth = paneWidth - HorizontalMargin;
        var imageHeight = contentWidth * bitmap.PixelHeight / bitmap.PixelWidth;
        return Math.Min(imageHeight, paneHeight * HeightRatio);
    }

    /// <summary>
    /// 预览高度为只读派生值，不支持反向写入。
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 按检视列写入宽度计算预览区域宽度。
/// </summary>
public sealed class InspectorPreviewWidthConverter : IValueConverter
{
    private const double HorizontalMargin = 32;

    /// <summary>
    /// 扣除检视区域左右外边距。
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double columnWidth
            ? Math.Max(0, columnWidth - HorizontalMargin)
            : 0.0;
    }

    /// <summary>
    /// 预览宽度为只读派生值，不支持反向写入。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
