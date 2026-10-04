using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 登记空壳或再次编辑题名、开始日与地点。
/// </summary>
public sealed class RegisterWorkWindow : Window
{
    private readonly WorkspaceSession mSession;
    private readonly WorkCatalogItem mWork;
    private readonly bool mIsEdit;
    private readonly string mChannel;
    private readonly string mFolderId;
    private readonly string mStageFolder;
    private readonly bool mShowSchedule;
    private readonly bool mShowPhotoFacts;
    private readonly TextBox mIdBox;
    private readonly TextBox mTitleBox;
    private readonly TextBox? mStartedOnBox;
    private readonly TextBox? mPlaceBox;
    private readonly Action<string> mApplied;

    private RegisterWorkWindow(
        WorkspaceSession session,
        WorkCatalogItem work,
        bool isEdit,
        Action<string> applied)
    {
        mSession = session;
        mWork = work;
        mIsEdit = isEdit;
        mChannel = work.Channel;
        mFolderId = WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder);
        if (string.IsNullOrWhiteSpace(mFolderId))
        {
            mFolderId = work.Id;
        }

        mStageFolder = string.IsNullOrWhiteSpace(work.StageFolder)
            ? WorkRegisterRules.StageFolder(work.Channel, work.Id)
            : JsonUtil.ToRel(work.StageFolder);
        mShowSchedule = WorkRegisterRules.SupportsScheduleFields(work.Channel);
        mShowPhotoFacts = !isEdit && PhotoFactRules.RequiresFacts(work.Channel);
        mApplied = applied;

