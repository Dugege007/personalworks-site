using System.IO;
using System.Windows;
using System.Windows.Threading;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 心得检视栏的标题和副标题。展示名自动拼接，已发布篇保存后标成待更新。
/// </summary>
public sealed partial class ShellViewModel
{
    private string mNoteMainTitle = "";
    private string mNoteSubtitle = "";
    private string mNoteTitleMessage = "";
    private string? mNoteTitleFolderId;
    private bool mShowNoteTitles;
    private bool mNoteTitleQuiet;
    private bool mNoteTitleReloadQueued;
    private string mNoteTitleReloadStatus = "";

    /// <summary>
    /// 检视栏里的标题。不包含自动拼接的展示名。
    /// </summary>
    public string NoteMainTitle
    {
        get => mNoteMainTitle;
        set
        {
            if (SetField(ref mNoteMainTitle, value ?? ""))
            {
                Raise(nameof(NoteDisplayTitleText));
            }
        }
    }

    /// <summary>
    /// 检视栏里的副标题。可空。
    /// </summary>
    public string NoteSubtitle
    {
        get => mNoteSubtitle;
        set
        {
            if (SetField(ref mNoteSubtitle, value ?? ""))
            {
                Raise(nameof(NoteDisplayTitleText));
            }
        }
    }

    /// <summary>
    /// 只读展示名。有副标题时为「标题——副标题」。
    /// </summary>
    public string NoteDisplayTitleText
    {
        get
        {
            var display = NoteRules.ComposeDisplayTitle(mNoteMainTitle, mNoteSubtitle);
            return display.Length == 0 ? "展示名会随标题自动生成。" : "展示名：" + display;
        }
    }

    /// <summary>
    /// 标题校验没通过时的说明。通过时为空。
    /// </summary>
    public string NoteTitleMessage
    {
        get => mNoteTitleMessage;
        private set => SetField(ref mNoteTitleMessage, value ?? "");
    }

    /// <summary>
    /// 是否显示心得标题和副标题。选中心得项目或正文时为真，配图仍用名称栏。
    /// </summary>
    public bool ShowNoteTitles
    {
        get => mShowNoteTitles;
        private set => SetField(ref mShowNoteTitles, value);
    }

    /// <summary>
    /// 当前标题栏对应的夹名。离开栏位时用它写回，避免选区已经换成下一篇。
    /// </summary>
    public string? NoteTitleFolderId => mNoteTitleFolderId;

