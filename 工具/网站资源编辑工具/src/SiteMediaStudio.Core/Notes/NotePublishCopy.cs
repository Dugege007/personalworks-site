namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得写入站点稿时的界面用词。索引里已有同夹的已发布条目时称「更新」，否则仍称「上页」。
/// 不把两步并成一次动作，也不写成「更新上页」。
/// </summary>
public static class NotePublishCopy
{
    /// <summary>
    /// 索引里还没有这篇时，按钮与确认框使用的动词。
    /// </summary>
    public const string PublishVerb = "上页";

    /// <summary>
    /// 索引里已有这篇时，按钮与确认框使用的动词。
    /// </summary>
    public const string UpdateVerb = "更新";

    /// <summary>
    /// 检视栏与确认框共用的两步说明。上页或更新只写仓库站点稿，发布上线才部署到公网。
    /// </summary>
    public const string TwoStepExplanation =
        "「上页」或「更新」只把中转站里的这篇心得同步写进仓库站点稿，包括 content/notes 和 index.json。「发布上线」才会把站点部署到公网。这两步是分开的，上页或更新不会自动发布上线。";

    /// <summary>
    /// 索引里是否已有同文件夹的已发布条目。草稿或缺少网址标识的条目不算已发布。
    /// </summary>
    public static bool IsPublished(NoteIndexFile? index, string? folderName)
    {
        return FindPublished(index, folderName) != null;
    }

    /// <summary>
    /// 找出同文件夹的已发布条目。没有时返回空。
    /// </summary>
    public static NoteEntry? FindPublished(NoteIndexFile? index, string? folderName)
    {
        if (index?.Notes == null || string.IsNullOrWhiteSpace(folderName))
        {
            return null;
        }

        return index.Notes.FirstOrDefault(item =>
            item != null
            && string.Equals(item.Folder, folderName, StringComparison.Ordinal)
            && !item.Draft
            && !string.IsNullOrWhiteSpace(item.Slug));
    }

    /// <summary>
    /// 读取心得索引。配置缺失或索引损坏时返回空表，避免界面把未知状态说成已发布。
    /// </summary>
    public static NoteIndexFile LoadOrEmpty(WorkspaceProfile? profile)
    {
        if (profile == null)
        {
            return new NoteIndexFile();
        }

        try
        {
            return NoteIndexStore.Load(profile);
        }
        catch (NoteIndexCorruptException)
        {
            return new NoteIndexFile();
        }
    }

    /// <summary>
    /// 从投放路径取出心得夹名。不是心得路径时返回空。
    /// </summary>
    public static string? FolderNameFromStage(WorkspaceProfile? profile, string? stageRel)
    {
        if (profile == null)
        {
            return null;
        }

        var channel = profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return null;
        }

        return NoteRules.NoteFolderFromStageRel(
            stageRel,
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel));
    }

    /// <summary>
    /// 已发布用「更新」，否则用「上页」。
    /// </summary>
    public static string Verb(bool published)
    {
        return published ? UpdateVerb : PublishVerb;
    }

    /// <summary>
    /// 右键菜单标题。
    /// </summary>
    public static string MenuHeader(bool published)
    {
        return Verb(published) + "心得";
    }

    /// <summary>
    /// 确认框标题，与菜单一致。
    /// </summary>
    public static string DialogTitle(bool published)
    {
        return MenuHeader(published);
    }

    /// <summary>
    /// 确认框主按钮。只写动词，不写成「更新上页」。
    /// </summary>
    public static string ConfirmButton(bool published)
    {
        return Verb(published);
    }

    /// <summary>
    /// 失败对话框标题。
    /// </summary>
    public static string FailureTitle(bool published)
    {
        return "无法" + Verb(published);
    }

    /// <summary>
    /// 写盘进度窗标题。
    /// </summary>
    public static string ProgressTitle(bool published)
    {
        return "正在" + Verb(published) + "心得";
    }

    /// <summary>
    /// 确认框正文。先写本次动作，再附两步说明。
    /// </summary>
    public static string ConfirmMessage(string? title, bool published)
    {
        var name = string.IsNullOrWhiteSpace(title) ? "这篇心得" : title.Trim();
        return "将" + Verb(published) + "「" + name + "」。未逐张标记配图时，夹内允许的图片全部压图入库。中转站正文仍用相对路径。"
            + "\n"
            + TwoStepExplanation;
    }

    /// <summary>
    /// 预演允许行。右键名称与菜单标题一致。
    /// </summary>
    public static string PreviewAllow(bool published)
    {
        var verb = Verb(published);
        return "将" + verb + "整篇心得，与右键「" + verb + "心得」相同。";
    }
}
