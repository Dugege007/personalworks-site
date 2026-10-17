using System.Windows;
using System.Windows.Controls;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 压缩、恢复或清理结束后的结果框。可以重试刚才因为文件被占用而跳过的项。
/// </summary>
public sealed class CompressResultWindow : Window
{
    private bool mRetry;

    private CompressResultWindow(string title, string summary, string body, bool canRetry)
    {
        Title = title;
        Width = 640;
        Height = 480;
        MinWidth = 420;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.CanResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var retry = new Button
        {
            Content = "重试",
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6),
            IsEnabled = canRetry,
            ToolTip = canRetry
                ? "只重新处理刚才被占用而跳过的配图。"
                : "没有因为占用而跳过的配图。"
        };
        ToolTipService.SetShowOnDisabled(retry, true);
        BrushCache.ApplyGhostButton(retry);
        retry.Click += (_, _) =>
        {
            if (!canRetry)
            {
                return;
            }

            mRetry = true;
            DialogResult = true;
            Close();
        };

        var close = new Button
        {
            Content = "关闭",
            IsCancel = true,
            IsDefault = true,
            MinWidth = 88,
            Padding = new Thickness(12, 6, 12, 6)
        };
        BrushCache.ApplyGhostButton(close);
        close.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        buttons.Children.Add(retry);
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        var stack = new DockPanel();
        var summaryText = new TextBlock
        {
            Text = summary,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(summaryText, Dock.Top);
        stack.Children.Add(summaryText);
        var box = new TextBox
        {
            Text = body,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, 微软雅黑"),
            Padding = new Thickness(12)
        };
        stack.Children.Add(box);
        root.Children.Add(stack);
        Content = root;
    }

    /// <summary>
    /// 显示结果。用户点击「重试」时返回真。
    /// </summary>
    public static bool Show(
        string title,
        string summary,
        IReadOnlyList<string> detailLineList,
        bool canRetry,
        Window? owner = null)
    {
        var body = detailLineList == null || detailLineList.Count == 0
            ? ""
            : string.Join("\n\n", detailLineList);
        var dialog = new CompressResultWindow(title, summary, body, canRetry)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        dialog.ShowDialog();
        WindowActivation.BringToFront(dialog.Owner);
        return dialog.mRetry;
    }
}