    /// <summary>
    /// 把当前标题和副标题写入中转站。没有改动时不写盘。已发布且有改动时标成待更新。
    /// </summary>
    public void CommitNoteTitles()
    {
        if (mNoteTitleQuiet || mSession == null || string.IsNullOrWhiteSpace(mNoteTitleFolderId))
        {
            return;
        }

        var folderId = mNoteTitleFolderId;
        var folder = NoteFolderPath(folderId);
        var configPath = Path.Combine(folder, NoteConfigStore.FileName);
        if (!File.Exists(configPath))
        {
            return;
        }

        NoteConfig config;
        try
        {
            config = NoteConfigStore.Load(configPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoteTitleMessage = ex.Message;
            return;
        }

        var main = (mNoteMainTitle ?? "").Trim();
        var sub = (mNoteSubtitle ?? "").Trim();
        var display = NoteRules.ComposeDisplayTitle(main, sub);
        if (string.Equals(config.MainTitle ?? "", main, StringComparison.Ordinal)
            && string.Equals(config.Subtitle ?? "", sub, StringComparison.Ordinal)
            && string.Equals((config.Title ?? "").Trim(), display, StringComparison.Ordinal))
        {
            return;
        }

        var blocked = NoteRules.CheckDisplayTitle(main, sub);
        if (blocked.Verdict == NoteFolderNameVerdict.Block)
        {
            NoteTitleMessage = blocked.Message;
            StatusText = blocked.Message;
            return;
        }

        if (RefuseWorkspaceWrite())
        {
            return;
        }

        var summaries = config.Revisions.Select(item => item.Summary ?? "").ToList();
        NoteMetaSaveResult result;
        try
        {
            result = NoteMetaEditor.SaveTitles(
                mSession.Profile,
                folder,
                main,
                sub,
                summaries,
                DateTime.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            NoteTitleMessage = ex.Message;
            StatusText = ex.Message;
            return;
        }

        if (!result.Ok)
        {
            NoteTitleMessage = result.Message;
            StatusText = result.Message;
            return;
        }

        NoteTitleMessage = result.Message;
        if (!result.Changed)
        {
            return;
        }

        if (result.PendingUpdate)
        {
            NoteUpdateQueue.MarkBody(mSession.Profile, result.FolderName);
        }

        ScheduleNoteTitleReload(NoteTitleSaveStatus(result));
    }

    /// <summary>
    /// 保存成功后的底栏说明。待更新时写明下一步是更新站点稿，不是发布上线。
    /// </summary>
    private static string NoteTitleSaveStatus(NoteMetaSaveResult result)
    {
        var status = result.PendingUpdate
            ? "已保存标题。这篇已发布，已标为待更新。点右键「更新心得」或底栏「执行」即可写入站点稿，不会发布上线。"
            : "已保存标题。";
        if (string.IsNullOrWhiteSpace(result.Message))
        {
            return status;
        }

        return status + result.Message;
    }

    /// <summary>
    /// 装入这篇的标题和副标题。不写盘。
    /// </summary>
    private void ShowNoteTitleFields(string? folderId)
    {
        if (mSession == null || string.IsNullOrWhiteSpace(folderId))
        {
            HideNoteTitleFields();
            return;
        }

        var folder = NoteFolderPath(folderId);
        if (!Directory.Exists(folder))
        {
            HideNoteTitleFields();
            return;
        }

        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var config = new NoteConfig();
        var path = Path.Combine(folder, NoteConfigStore.FileName);
        if (File.Exists(path))
        {
            try
            {
                config = NoteConfigStore.Load(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                HideNoteTitleFields();
                NoteTitleMessage = ex.Message;
                return;
            }
        }

        var resolved = NoteConfigStore.ResolveTitles(folder, name, config);
        mNoteTitleQuiet = true;
        mNoteTitleFolderId = name;
        NoteMainTitle = resolved.Resolved ? resolved.MainTitle : "";
        NoteSubtitle = resolved.Resolved ? resolved.Subtitle : "";
        mNoteTitleQuiet = false;
        ShowNoteTitles = true;
        NoteTitleMessage = "";
    }

    /// <summary>
    /// 收起标题栏，避免把下一篇的字写回上一篇。
    /// </summary>
    private void HideNoteTitleFields()
    {
        mNoteTitleQuiet = true;
        mNoteTitleFolderId = null;
        NoteMainTitle = "";
        NoteSubtitle = "";
        mNoteTitleQuiet = false;
        ShowNoteTitles = false;
        NoteTitleMessage = "";
    }

    /// <summary>
    /// 保存后刷新编目，让左侧列表使用新的展示名，并挂上待更新标记。
    /// </summary>
    private void ScheduleNoteTitleReload(string status)
    {
        mNoteTitleReloadStatus = status;
        if (mNoteTitleReloadQueued)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        mNoteTitleReloadQueued = true;
        dispatcher.BeginInvoke(new Action(() =>
        {
            mNoteTitleReloadQueued = false;
            _ = FinishNoteTitleReloadAsync(mNoteTitleReloadStatus);
        }), DispatcherPriority.Background);
    }

    /// <summary>
    /// 编目重读后再写底栏。失败时保留加载失败的说明。
    /// </summary>
    private async Task FinishNoteTitleReloadAsync(string status)
    {
        if (mSession == null)
        {
            return;
        }

        var path = mSession.Profile.ProfilePath;
        if (!await ReloadAsync(path, coverGrid: false).ConfigureAwait(true))
        {
            return;
        }

        StatusText = status;
    }
}
