using System.IO;
using System.Windows;
using System.Windows.Controls;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 编辑心得标题、副标题与修订摘要。展示名自动拼接，达到 60 字警告、80 字禁止保存。
/// </summary>
public sealed class NoteMetaWindow : Window
{
    private readonly WorkspaceProfile mProfile;
    private readonly string mFolderFullPath;
    private readonly TextBox mMainBox;
    private readonly TextBox mSubBox;
    private readonly TextBlock mLengthText;
    private readonly TextBlock mDisplayText;
    private readonly TextBlock mMessage;
    private readonly Button mSaveButton;
    private readonly List<TextBox> mSummaryBoxList = new();
    private NoteMetaSaveResult? mResult;

    private NoteMetaWindow(WorkspaceProfile profile, string folderFullPath, NoteConfig config)
    {
        mProfile = profile;
        mFolderFullPath = folderFullPath;
        Title = "编辑标题与修订";
        Width = 560;
        Height = 680;
        MinWidth = 440;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;

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
            DialogResult = false;
            Close();
        };
        mSaveButton = new Button
        {
            Content = "保存",
            MinWidth = 88,
            Padding = new Thickness(12, 6, 12, 6)
        };
        BrushCache.ApplyGhostButton(mSaveButton);
        mSaveButton.Click += (_, _) => Save();
        buttons.Children.Add(cancel);
        buttons.Children.Add(mSaveButton);
        root.Children.Add(buttons);

        var folderName = Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var resolved = NoteConfigStore.ResolveTitles(folderFullPath, folderName, config);
        var body = new DockPanel();
        var heading = new StackPanel();
        DockPanel.SetDock(heading, Dock.Top);
        heading.Children.Add(Help("标题和副标题分开填写。有副标题时，列表和站点上的展示名自动写成「标题——副标题」，中间的中文破折号由工具加上。没有副标题时只显示标题。"));
        heading.Children.Add(Help("网址仍用原来的英文 slug「" + config.Slug + "」，夹名保持不变。"));
        heading.Children.Add(Label("标题"));
        mMainBox = InputBox(resolved.Resolved ? resolved.MainTitle : "");
        mMainBox.TextChanged += (_, _) => RefreshNameCheck();
        heading.Children.Add(mMainBox);
        heading.Children.Add(Label("副标题"));
        mSubBox = InputBox(resolved.Resolved ? resolved.Subtitle : "");
        mSubBox.TextChanged += (_, _) => RefreshNameCheck();
        heading.Children.Add(mSubBox);
        mLengthText = Info("");
        mDisplayText = Info("");
        mMessage = Info("");
        mMessage.TextWrapping = TextWrapping.Wrap;
        mMessage.Margin = new Thickness(0, 4, 0, 12);
        heading.Children.Add(mLengthText);
        heading.Children.Add(mDisplayText);
        heading.Children.Add(mMessage);
        heading.Children.Add(Label("修订说明"));
        heading.Children.Add(Help("可改每条摘要。时间保留。手改后这条来源记为手改。发布上线时，若正文或配图有实质变化，会追加一条助手写的短说明；没有变化不会重复追加。"));
        body.Children.Add(heading);

        var revisions = new StackPanel();
        if (config.Revisions.Count == 0)
        {
            revisions.Children.Add(Help("尚无修订。"));
        }
        else
        {
            foreach (var revision in config.Revisions)
            {
                revisions.Children.Add(Label(revision.At + "　" + SourceLabel(revision.Source)));
                var box = InputBox(revision.Summary);
                box.AcceptsReturn = true;
                box.TextWrapping = TextWrapping.Wrap;
                box.MinHeight = 56;
                box.Margin = new Thickness(0, 0, 0, 12);
                mSummaryBoxList.Add(box);
                revisions.Children.Add(box);
            }
        }

        body.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = revisions
        });
        root.Children.Add(body);
        Content = root;
        RefreshNameCheck();
    }

    /// <summary>
    /// 打开编辑窗。取消时返回空。
    /// </summary>
    public static NoteMetaSaveResult? Show(WorkspaceProfile profile, string folderFullPath, Window? owner = null)
    {
        var full = Path.GetFullPath(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!Directory.Exists(full))
        {
            TextDialog.Show("无法编辑标题", "找不到这篇心得的文件夹。", owner);
            return null;
        }

        var folderName = Path.GetFileName(full);
        NoteConfig config;
        try
        {
            config = NoteConfigStore.Ensure(full, folderName);
        }
        catch (Exception ex)
        {
            TextDialog.Show("无法编辑标题", ex.Message, owner);
            return null;
        }

        owner ??= Application.Current?.MainWindow;
        WindowActivation.BringToFront(owner);
        var dialog = new NoteMetaWindow(profile, full, config)
        {
            Owner = owner
        };
        var accepted = dialog.ShowDialog() == true;
        WindowActivation.BringToFront(owner);
        return accepted ? dialog.mResult : null;
    }

    /// <summary>
    /// 按当前标题刷新长度和展示名，并在禁止保存时停用保存按钮。
    /// </summary>
    private void RefreshNameCheck()
    {
        var check = NoteRules.CheckDisplayTitle(mMainBox.Text, mSubBox.Text);
        mLengthText.Text = "当前 " + check.Length + " 字。满 60 字提醒缩短，满 80 字不能保存。字数按展示名计算。";
        mDisplayText.Text = check.Titles.DisplayTitle.Length == 0
            ? "展示名会随标题自动生成。"
            : "展示名：" + check.Titles.DisplayTitle;
        mMessage.Text = check.Message;
        mMessage.Foreground = check.Verdict switch
        {
            NoteFolderNameVerdict.Block => BrushCache.Oxide,
            NoteFolderNameVerdict.Warn => BrushCache.Gold,
            _ => BrushCache.Muted
        };
        mSaveButton.IsEnabled = check.Verdict != NoteFolderNameVerdict.Block;
    }

    /// <summary>
    /// 提交标题、副标题与摘要。禁止项留在窗内，不关闭。
    /// </summary>
    private void Save()
    {
        var summaries = mSummaryBoxList.Select(box => box.Text ?? "").ToList();
        var result = NoteMetaEditor.SaveTitles(mProfile, mFolderFullPath, mMainBox.Text, mSubBox.Text, summaries, DateTime.Now);
        if (!result.Ok)
        {
            mMessage.Text = result.Message;
            mMessage.Foreground = BrushCache.Oxide;
            return;
        }

        mResult = result;
        DialogResult = true;
        Close();
    }

    private static string SourceLabel(string? source)
    {
        return source switch
        {
            NoteRevisionSource.Manual => "手改",
            NoteRevisionSource.Assistant => "助手",
            NoteRevisionSource.Publish => "上页",
            _ => string.IsNullOrWhiteSpace(source) ? "未标记" : source
        };
    }

    private static TextBlock Help(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 0, 0, 8)
        };
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static TextBlock Info(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushCache.Muted,
            Margin = new Thickness(0, 6, 0, 0)
        };
    }

    private static TextBox InputBox(string text)
    {
        return new TextBox
        {
            Text = text ?? "",
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            BorderBrush = BrushCache.Line,
            CaretBrush = BrushCache.Text,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };
    }
}