        Title = isEdit ? "编辑项目" : "登记为作品";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var title = new TextBlock
        {
            Text = isEdit ? "编辑项目" : "登记为空壳作品",
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

        var previewBtn = new Button
        {
            Content = "预演",
            MinWidth = 80,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        BrushCache.ApplyGhostButton(previewBtn);
        previewBtn.Click += (_, _) => ShowPreview();

        var applyBtn = new Button
        {
            Content = "写入",
            MinWidth = 80,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        BrushCache.ApplyGhostButton(applyBtn);
        applyBtn.Click += (_, _) => Apply();

        var cancelBtn = new Button
        {
            Content = "取消",
            MinWidth = 80,
            Padding = new Thickness(12, 6, 12, 6),
            IsCancel = true
        };
        BrushCache.ApplyGhostButton(cancelBtn);
        cancelBtn.Click += (_, _) => Close();
        buttons.Children.Add(previewBtn);
        buttons.Children.Add(applyBtn);
        buttons.Children.Add(cancelBtn);
        root.Children.Add(buttons);

        var body = new StackPanel();
        body.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.Channel));
        body.Children.Add(LockedBox(mChannel));
        body.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.StageFolder));
        body.Children.Add(LockedBox(mStageFolder));
        body.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.WorkId));
        mIdBox = isEdit
            ? LockedBox(work.Id)
            : EditableBox(WorkRegisterRules.SeedIdForChannel(mChannel, mFolderId) ?? "");
        body.Children.Add(mIdBox);
        body.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.Title));
        mTitleBox = EditableBox(isEdit ? work.Title : WorkRegisterRules.SeedTitle(mFolderId));
        body.Children.Add(mTitleBox);
        if (mShowPhotoFacts)
        {
            var year = PhotoFactRules.TryReadExplicitYear(WorkRegisterRules.SeedStartedOn(mFolderId), null, out var explicitYear)
                ? explicitYear
                : "";
            body.Children.Add(YearLabel());
            body.Children.Add(LockedBox(year));
            body.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.Place));
            mPlaceBox = EditableBox("");
            body.Children.Add(mPlaceBox);
        }
        else if (mShowSchedule)
        {
            var startedSeed = isEdit && !string.IsNullOrWhiteSpace(work.StartedOn)
                ? work.StartedOn
                : WorkRegisterRules.SeedStartedOn(mFolderId);
            mStartedOnBox = EditableBox(startedSeed);
            mPlaceBox = EditableBox(isEdit ? work.Place ?? "" : "");
            body.Children.Add(ScheduleRow(mStartedOnBox, mPlaceBox));
        }

        body.Children.Add(new TextBlock
        {
            Text = isEdit
                ? "确认前不写盘。写入后只改题名、开始日与地点，不改媒体与授权。"
                : mShowPhotoFacts
                    ? "确认前不写盘。写入后只建空作品，并记下年份与地点。类型写在单张照片上。夹里的文件仍未上页。"
                    : "确认前不写盘。写入后只建空作品，夹里的文件仍未上页。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 8, 0, 0)
        });
        root.Children.Add(body);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// 打开登记窗。
    /// </summary>
    public static void Show(WorkspaceSession session, WorkCatalogItem folder, Action<string> applied)
    {
        var dialog = new RegisterWorkWindow(session, folder, false, applied)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 打开已入编项目的编辑窗。
    /// </summary>
    public static void ShowEdit(WorkspaceSession session, WorkCatalogItem work, Action<string> applied)
    {
        var dialog = new RegisterWorkWindow(session, work, true, applied)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 预演将写入的作品块。
    /// </summary>
    private void ShowPreview()
    {
        var report = BuildReport(ExecutionMode.Prompt);
        TextDialog.Show("预演", PromptRenderer.RenderPreview(report));
    }

    /// <summary>
    /// 确认后写入并保留意图文件。
    /// </summary>
    private void Apply()
    {
        var report = BuildReport(ExecutionMode.Direct);
        if (report.HasHardError)
        {
            TextDialog.Show(mIsEdit ? "无法编辑" : "无法登记", PromptRenderer.RenderPreview(report));
            return;
        }

        var confirm = mIsEdit
            ? $"将改写「{mTitleBox.Text.Trim()}」（{mIdBox.Text.Trim()}）的题名、开始日与地点。不改媒体。"
            : $"将登记「{mTitleBox.Text.Trim()}」（{mIdBox.Text.Trim()}）为空壳作品。不调用 ingest，不产生对象键。";
        if (!ConfirmIntentDialog.Show(
                confirm,
                requireAck: false,
                title: mIsEdit ? "确认编辑" : "确认登记",
                okLabel: "写入"))
        {
            return;
        }

        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var intentPath = Path.Combine(outDir, mIsEdit ? "intent-update.json" : "intent-register.json");
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(report.Document), JsonUtil.Utf8NoBom);
        var result = BatchExecutor.Run(mSession, report.Document, intentPath);
        if (!result.Ok)
        {
            TextDialog.Show(
                mIsEdit ? "编辑失败，意图文件已保留" : "登记失败，意图文件已保留",
                $"{result.FailureText}\n\n意图文件：{intentPath}");
            return;
        }

        mApplied(mIdBox.Text.Trim());
        Close();
    }

    /// <summary>
    /// 按当前表单组装预演。
    /// </summary>
    private PreviewReport BuildReport(ExecutionMode mode)
    {
        var startedOn = mShowPhotoFacts
            ? WorkRegisterRules.SeedStartedOn(mFolderId)
            : mStartedOnBox?.Text;
        var document = mIsEdit
            ? IntentDocumentBuilder.BuildUpdate(
                mSession,
                mode,
                mWork,
                mTitleBox.Text.Trim(),
                mStartedOnBox?.Text,
                mPlaceBox?.Text)
            : IntentDocumentBuilder.BuildRegister(
                mSession,
                mode,
                mChannel,
                mIdBox.Text.Trim(),
                mTitleBox.Text.Trim(),
                mStageFolder,
                startedOn,
                mPlaceBox?.Text,
                null);
        return PreviewReporter.BuildRegister(mSession, document);
    }

    /// <summary>
    /// 只读且置灰，表示该项已定、不能改。
    /// </summary>
    private static TextBox LockedBox(string text)
    {
        var box = EditableBox(text);
        box.IsReadOnly = true;
        box.Foreground = BrushCache.Muted;
        box.CaretBrush = BrushCache.Muted;
        box.IsTabStop = false;
        box.Focusable = false;
        box.Opacity = 0.55;
        return box;
    }

    /// <summary>
    /// 可编辑输入框。
    /// </summary>
    private static TextBox EditableBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑")
        };
    }

    /// <summary>
    /// 年份只读，右侧标自动。
    /// </summary>
    private UIElement YearLabel()
    {
        var row = new DockPanel { LastChildFill = true };
        var auto = new TextBlock
        {
            Text = "自动",
            Foreground = BrushCache.Muted,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(auto, Dock.Right);
        row.Children.Add(auto);
        row.Children.Add(PlainLabel("年份"));
        return row;
    }

    /// <summary>
    /// 不带问号的短标签。
    /// </summary>
    private static TextBlock PlainLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushCache.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>
    /// 开始日与地点同一行，各带短标签与问号。
    /// </summary>
    private UIElement ScheduleRow(TextBox startedOnBox, TextBox placeBox)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var started = new StackPanel();
        started.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.StartedOn));
        started.Children.Add(startedOnBox);
        Grid.SetColumn(started, 0);

        var place = new StackPanel();
        place.Children.Add(FieldLabel(WorkRegisterFieldHelp.Field.Place));
        place.Children.Add(placeBox);
        Grid.SetColumn(place, 2);

        row.Children.Add(started);
        row.Children.Add(place);
        return row;
    }

    /// <summary>
    /// 短标签加问号；悬停问号给出说明与随机示例。
    /// </summary>
    private UIElement FieldLabel(WorkRegisterFieldHelp.Field field)
    {
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 0) };
        var help = HelpButton(field);
        DockPanel.SetDock(help, Dock.Right);
        row.Children.Add(help);
        row.Children.Add(new TextBlock
        {
            Text = WorkRegisterFieldHelp.Label(field),
            Foreground = BrushCache.Muted,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    /// <summary>
    /// 问号钮：悬停刷新三则示例，不抢 Tab。
    /// </summary>
    private Button HelpButton(WorkRegisterFieldHelp.Field field)
    {
        var button = new Button
        {
            Content = "?",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 12,
            Cursor = Cursors.Help,
            Focusable = false,
            ToolTip = BuildHelpTip(field)
        };
        BrushCache.ApplyGhostButton(button);
        button.Padding = new Thickness(0);
        button.Width = 22;
        button.Height = 22;
        button.Margin = new Thickness(8, 0, 0, 0);
        ToolTipService.SetInitialShowDelay(button, 200);
        ToolTipService.SetShowDuration(button, 20000);
        ToolTipService.SetBetweenShowDelay(button, 0);
        button.ToolTipOpening += (_, _) =>
        {
            if (button.ToolTip is ToolTip tip && tip.Content is TextBlock block)
            {
                block.Text = WorkRegisterFieldHelp.FormatTooltip(field, mSession, channelKey: mChannel);
            }
        };
        return button;
    }

    /// <summary>
    /// 深色提示：作用说明与三则示例。
    /// </summary>
    private ToolTip BuildHelpTip(WorkRegisterFieldHelp.Field field)
    {
        return new ToolTip
        {
            Background = BrushCache.Freeze("#1A1D22"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            Padding = new Thickness(10, 8, 10, 8),
            HasDropShadow = false,
            Content = new TextBlock
            {
                Text = WorkRegisterFieldHelp.FormatTooltip(field, mSession, channelKey: mChannel),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320,
                Foreground = BrushCache.Text,
                FontSize = 12
            }
        };
    }
}
