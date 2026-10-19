namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 已发布心得在工具里保存后，把正文标成待更新。相当于按过 I，但这一步只排队，不写站点稿。
/// </summary>
public static class NoteUpdateQueue
{
    /// <summary>
    /// 这篇正文在投放箱里的相对路径，与网格卡片键一致。
    /// </summary>
    public static string BodyStageRel(WorkspaceProfile profile, string folderName)
    {
        var channel = profile.Channels.First(item => NoteRules.IsNotesChannel(item.Key));
        var folder = WorkspaceProfileLoader.ChannelStageRelative(profile, channel);
        return JsonUtil.ToRel(folder + "/" + folderName + "/" + NoteRules.BodyFileName);
    }

    /// <summary>
    /// 写入上页标记。执行时已发布篇走「更新」，只写站点稿，不发布上线。
    /// </summary>
    public static void MarkBody(WorkspaceProfile profile, string folderName, string? markPath = null)
    {
        MarkDraftStore.Commit(
            profile,
            MarkDraftStore.StageKey(BodyStageRel(profile, folderName)),
            MediaIntent.StageIngest,
            NoteRules.ChannelKey,
            folderName,
            markPath);
    }
}
