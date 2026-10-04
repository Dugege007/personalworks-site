using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PersonalWorks.SiteMediaStudio.Controls;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 给当前作品或选中的多张写类型与自由标签。嵌入与检视栏相同的标签组件。
/// </summary>
public sealed class TagWindow : Window
{
    private readonly WorkspaceSession mSession;
    private readonly Func<Task<WorkCatalogItem?>> mReloaded;
    private readonly IReadOnlyList<string> mObjectKeyList;
    private readonly TagEditor mEditor;
    private WorkCatalogItem mWork;

    private TagWindow(
        WorkspaceSession session,
        WorkCatalogItem work,
        IReadOnlyList<string> objectKeyList,
        Func<Task<WorkCatalogItem?>> reloaded)
    {
        mSession = session;
        mWork = work;
        mObjectKeyList = objectKeyList;
        mReloaded = reloaded;

        Title = "标签";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;

        var root = new DockPanel { Margin = new Thickness(20) };
        var title = new TextBlock
        {
            Text = objectKeyList.Count == 0 ? "作品标签" : "已选 " + objectKeyList.Count + " 张",
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
        var close = MakeButton("关闭", (_, _) => Close());
        close.IsCancel = true;
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        var body = new StackPanel();
        if (objectKeyList.Count == 0)
        {
            body.Children.Add(Muted("类型"));
            body.Children.Add(LockedLine(FormatDerivedThemes(work)));
            body.Children.Add(new TextBlock
            {
                Text = "由已上页照片汇总，此处不能改。",
                Foreground = BrushCache.Muted,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }
        else
        {
            body.Children.Add(Muted("年份"));
            body.Children.Add(LockedLine(work.Year));
            body.Children.Add(Muted("地点"));
            body.Children.Add(LockedLine(string.IsNullOrWhiteSpace(work.Place) ? "" : work.Place));
        }

        mEditor = new TagEditor { Margin = new Thickness(0, 4, 0, 0) };
        body.Children.Add(TagCaptionRow());
        mEditor.TextCommitRequested += OnCommit;
        mEditor.SuggestionPicked += OnPick;
        mEditor.ChipRemoveRequested += OnRemove;
        mEditor.PresetDeleteRequested += OnPresetDelete;
        body.Children.Add(mEditor);
        body.Children.Add(new TextBlock
        {
            Text = objectKeyList.Count == 0
                ? "回车或空格写入作品标签。四位年份不写入。不改类型与 src。"
                : "回车或空格写入。年份与地点跟项目，不能改。类型可多选。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 8, 0, 0)
        });
        root.Children.Add(body);
        Content = root;
        BindEditor();
    }

    /// <summary>
    /// 打开标签窗。对象列表为空时写作品级。写盘失败时由回调重载并交回新作品。
    /// </summary>
    public static void Show(
        WorkspaceSession session,
        WorkCatalogItem work,
        IReadOnlyList<string> objectKeyList,
        Func<Task<WorkCatalogItem?>> reloaded)
    {
        var dialog = new TagWindow(session, work, objectKeyList, reloaded)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// 把已通过闸门的标签意图写入内容层，并入待发布队列。
    /// </summary>
    public static bool TryWrite(WorkspaceSession session, PreviewReport report, out string? failureText)
    {
        failureText = null;
        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var intentPath = Path.Combine(outDir, "intent-tags.json");
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(report.Document), JsonUtil.Utf8NoBom);
        var result = BatchExecutor.Run(session, report.Document, intentPath);
        if (!result.Ok)
        {
            failureText = $"{result.FailureText}\n\n意图文件：{intentPath}";
            return false;
        }

        if (result.Pending != null)
        {
            PendingPublishStore.MergeSave(result.Pending);
        }

        return true;
    }

    /// <summary>
    /// 回车或空格。
    /// </summary>
    private void OnCommit(object? sender, TagEditorCommitEventArgs e)
    {
        string? inputNotice;
        var outcome = mObjectKeyList.Count == 0
            ? TagEditorHost.CommitWork(mSession, mWork, CurrentSuggestions(), e.Text, out inputNotice, OnPersistFailed)
            : TagEditorHost.CommitFrames(mSession, mWork.Channel, CurrentFrames(), CurrentSuggestions(), e.Text, out inputNotice, OnPersistFailed);
        e.Notice = inputNotice;
        e.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        AfterWrite(outcome);
    }

    /// <summary>
    /// 点下拉项。
    /// </summary>
    private void OnPick(object? sender, TagEditorItemEventArgs e)
    {
        string? inputNotice = null;
        var outcome = mObjectKeyList.Count == 0
            ? TagEditorHost.ToggleWork(mSession, mWork, e.Item, out inputNotice, OnPersistFailed)
            : TagEditorHost.ToggleFrames(mSession, mWork.Channel, CurrentFrames(), e.Item, OnPersistFailed);
        e.Notice = inputNotice;
        e.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        AfterWrite(outcome);
    }

    /// <summary>
    /// 芯片叉。
    /// </summary>
    private void OnRemove(object? sender, TagEditorItemEventArgs e)
    {
        var outcome = mObjectKeyList.Count == 0
            ? TagEditorHost.RemoveWork(mSession, mWork, e.Item, OnPersistFailed)
            : TagEditorHost.RemoveFromFrames(mSession, mWork.Channel, CurrentFrames(), e.Item, OnPersistFailed);
        e.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        AfterWrite(outcome);
    }

    /// <summary>
    /// 删除自由预设。
    /// </summary>
    private void OnPresetDelete(object? sender, TagEditorItemEventArgs e)
    {
        if (e.Item.Kind != TagEditorKind.Free)
        {
            return;
        }

        var outcome = TagEditorHost.DeletePreset(mSession, mWork.Channel, e.Item.Label, OnPersistFailed);
        AfterWrite(outcome);
    }

    /// <summary>
    /// 内存已改后立刻重画芯片。写盘在后台，这里不重载编目。
    /// </summary>
    private void AfterWrite(TagWriteOutcome outcome)
    {
        if (outcome != TagWriteOutcome.Written)
        {
            return;
        }

        BindEditor();
    }

    /// <summary>
    /// 后台写盘失败：提示后重载，丢掉还没落盘的内存改动。
    /// </summary>
    private async void OnPersistFailed(string failureText)
    {
        TextDialog.Show("标签失败，意图文件已保留", failureText);
        var next = await mReloaded().ConfigureAwait(true);
        if (!IsLoaded || next == null)
        {
            if (IsLoaded)
            {
                Close();
            }

            return;
        }

        mWork = next;
        BindEditor();
    }

    /// <summary>
    /// 按当前作品装预设与已选芯片。
    /// </summary>
    private void BindEditor()
    {
        var suggestions = CurrentSuggestions();
        mEditor.Suggestions = suggestions;
        if (mObjectKeyList.Count == 0)
        {
            mEditor.SelectedItems = TagEditorCatalog.ChipsForFrames(
                suggestions,
                new[] { (Themes: (IReadOnlyList<string>)Array.Empty<string>(), Tags: mWork.Tags) });
            mEditor.IsEditingEnabled = true;
            return;
        }

        var frames = mObjectKeyList.Select(key => TagEditorCatalog.ReadFrame(mWork, key)).ToList();
        mEditor.SelectedItems = TagEditorCatalog.ChipsForFrames(suggestions, frames);
        mEditor.IsEditingEnabled = true;
    }

    /// <summary>
    /// 本栏目预设。作品级不带类型。
    /// </summary>
    private List<TagEditorItem> CurrentSuggestions()
    {
        var works = mSession.Works.Where(item =>
            !item.IsUnregistered && string.Equals(item.Channel, mWork.Channel, StringComparison.Ordinal));
        return TagEditorCatalog.BuildSuggestions(
            works,
            TagVocabStore.Read(mSession.Profile.ResolvedRoot, mWork.Channel),
            includeTypes: mObjectKeyList.Count > 0);
    }

    /// <summary>
    /// 当前选中、且属于该作品的帧。
    /// </summary>
    private List<(string WorkId, string ObjectKey, IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> CurrentFrames()
    {
        var frames = new List<(string, string, IReadOnlyList<string>, IReadOnlyList<string>)>();
        foreach (var key in mObjectKeyList)
        {
            var frame = TagEditorCatalog.ReadFrame(mWork, key);
            frames.Add((mWork.Id, key, frame.Themes, frame.Tags));
        }

        return frames;
    }

    /// <summary>
    /// 已上页照片的类型中文，按固定顺序。
    /// </summary>
    private static string FormatDerivedThemes(WorkCatalogItem work)
    {
        var names = PhotoFactRules.ThemesOfPublishedMedia(work)
            .Select(key => PhotoFactRules.ThemeList.First(theme => theme.Key == key).Zh);
        return string.Join("、", names);
    }

    /// <summary>
    /// 只读一行。
    /// </summary>
    private static TextBlock LockedLine(string text)
    {
        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(text) ? "—" : text,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 4, 0, 12)
        };
    }

    /// <summary>
    /// 「标签」标题，右侧留拒绝原因。
    /// </summary>
    private UIElement TagCaptionRow()
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Muted("标签"));
        var notice = new TextBlock
        {
            Foreground = BrushCache.Oxide,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        notice.SetBinding(TextBlock.TextProperty, new Binding(nameof(TagEditor.NoticeText)) { Source = mEditor });
        Grid.SetColumn(notice, 1);
        row.Children.Add(notice);
        return row;
    }

    /// <summary>
    /// 短标签。
    /// </summary>
    private static TextBlock Muted(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushCache.Muted
        };
    }

    /// <summary>
    /// 幽灵按钮。
    /// </summary>
    private static Button MakeButton(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        BrushCache.ApplyGhostButton(button);
        button.Click += click;
        return button;
    }
}
