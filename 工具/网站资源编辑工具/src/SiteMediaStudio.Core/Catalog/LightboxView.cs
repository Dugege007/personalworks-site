namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 灯箱绝对缩放、适配、平移与小地图计算。
/// </summary>
public static class LightboxView
{
    public const double DefaultScale = 1;
    public const double MinScale = 0.1;
    public const double MaxScale = 8;
    public const double WheelStep = 1.15;
    public const double MiniMaxWidth = 160;
    public const double MiniMaxHeight = 120;

    public static readonly int[] PresetPercents = { 25, 50, 75, 100, 150, 200, 400 };

    /// <summary>
    /// 将绝对缩放夹到 10%～800%；非法值回落 100%。
    /// </summary>
    public static double ClampScale(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return DefaultScale;
        }

        return Math.Clamp(scale, MinScale, MaxScale);
    }

    /// <summary>
    /// 视口相对原图像素的 contain 比例。
    /// </summary>
    public static double ContainRatio(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return DefaultScale;
        }

        var boxWidth = viewportWidth > 0 ? viewportWidth : imageWidth;
        var boxHeight = viewportHeight > 0 ? viewportHeight : imageHeight;
        var ratio = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
        if (ratio <= 0 || double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return DefaultScale;
        }

        return ratio;
    }

    /// <summary>
    /// 适应窗口时用 contain；否则用夹紧后的绝对缩放。
    /// </summary>
    public static double EffectiveScale(bool isFit, double absoluteScale, double containRatio)
    {
        return isFit ? containRatio : ClampScale(absoluteScale);
    }

    /// <summary>
    /// 按滚轮步进绝对缩放。WPF 的 Delta 通常为 ±120。
    /// </summary>
    public static double ApplyWheel(double scale, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return ClampScale(scale);
        }

        var steps = wheelDelta / 120.0;
        return ClampScale(scale * Math.Pow(WheelStep, steps));
    }

    /// <summary>
    /// 按绝对缩放得到显示宽高。
    /// </summary>
    public static LightboxFit DisplaySize(double imageWidth, double imageHeight, double absoluteScale)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return LightboxFit.Empty;
        }

        var scale = ClampScale(absoluteScale);
        return new LightboxFit(imageWidth * scale, imageHeight * scale);
    }

    /// <summary>
    /// 显示文字：适应时带 contain 百分比，否则为绝对百分比。
    /// </summary>
    public static string FormatZoomLabel(bool isFit, double effectiveScale)
    {
        var percent = Math.Round(effectiveScale * 100);
        return isFit ? $"适应 {percent:0}%" : $"{percent:0}%";
    }

    /// <summary>
    /// 绕视口内锚点缩放后的平移，使该点对应的图像位置保持不动。
    /// </summary>
    public static LightboxPan ZoomAtCursor(
        double panX,
        double panY,
        double oldScale,
        double newScale,
        double cursorX,
        double cursorY,
        double viewportWidth,
        double viewportHeight,
        double newDisplayWidth,
        double newDisplayHeight)
    {
        if (oldScale <= 0 || double.IsNaN(oldScale) || double.IsInfinity(oldScale))
        {
            return ClampPan(panX, panY, newDisplayWidth, newDisplayHeight, viewportWidth, viewportHeight);
        }

        var factor = newScale / oldScale;
        var centerX = viewportWidth / 2 + panX;
        var centerY = viewportHeight / 2 + panY;
        var nextX = panX + (cursorX - centerX) * (1 - factor);
        var nextY = panY + (cursorY - centerY) * (1 - factor);
        return ClampPan(nextX, nextY, newDisplayWidth, newDisplayHeight, viewportWidth, viewportHeight);
    }

    /// <summary>
    /// 平移夹在可露出范围内；图小于视口时归零居中。
    /// </summary>
    public static LightboxPan ClampPan(
        double panX,
        double panY,
        double displayWidth,
        double displayHeight,
        double viewportWidth,
        double viewportHeight)
    {
        var maxX = Math.Max(0, (displayWidth - viewportWidth) / 2);
        var maxY = Math.Max(0, (displayHeight - viewportHeight) / 2);
        return new LightboxPan(Math.Clamp(panX, -maxX, maxX), Math.Clamp(panY, -maxY, maxY));
    }

    /// <summary>
    /// 图是否大于视口，从而允许拖移。
    /// </summary>
    public static bool CanPan(double displayWidth, double displayHeight, double viewportWidth, double viewportHeight)
    {
        return displayWidth > viewportWidth + 0.5 || displayHeight > viewportHeight + 0.5;
    }

    /// <summary>
    /// 灯箱打开且非适应窗口、小地图有尺寸时才显示。
    /// </summary>
    public static bool ShouldShowMinimap(bool isOpen, bool isFit, double miniWidth)
    {
        return isOpen && !isFit && miniWidth > 0;
    }

    /// <summary>
    /// 小地图外框与当前视口框（相对小地图左上）。
    /// </summary>
    public static LightboxMinimap Minimap(
        double displayWidth,
        double displayHeight,
        double viewportWidth,
        double viewportHeight,
        double panX,
        double panY,
        double imageWidth,
        double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || displayWidth <= 0 || displayHeight <= 0)
        {
            return LightboxMinimap.Empty;
        }

        var miniScale = Math.Min(MiniMaxWidth / imageWidth, MiniMaxHeight / imageHeight);
        var miniWidth = imageWidth * miniScale;
        var miniHeight = imageHeight * miniScale;
        var imageLeft = (viewportWidth - displayWidth) / 2 + panX;
        var imageTop = (viewportHeight - displayHeight) / 2 + panY;
        var visibleLeft = Math.Max(0, -imageLeft);
        var visibleTop = Math.Max(0, -imageTop);
        var visibleRight = Math.Min(displayWidth, viewportWidth - imageLeft);
        var visibleBottom = Math.Min(displayHeight, viewportHeight - imageTop);
        var scaleX = miniWidth / displayWidth;
        var scaleY = miniHeight / displayHeight;
        return new LightboxMinimap(
            miniWidth,
            miniHeight,
            visibleLeft * scaleX,
            visibleTop * scaleY,
            Math.Max(0, visibleRight - visibleLeft) * scaleX,
            Math.Max(0, visibleBottom - visibleTop) * scaleY);
    }

    /// <summary>
    /// 把小地图点击点对应的图像移到视口中心。
    /// </summary>
    public static LightboxPan PanToMinimap(
        double miniX,
        double miniY,
        double miniWidth,
        double miniHeight,
        double displayWidth,
        double displayHeight,
        double viewportWidth,
        double viewportHeight)
    {
        var displayX = miniWidth <= 0 ? displayWidth / 2 : miniX / miniWidth * displayWidth;
        var displayY = miniHeight <= 0 ? displayHeight / 2 : miniY / miniHeight * displayHeight;
        return ClampPan(
            displayWidth / 2 - displayX,
            displayHeight / 2 - displayY,
            displayWidth,
            displayHeight,
            viewportWidth,
            viewportHeight);
    }
}

/// <summary>
/// 灯箱显示宽高。
/// </summary>
public readonly record struct LightboxFit(double Width, double Height)
{
    public static LightboxFit Empty { get; } = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// 相对居中位置的平移。
/// </summary>
public readonly record struct LightboxPan(double X, double Y);

/// <summary>
/// 小地图尺寸与视口框。
/// </summary>
public readonly record struct LightboxMinimap(
    double Width,
    double Height,
    double ViewLeft,
    double ViewTop,
    double ViewWidth,
    double ViewHeight)
{
    public static LightboxMinimap Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
