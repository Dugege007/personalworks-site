using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 本机设置与心得配图共享阈值。内容可滚动，窗口可调整大小。
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly TextBlock mSizeText = new();
    private readonly TextBox mConfigPathBox = new();
    private readonly TextBox mImageKbBox = new();
    private readonly TextBox mGifKbBox = new();
    private readonly TextBlock mThresholdStatus = new();
    private readonly Button mSaveThresholdBtn = new();
    private readonly Action mClearCache;
    private readonly AppSettings mSettings;
    private readonly Action? mWatchChanged;
    private readonly Action? mThresholdsChanged;
    private readonly WorkspaceProfile? mProfile;
    private readonly TextBlock mSettingsStatus = new();
    private bool mThresholdSaveBlocked;
    private bool mSettingsSaveBlocked;
    private int mPngquantProbeGeneration;
    private int mOxipngProbeGeneration;
    private int mGifsicleProbeGeneration;

    private SettingsWindow(
        Action clearCache,
        AppSettings settings,
        Action? watchChanged,
        Action? thresholdsChanged,
        WorkspaceProfile? profile)
    {
        mClearCache = clearCache;
        mSettings = settings;
        mWatchChanged = watchChanged;
        mThresholdsChanged = thresholdsChanged;
        mProfile = profile;
        Title = "设置";
        Width = 560;
        Height = 720;
        MinWidth = 480;
        MinHeight = 360;
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
        mSettingsStatus.TextWrapping = TextWrapping.Wrap;
        mSettingsStatus.Margin = new Thickness(0, 0, 0, 12);
        mSettingsStatus.Visibility = Visibility.Collapsed;
        body.Children.Add(mSettingsStatus);
        WarnIfSettingsFileCorrupt();
        AddThumbnailSection(body);
        AddNoteImageSection(body);
        AddExternalProgramSection(body);
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body
        };
        root.Children.Add(scroll);
        Content = root;
        RefreshSize();
        LoadThresholds();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// 打开设置窗。保存共享阈值后调用 <paramref name="thresholdsChanged"/>，用来重新计算超限状态。
    /// </summary>
    public static void Show(
        Action clearCache,
        AppSettings settings,
        Action? watchChanged = null,
        Action? thresholdsChanged = null,
        WorkspaceProfile? profile = null)
    {
        var dialog = new SettingsWindow(clearCache, settings, watchChanged, thresholdsChanged, profile)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 缩略图缓存、本机草稿路径和投放箱监视。
    /// </summary>
    private void AddThumbnailSection(StackPanel body)
    {
        body.Children.Add(SectionTitle("缩略图缓存", first: true));
        body.Children.Add(Muted("缓存路径（软件安装目录下的 thumbs）"));
        body.Children.Add(ReadOnlyPath(ThumbCacheStore.DirectoryPath));
        body.Children.Add(Muted("当前缓存大小"));
        mSizeText.Margin = new Thickness(0, 4, 0, 12);
        mSizeText.FontFamily = MonoFamily();
        mSizeText.FontSize = 14;
        body.Children.Add(mSizeText);
        body.Children.Add(Muted("文案草稿路径（清理缩略图不删除）"));
        body.Children.Add(ReadOnlyPath(CopyDraftStore.FilePath));
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
            Foreground = BrushCache.Text,
            IsEnabled = !mSettingsSaveBlocked
        };
        watchBox.Checked += (_, _) => SetWatch(true);
        watchBox.Unchecked += (_, _) => SetWatch(false);
        body.Children.Add(watchBox);
    }

    /// <summary>
    /// 仓库内的配图阈值，以及只保存在本机的照片信息开关。
    /// </summary>
    private void AddNoteImageSection(StackPanel body)
    {
        body.Children.Add(SectionTitle("心得配图", first: false));
        body.Children.Add(Muted("普通配图和 GIF 的上限保存在仓库里的共享配置中，不写进本机设置。填写不小于 1 的整数 KB（1 KB = 1024 字节）后点击「保存阈值」，配置会写入下面的文件，并立即重新计算超限状态。修改后需要和其他文件一样提交并推送，另一台电脑拉取后使用相同阈值。文件大小严格大于阈值才算超限。"));
        body.Children.Add(Muted("共享配置路径"));
        ConfigureReadOnly(mConfigPathBox);
        body.Children.Add(mConfigPathBox);
        body.Children.Add(Muted("普通配图上限（KB）"));
        ConfigureEditor(mImageKbBox);
        body.Children.Add(mImageKbBox);
        body.Children.Add(Muted("GIF 上限（KB）"));
        ConfigureEditor(mGifKbBox);
        body.Children.Add(mGifKbBox);
        mThresholdStatus.TextWrapping = TextWrapping.Wrap;
        mThresholdStatus.Margin = new Thickness(0, 0, 0, 8);
        mThresholdStatus.Foreground = BrushCache.Muted;
        body.Children.Add(mThresholdStatus);
        mSaveThresholdBtn.Content = "保存阈值";
        mSaveThresholdBtn.MinWidth = 96;
        mSaveThresholdBtn.Padding = new Thickness(12, 6, 12, 6);
        mSaveThresholdBtn.HorizontalAlignment = HorizontalAlignment.Left;
        mSaveThresholdBtn.Margin = new Thickness(0, 0, 0, 12);
        BrushCache.ApplyGhostButton(mSaveThresholdBtn);
        mSaveThresholdBtn.Click += (_, _) => SaveThresholds();
        body.Children.Add(mSaveThresholdBtn);
        body.Children.Add(Muted("下面两个开关只影响以后的压缩，保存在本机设置里。"));
        var stripGps = new CheckBox
        {
            Content = "压缩时去掉 GPS 位置（默认开）",
            IsChecked = mSettings.StripGpsEnabled,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = BrushCache.Text,
            IsEnabled = !mSettingsSaveBlocked
        };
        stripGps.Checked += (_, _) => SetStripGps(true);
        stripGps.Unchecked += (_, _) => SetStripGps(false);
        body.Children.Add(stripGps);
        var keepParams = new CheckBox
        {
            Content = "压缩时保留拍摄参数（默认开）",
            IsChecked = mSettings.KeepCaptureParamsEnabled,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = BrushCache.Text,
            IsEnabled = !mSettingsSaveBlocked
        };
        keepParams.Checked += (_, _) => SetKeepCaptureParams(true);
        keepParams.Unchecked += (_, _) => SetKeepCaptureParams(false);
        body.Children.Add(keepParams);
    }

    /// <summary>
    /// pngquant、oxipng 和 gifsicle 的本机路径，以及后台检测到的版本。
    /// </summary>
    private void AddExternalProgramSection(StackPanel body)
    {
        body.Children.Add(SectionTitle("外部程序", first: false));
        body.Children.Add(Muted("下面三个路径只保存在本机设置里。留空时继续查找常见安装位置和系统 PATH。每一行右侧是检测到的版本；找不到时显示「未找到」。"));
        var pngquantVersion = AddToolPathRow(body, "pngquant 路径", out var pngquant);
        BindToolPath(
            pngquant,
            () => mSettings.PngquantPath,
            value => mSettings.PngquantPath = value,
            value => BeginPngquantProbe(pngquantVersion, value));
        BeginPngquantProbe(pngquantVersion, mSettings.PngquantPath);
        var oxipngVersion = AddToolPathRow(body, "oxipng 路径", out var oxipng);
        BindToolPath(
            oxipng,
            () => mSettings.OxipngPath,
            value => mSettings.OxipngPath = value,
            value => BeginOxipngProbe(oxipngVersion, value));
        BeginOxipngProbe(oxipngVersion, mSettings.OxipngPath);
        var gifsicleVersion = AddToolPathRow(body, "gifsicle 路径", out var gifsicle);
        BindToolPath(
            gifsicle,
            () => mSettings.GifsiclePath,
            value => mSettings.GifsiclePath = value,
            value => BeginGifsicleProbe(gifsicleVersion, value));
        BeginGifsicleProbe(gifsicleVersion, mSettings.GifsiclePath);
    }

    /// <summary>
    /// 路径输入框和右侧的版本文字。版本探测还没回来时先显示「正在检测…」。
    /// </summary>
    private static TextBlock AddToolPathRow(StackPanel body, string label, out TextBox pathBox)
    {
        body.Children.Add(Muted(label));
        var row = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathBox = EditorBox();
        pathBox.Margin = new Thickness(0, 0, 12, 0);
        var version = new TextBlock
        {
            Text = "正在检测…",
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 180,
            Foreground = BrushCache.Muted
        };
        Grid.SetColumn(version, 1);
        row.Children.Add(pathBox);
        row.Children.Add(version);
        body.Children.Add(row);
        return version;
    }

    /// <summary>
    /// 窗口关闭后，进行中的版本探测不再改文字。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        mPngquantProbeGeneration++;
        mOxipngProbeGeneration++;
        mGifsicleProbeGeneration++;
        base.OnClosed(e);
    }

    /// <summary>
    /// 在后台查找 pngquant 并读取版本。
    /// </summary>
    private void BeginPngquantProbe(TextBlock target, string? configuredPath)
    {
        var generation = ++mPngquantProbeGeneration;
        BeginVersionProbe(
            target,
            generation,
            () => mPngquantProbeGeneration,
            () => ExternalToolLocator.LocatePngquant(configuredPath));
    }

    /// <summary>
    /// 在后台查找 oxipng 并读取版本。
    /// </summary>
    private void BeginOxipngProbe(TextBlock target, string? configuredPath)
    {
        var generation = ++mOxipngProbeGeneration;
        BeginVersionProbe(
            target,
            generation,
            () => mOxipngProbeGeneration,
            () => ExternalToolLocator.LocateOxipng(configuredPath));
    }

    /// <summary>
    /// 在后台查找 gifsicle 并读取版本。
    /// </summary>
    private void BeginGifsicleProbe(TextBlock target, string? configuredPath)
    {
        var generation = ++mGifsicleProbeGeneration;
        BeginVersionProbe(
            target,
            generation,
            () => mGifsicleProbeGeneration,
            () => ExternalToolLocator.LocateGifsicle(configuredPath));
    }

    /// <summary>
    /// 版本探测放在后台线程。完成后回到界面线程更新文字，过期结果直接丢掉。
    /// </summary>
    private void BeginVersionProbe(
        TextBlock target,
        int generation,
        Func<int> readGeneration,
        Func<ExternalToolStatus> locate)
    {
        target.Text = "正在检测…";
        target.Foreground = BrushCache.Muted;
        var dispatcher = Dispatcher;
        System.Threading.Tasks.Task.Run(locate).ContinueWith(task =>
        {
            void Apply()
            {
                if (generation != readGeneration())
                {
                    return;
                }

                if (task.IsFaulted || task.IsCanceled)
                {
                    _ = task.Exception;
                    target.Text = ExternalToolStatus.NotFoundText;
                    target.Foreground = BrushCache.Muted;
                    return;
                }

                var status = task.Result;
                target.Text = status.DisplayText;
                target.Foreground = status.Found && !string.IsNullOrWhiteSpace(status.Version)
                    ? BrushCache.Text
                    : BrushCache.Muted;
            }

            try
            {
                if (dispatcher.CheckAccess())
                {
                    Apply();
                }
                else
                {
                    dispatcher.BeginInvoke(Apply);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });
    }

    /// <summary>
    /// 把共享阈值填进输入框。文件损坏时禁止保存，避免用默认值覆盖原文件。
    /// </summary>
    private void LoadThresholds()
    {
        var path = mProfile == null ? null : NoteImageLimitsStore.FilePath(mProfile);
        mConfigPathBox.Text = path ?? "";

        if (path == null)
        {
            BlockThresholdSave("请先打开工作区，再修改心得配图阈值。");
            return;
        }

        try
        {
            var limits = NoteImageLimitsStore.Load(mProfile!);
            mImageKbBox.Text = NoteImageThreshold.FormatWholeKilobytes(limits.ImageMaxBytes);
            mGifKbBox.Text = NoteImageThreshold.FormatWholeKilobytes(limits.GifMaxBytes);
        }
        catch (NoteImageLimitsException ex)
        {
            BlockThresholdSave(ex.Message);
            return;
        }

        if (!NoteImageThreshold.TryParseWholeKilobytes(mImageKbBox.Text, out _, out var imageFailure))
        {
            ShowThresholdStatus(KilobyteError("普通配图上限", imageFailure), error: true);
        }
        else if (!NoteImageThreshold.TryParseWholeKilobytes(mGifKbBox.Text, out _, out var gifFailure))
        {
            ShowThresholdStatus(KilobyteError("GIF 上限", gifFailure), error: true);
        }
    }

    /// <summary>
    /// 校验两个上限，原子写入共享配置，然后通知主窗口重新计算超限状态。
    /// </summary>
    private void SaveThresholds()
    {
        if (mThresholdSaveBlocked || mProfile == null)
        {
            return;
        }

        if (!NoteImageThreshold.TryParseWholeKilobytes(mImageKbBox.Text, out var imageBytes, out var imageFailure))
        {
            ShowThresholdStatus(KilobyteError("普通配图上限", imageFailure), error: true);
            return;
        }

        if (!NoteImageThreshold.TryParseWholeKilobytes(mGifKbBox.Text, out var gifBytes, out var gifFailure))
        {
            ShowThresholdStatus(KilobyteError("GIF 上限", gifFailure), error: true);
            return;
        }

        try
        {
            NoteImageLimitsStore.Save(mProfile, new NoteImageLimits
            {
                ImageMaxBytes = imageBytes,
                GifMaxBytes = gifBytes
            });
        }
        catch (Exception ex) when (ex is NoteImageLimitsException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowThresholdStatus(ex.Message, error: true);
            return;
        }

        ShowThresholdStatus("已写入仓库里的共享配置，并开始重新计算超限状态。请记得提交并推送。", error: false);
        mThresholdsChanged?.Invoke();
    }

    /// <summary>
    /// 损坏或缺少工作区时，不允许把输入框里的数字写回去。
    /// </summary>
    private void BlockThresholdSave(string message)
    {
        mThresholdSaveBlocked = true;
        mImageKbBox.IsEnabled = false;
        mGifKbBox.IsEnabled = false;
        mSaveThresholdBtn.IsEnabled = false;
        ShowThresholdStatus(message, error: true);
    }

    /// <summary>
    /// 把 KB 解析失败写成完整句子。
    /// </summary>
    private static string KilobyteError(string label, NoteImageKilobyteParseFailure failure)
    {
        if (failure == NoteImageKilobyteParseFailure.Overflow)
        {
            return label + "太大，无法换算成字节。";
        }

        if (failure == NoteImageKilobyteParseFailure.BelowMinimum)
        {
            return label + "不能小于 1 KB。";
        }

        return label + "请填写不小于 1 的整数，单位是 KB。";
    }

    /// <summary>
    /// 在阈值输入框下显示结果。错误用醒目颜色，成功用正文颜色。
    /// </summary>
    private void ShowThresholdStatus(string message, bool error)
    {
        mThresholdStatus.Text = message;
        mThresholdStatus.Foreground = error ? BrushCache.Oxide : BrushCache.Text;
    }

    /// <summary>
    /// 本机设置无法解析时提示用户，并禁止把内存里的值写回原文件。
    /// </summary>
    private void WarnIfSettingsFileCorrupt()
    {
        try
        {
            AppSettingsStore.Load();
        }
        catch (AppSettingsException ex)
        {
            mSettingsSaveBlocked = true;
            ShowSettingsStatus(ex.Message);
        }
    }

    /// <summary>
    /// 在窗口顶部显示本机设置错误。
    /// </summary>
    private void ShowSettingsStatus(string message)
    {
        mSettingsStatus.Text = message;
        mSettingsStatus.Foreground = BrushCache.Oxide;
        mSettingsStatus.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 写回本机设置。文件损坏时提示并拒绝覆盖。
    /// </summary>
    private bool TrySaveSettings()
    {
        if (mSettingsSaveBlocked)
        {
            return false;
        }

        try
        {
            AppSettingsStore.Save(mSettings);
            return true;
        }
        catch (AppSettingsException ex)
        {
            mSettingsSaveBlocked = true;
            ShowSettingsStatus(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 写入自动刷新开关并立即套用监视。
    /// </summary>
    private void SetWatch(bool enabled)
    {
        if (mSettingsSaveBlocked)
        {
            return;
        }

        mSettings.WatchStageChanges = enabled;
        if (!TrySaveSettings())
        {
            return;
        }

        mWatchChanged?.Invoke();
    }

    /// <summary>
    /// 写入是否去掉全球定位系统位置。
    /// </summary>
    private void SetStripGps(bool enabled)
    {
        if (mSettingsSaveBlocked)
        {
            return;
        }

        mSettings.StripGps = enabled;
        TrySaveSettings();
    }

    /// <summary>
    /// 写入是否保留拍摄参数。
    /// </summary>
    private void SetKeepCaptureParams(bool enabled)
    {
        if (mSettingsSaveBlocked)
        {
            return;
        }

        mSettings.KeepCaptureParams = enabled;
        TrySaveSettings();
    }

    /// <summary>
    /// 路径框失去焦点时，把有变化的路径写进本机设置，并重新检测版本。
    /// </summary>
    private void BindToolPath(TextBox box, Func<string?> read, Action<string?> write, Action<string?> changed)
    {
        box.Text = read() ?? "";
        if (mSettingsSaveBlocked)
        {
            box.IsEnabled = false;
        }

        box.LostFocus += (_, _) =>
        {
            var next = box.Text.Trim();
            var value = next.Length == 0 ? null : next;
            if (string.Equals(read(), value, StringComparison.Ordinal))
            {
                return;
            }

            write(value);
            TrySaveSettings();
            changed(value);
        };
    }

    /// <summary>
    /// 在阈值框里按回车时保存。
    /// </summary>
    private void OnThresholdKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SaveThresholds();
        e.Handled = true;
    }

    /// <summary>
    /// 刷新缓存占用。
    /// </summary>
    private void RefreshSize()
    {
        mSizeText.Text = FileSizeFormat.Format(ThumbCacheStore.MeasureBytes());
    }

    /// <summary>
    /// 分组标题。
    /// </summary>
    private static TextBlock SectionTitle(string text, bool first)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, first ? 0 : 22, 0, 12)
        };
    }

    /// <summary>
    /// 只读路径。清理缩略图不改这些文件。
    /// </summary>
    private static TextBox ReadOnlyPath(string path)
    {
        var box = new TextBox { Text = path };
        ConfigureReadOnly(box);
        return box;
    }

    /// <summary>
    /// 把输入框调成只读路径样式。
    /// </summary>
    private static void ConfigureReadOnly(TextBox box)
    {
        box.IsReadOnly = true;
        box.Margin = new Thickness(0, 4, 0, 12);
        box.Padding = new Thickness(8, 6, 8, 6);
        box.Background = BrushCache.Freeze("#0B0D10");
        box.Foreground = BrushCache.Text;
        box.BorderBrush = BrushCache.Line;
        box.FontFamily = MonoFamily();
    }

    /// <summary>
    /// 可编辑的单行路径或数字。
    /// </summary>
    private static TextBox EditorBox()
    {
        var box = new TextBox
        {
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            CaretBrush = BrushCache.Text,
            FontFamily = MonoFamily()
        };
        return box;
    }

    /// <summary>
    /// 把已经建好的输入框调成可编辑样式，并让回车保存阈值。
    /// </summary>
    private void ConfigureEditor(TextBox box)
    {
        box.Margin = new Thickness(0, 4, 0, 12);
        box.Padding = new Thickness(8, 6, 8, 6);
        box.Background = BrushCache.Freeze("#0B0D10");
        box.Foreground = BrushCache.Text;
        box.BorderBrush = BrushCache.Line;
        box.CaretBrush = BrushCache.Text;
        box.FontFamily = MonoFamily();
        box.KeyDown += OnThresholdKeyDown;
    }

    /// <summary>
    /// 路径和数字用的等宽字体。
    /// </summary>
    private static FontFamily MonoFamily()
    {
        return new FontFamily("Cascadia Mono, Consolas, 微软雅黑");
    }

    /// <summary>
    /// 次要说明文字。
    /// </summary>
    private static TextBlock Muted(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }
}
