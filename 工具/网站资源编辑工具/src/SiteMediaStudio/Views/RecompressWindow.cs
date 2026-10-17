using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 重新压缩时用户选择的质量。没有接受时 <see cref="Accepted"/> 为假。
/// 手动质量为空表示按默认规则从高到低逐级尝试。
/// </summary>
public readonly record struct RecompressChoice(bool Accepted, int? ManualQuality);

/// <summary>
/// 重新压缩的小窗。总是从原图备份重新压，不在已经压过的图上再压。
/// </summary>
public sealed class RecompressWindow : Window
{
    /// <summary>
    /// 比自动阶梯的第一级 92 更高，并且只编码这一级、不再缩边。
    /// </summary>
    public const int HigherQuality = 95;

    private readonly RadioButton mAutoButton;
    private readonly RadioButton mHigherButton;
    private readonly RadioButton mManualButton;
    private readonly TextBox mQualityBox;
    private readonly TextBlock mErrorText;
    private RecompressChoice mChoice;

    private RecompressWindow(string fileName)
    {
        Title = "重新压缩";
        Width = 460;
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
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        BrushCache.ApplyGhostButton(cancel);
        cancel.Click += (_, _) =>
        {
            mChoice = new RecompressChoice(false, null);
            DialogResult = false;
            Close();
        };

        var start = new Button
        {
            Content = "开始压缩",
            IsDefault = true,
            MinWidth = 96,
            Padding = new Thickness(12, 6, 12, 6)
        };
        BrushCache.ApplyGhostButton(start);
        start.Click += (_, _) => Accept();
        buttons.Children.Add(cancel);
        buttons.Children.Add(start);
        root.Children.Add(buttons);

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "将从原图备份重新压缩「" + fileName + "」，不会在已经压过的图上再压一次。手动选择的质量如果仍超过阈值，也会保存，并在结果中列为「压缩后仍超限」。",
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 12)
        });

        mAutoButton = Radio("按默认规则自动", true);
        mHigherButton = Radio("更高画质（质量 " + HigherQuality.ToString(CultureInfo.InvariantCulture) + "，只压这一级）", false);
        mManualButton = Radio("手动质量（1 到 100）", false);
        body.Children.Add(mAutoButton);
        body.Children.Add(mHigherButton);
        body.Children.Add(mManualButton);

        mQualityBox = new TextBox
        {
            Width = 88,
            Margin = new Thickness(28, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
            Text = HigherQuality.ToString(CultureInfo.InvariantCulture)
        };
        mManualButton.Checked += (_, _) => mQualityBox.IsEnabled = true;
        mManualButton.Unchecked += (_, _) => mQualityBox.IsEnabled = false;
        body.Children.Add(mQualityBox);

        mErrorText = new TextBlock
        {
            Foreground = BrushCache.Oxide,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        body.Children.Add(mErrorText);
        root.Children.Add(body);
        Content = root;
    }

    /// <summary>
    /// 打开小窗。取消时返回未接受的选择。
    /// </summary>
    public static RecompressChoice Show(string fileName, Window? owner = null)
    {
        var dialog = new RecompressWindow(string.IsNullOrWhiteSpace(fileName) ? "这张配图" : fileName.Trim())
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        dialog.ShowDialog();
        WindowActivation.BringToFront(dialog.Owner);
        return dialog.mChoice;
    }

    /// <summary>
    /// 按当前选项关闭窗口。手动质量不在 1 到 100 时留在窗口里说明原因。
    /// </summary>
    private void Accept()
    {
        if (mHigherButton.IsChecked == true)
        {
            mChoice = new RecompressChoice(true, HigherQuality);
            DialogResult = true;
            Close();
            return;
        }

        if (mManualButton.IsChecked == true)
        {
            if (!int.TryParse(mQualityBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality)
                || quality < 1
                || quality > 100)
            {
                mErrorText.Text = "请填写 1 到 100 的整数质量。";
                return;
            }

            mChoice = new RecompressChoice(true, quality);
            DialogResult = true;
            Close();
            return;
        }

        mChoice = new RecompressChoice(true, null);
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 一组互斥的质量选项。
    /// </summary>
    private static RadioButton Radio(string label, bool isChecked)
    {
        return new RadioButton
        {
            Content = label,
            GroupName = "重新压缩质量",
            IsChecked = isChecked,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = BrushCache.Text
        };
    }
}
