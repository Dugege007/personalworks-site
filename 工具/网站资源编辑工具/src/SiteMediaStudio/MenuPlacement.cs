using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 弹出层以左上角为锚点向右下展开，不跟系统「菜单右对齐」。
/// </summary>
public static class MenuPlacement
{
    /// <summary>
    /// 顶栏下拉：弹出层左上角贴在该选项左下角。
    /// </summary>
    public static CustomPopupPlacementCallback BelowTarget { get; } = PlaceBelowTarget;

    /// <summary>
    /// 启动时压掉系统右对齐，并让所有右键菜单以点击点为左上角。
    /// </summary>
    public static void ApplyAppWide()
    {
        ForceLeftAlignment();
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.ContextMenuOpeningEvent,
            new ContextMenuEventHandler(OnContextMenuOpening),
            true);
    }

    /// <summary>
    /// 右键即将弹出时，把菜单左上角钉在指针位置。
    /// </summary>
    private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement owner || owner.ContextMenu is null)
        {
            return;
        }

        var menu = owner.ContextMenu;
        var pos = Mouse.GetPosition(owner);
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Custom;
        menu.HorizontalOffset = 0;
        menu.VerticalOffset = 0;
        menu.CustomPopupPlacementCallback = (_, _, _) =>
            new[] { new CustomPopupPlacement(pos, PopupPrimaryAxis.Vertical) };
    }

    /// <summary>
    /// 系统改回右对齐时再压一次。
    /// </summary>
    private static void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.MenuDropAlignment))
        {
            ForceLeftAlignment();
        }
    }

    /// <summary>
    /// 本机若打开菜单右对齐，WPF 会把弹出层右上角当锚点。
    /// </summary>
    private static void ForceLeftAlignment()
    {
        if (!SystemParameters.MenuDropAlignment)
        {
            return;
        }

        var field = typeof(SystemParameters).GetField(
            "_menuDropAlignment",
            BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, false);
    }

    /// <summary>
    /// 弹出层左上角对齐目标左下角。
    /// </summary>
    private static CustomPopupPlacement[] PlaceBelowTarget(Size popupSize, Size targetSize, Point offset)
    {
        return new[]
        {
            new CustomPopupPlacement(new Point(0, targetSize.Height), PopupPrimaryAxis.Vertical)
        };
    }
}
