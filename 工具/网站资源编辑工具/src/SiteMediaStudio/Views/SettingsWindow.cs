using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 本机设置：查看与清理 EXE 旁缩略图缓存；只读列出文案草稿、未执行标记与星级草稿路径。
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly TextBlock mSizeText;
    private readonly Action mClearCache;
    private readonly AppSettings mSettings;
    private readonly Action? mWatchChanged;

    private SettingsWindow(Action clearCache, AppSettings settings, Action? watchChanged)
    {
        mClearCache = clearCache;
        mSettings = settings;
        mWatchChanged = watchChanged;
        Title = "设置";
        Width = 520;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var title = new TextBlock
        {
            Text = "缩略图缓存",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var clearBtn = new Button
        {
            Content = "清理缓存",
            MinWidth = 96,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        BrushCache.ApplyGhostButton(clearBtn);
        clearBtn.Click += (_, _) =>
        {
            mClearCache();
            RefreshSize();
        };

        var closeBtn = new Button
        {
            Content = "关闭",
            MinWidth = 80,
            Padding = new Thickness(12, 6, 12, 6),
            IsCancel = true
        };
        BrushCache.ApplyGhostButton(closeBtn);
        closeBtn.Click += (_, _) => Close();
        buttons.Children.Add(clearBtn);
        buttons.Children.Add(closeBtn);
        root.Children.Add(buttons);

        var body = new StackPanel();
        body.Children.Add(Muted("缓存路径（软件安装目录下的 thumbs）"));
        body.Children.Add(new TextBox
        {
            Text = ThumbCacheStore.DirectoryPath,
            IsReadOnly = true,
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑")
        });
        body.Children.Add(Muted("当前缓存大小"));
        mSizeText = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑"),
            FontSize = 14
        };
        body.Children.Add(mSizeText);
        body.Children.Add(Muted("文案草稿路径（清理缩略图不删除）"));
        body.Children.Add(new TextBox
        {
            Text = CopyDraftStore.FilePath,
            IsReadOnly = true,
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑")
        });
        body.Children.Add(Muted("未执行标记路径（清理缩略图不删除）"));
        body.Children.Add(ReadOnlyPath(MarkDraftStore.FilePath));
        body.Children.Add(Muted("星级草稿路径（清理缩略图不删除）"));
        body.Children.Add(ReadOnlyPath(StarDraftStore.FilePath));
        body.Children.Add(Muted("投放箱监视"));
        var watchBox = new CheckBox
        {
            Content = "投放箱变更时自动刷新编目",
            IsChecked = mSettings.WatchStageEnabled,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = BrushCache.Text
        };
        watchBox.Checked += (_, _) => SetWatch(true);
        watchBox.Unchecked += (_, _) => SetWatch(false);
        body.Children.Add(watchBox);
        root.Children.Add(body);
        Content = root;
        RefreshSize();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// 打开设置窗。
    /// </summary>
    public static void Show(Action clearCache, AppSettings settings, Action? watchChanged = null)
    {
        var dialog = new SettingsWindow(clearCache, settings, watchChanged)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 写入自动刷新开关并立即套用监视。
    /// </summary>
    private void SetWatch(bool enabled)
    {
        mSettings.WatchStageChanges = enabled;
        AppSettingsStore.Save(mSettings);
        mWatchChanged?.Invoke();
    }

    /// <summary>
    /// 刷新缓存占用。
    /// </summary>
    private void RefreshSize()
    {
        mSizeText.Text = FileSizeFormat.Format(ThumbCacheStore.MeasureBytes());
    }

    /// <summary>
    /// 只读路径。清理缩略图不改这些文件。
    /// </summary>
    private static TextBox ReadOnlyPath(string path)
    {
        return new TextBox
        {
            Text = path,
            IsReadOnly = true,
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑")
        };
    }

    /// <summary>
    /// 次要说明文字。
    /// </summary>
    private static TextBlock Muted(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushCache.Muted
        };
    }
}
