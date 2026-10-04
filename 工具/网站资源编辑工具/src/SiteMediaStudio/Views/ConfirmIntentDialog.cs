using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 机械执行确认。回收 / 撤下须勾选「已知后果」，默认焦点在取消。
/// </summary>
public sealed class ConfirmIntentDialog : Window
{
    private readonly CheckBox? mAckBox;
    private readonly Button mOkButton;

    private ConfirmIntentDialog(string message, bool requireAck, string title, string okLabel)
    {
        Title = title;
        Width = 520;
        MinHeight = requireAck ? 280 : 220;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var cancel = new Button
        {
            Content = "取消",
            IsCancel = true,
            IsDefault = true,
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        BrushCache.ApplyGhostButton(cancel);
        cancel.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        mOkButton = new Button
        {
            Content = okLabel,
            MinWidth = 88,
            Padding = new Thickness(12, 6, 12, 6),
            IsEnabled = !requireAck
        };
        BrushCache.ApplyGhostButton(mOkButton);
        mOkButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(mOkButton);
        root.Children.Add(buttons);

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22
        });
        if (requireAck)
        {
            mAckBox = new CheckBox
            {
                Content = "已知后果",
                Margin = new Thickness(0, 16, 0, 0),
                Foreground = BrushCache.Text
            };
            mAckBox.Checked += (_, _) => mOkButton.IsEnabled = true;
            mAckBox.Unchecked += (_, _) => mOkButton.IsEnabled = false;
            body.Children.Add(mAckBox);
        }

        root.Children.Add(body);
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    /// <summary>
    /// 确认则返回 true；取消或未勾选则 false。
    /// </summary>
    public static bool Show(string message, bool requireAck, string title = "确认执行", string okLabel = "执行")
    {
        var dialog = new ConfirmIntentDialog(message, requireAck, title, okLabel)
        {
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true;
    }
}
