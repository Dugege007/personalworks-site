using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 确认后把台账 stageRel 指到新投放路径。
/// </summary>
public sealed class RelocateWindow : Window
{
    private readonly WorkspaceSession mSession;
    private readonly string mObjectKey;
    private readonly string mStageRelBefore;
    private readonly string mStageRelAfter;
    private readonly CheckBox? mFolderBox;
    private readonly Action mApplied;

    private RelocateWindow(
        WorkspaceSession session,
        string objectKey,
        string stageRelBefore,
        string stageRelAfter,
        Action applied)
    {
        mSession = session;
        mObjectKey = objectKey;
        mStageRelBefore = JsonUtil.ToRel(stageRelBefore);
        mStageRelAfter = JsonUtil.ToRel(stageRelAfter);
        mApplied = applied;

        Title = "路径重挂";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var title = new TextBlock
        {
            Text = "只改台账路径",
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
        body.Children.Add(Muted("对象键（不改）"));
        body.Children.Add(ReadOnlyBox(mObjectKey));
        body.Children.Add(Muted("台账旧路径"));
        body.Children.Add(ReadOnlyBox(mStageRelBefore));
        body.Children.Add(Muted("投放箱新路径"));
        body.Children.Add(ReadOnlyBox(mStageRelAfter));
        if (StageRelocateRules.IsFolderMove(mStageRelBefore, mStageRelAfter))
        {
            mFolderBox = new CheckBox
            {
                Content = "整夹重挂（同一旧文件夹前缀的 published 一并改写）",
                IsChecked = true,
                Margin = new Thickness(0, 4, 0, 8),
                Foreground = BrushCache.Text
            };
            body.Children.Add(mFolderBox);
        }

        body.Children.Add(new TextBlock
        {
            Text = "确认前不写盘。写入后不改对象键、内容层 src 与题名，也不移动正式位。",
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
    /// 打开重挂窗。
    /// </summary>
    public static void Show(
        WorkspaceSession session,
        string objectKey,
        string stageRelBefore,
        string stageRelAfter,
        Action applied)
    {
        var dialog = new RelocateWindow(session, objectKey, stageRelBefore, stageRelAfter, applied)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 预演将改的台账路径。
    /// </summary>
    private void ShowPreview()
    {
        TextDialog.Show("预演", PromptRenderer.RenderPreview(BuildReport(ExecutionMode.Prompt)));
    }

    /// <summary>
    /// 确认后只写台账。
    /// </summary>
    private void Apply()
    {
        var report = BuildReport(ExecutionMode.Direct);
        if (report.HasHardError)
        {
            TextDialog.Show("无法重挂", PromptRenderer.RenderPreview(report));
            return;
        }

        if (!ConfirmIntentDialog.Show(
                $"将重挂 {report.Document.Items.Count} 条台账路径。对象键与内容层不变。",
                requireAck: false,
                title: "确认重挂",
                okLabel: "写入"))
        {
            return;
        }

        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var intentPath = Path.Combine(outDir, "intent-relocate.json");
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(report.Document), JsonUtil.Utf8NoBom);
        var result = BatchExecutor.Run(mSession, report.Document, intentPath);
        if (!result.Ok)
        {
            TextDialog.Show(
                "重挂失败，意图文件已保留",
                $"{result.FailureText}\n\n意图文件：{intentPath}");
            return;
        }

        mApplied();
        Close();
    }

    /// <summary>
    /// 按是否整夹组装预演。
    /// </summary>
    private PreviewReport BuildReport(ExecutionMode mode)
    {
        IEnumerable<IntentItem> itemList = mFolderBox?.IsChecked == true
            ? StageRelocateRules.PlanFolder(mSession.LedgerDict, mStageRelBefore, mStageRelAfter)
            : new[] { StageRelocateRules.CreateItem(mObjectKey, mStageRelBefore, mStageRelAfter) };
        var document = IntentDocumentBuilder.BuildRelocate(mSession, mode, itemList);
        return PreviewReporter.BuildRelocate(mSession, document);
    }

    /// <summary>
    /// 只读路径框。
    /// </summary>
    private static TextBox ReadOnlyBox(string text)
    {
        return new TextBox
        {
            Text = text,
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
