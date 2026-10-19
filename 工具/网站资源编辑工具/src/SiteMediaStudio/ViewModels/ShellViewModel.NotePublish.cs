using System.IO;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 心得上页与更新的文案、两步说明，以及未同步和缺夹提示。
/// </summary>
public sealed partial class ShellViewModel
{
    private string mNotePublishMenuHeader = NotePublishCopy.MenuHeader(false);
    private string mNoteStageNotice = "";
    private string mNotePublishHelp = "";

    /// <summary>
    /// 右键「上页心得」或「更新心得」。
    /// </summary>
    public string NotePublishMenuHeader => mNotePublishMenuHeader;

    /// <summary>
    /// 未同步或缺少中转站夹时的提示。空字符串表示不显示。
    /// </summary>
    public string NoteStageNotice => mNoteStageNotice;

    /// <summary>
    /// 是否显示未同步或回填提示。
    /// </summary>
    public bool ShowNoteStageNotice => mNoteStageNotice.Length > 0;

    /// <summary>
    /// 选中已有夹的心得时，说明上页或更新与发布上线是分开的两步。
    /// </summary>
    public string NotePublishHelp => mNotePublishHelp;

    /// <summary>
    /// 是否显示两步说明。
    /// </summary>
    public bool ShowNotePublishHelp => mNotePublishHelp.Length > 0;

    /// <summary>
    /// 按当前导航刷新菜单用词、两步说明和底栏提示。不写站点稿。
    /// </summary>
    private void RefreshNotePublishChrome()
    {
        var header = NotePublishCopy.MenuHeader(false);
        var notice = "";
        var help = "";
        var work = mSelectedNav?.Work;
        if (mSession != null && work is { SourceKind: "note", MissingStageFolder: true })
        {
            notice = NoteStagePresence.MissingFolderNotice;
        }
        else if (mSession != null && work is { SourceKind: "note" })
        {
            var published = IsNoteFolderPublished(work.Id);
            header = NotePublishCopy.MenuHeader(published);
            help = NotePublishCopy.TwoStepExplanation;
            if (published && NoteFolderHasUnsyncedSiteChanges(work.Id))
            {
                notice = NoteSiteDrift.UnsyncedNotice;
            }
        }
        else if (mSession != null
            && mSelectedNav != null
            && NoteRules.IsNotesChannel(mSelectedNav.Channel.Key)
            && mSelectedNav.Work == null
            && !mSelectedNav.IsYearGroup
            && !mSelectedNav.IsUnregisteredGroup
            && ChannelHasMissingNoteFolder())
        {
            notice = NoteStagePresence.MissingFolderNotice;
        }

        SetNotePublishText(ref mNotePublishMenuHeader, header, nameof(NotePublishMenuHeader));
        SetNotePublishText(ref mNoteStageNotice, notice, nameof(NoteStageNotice), nameof(ShowNoteStageNotice));
        SetNotePublishText(ref mNotePublishHelp, help, nameof(NotePublishHelp), nameof(ShowNotePublishHelp));
    }

    /// <summary>
    /// 索引里是否已有这个夹的已发布条目。索引读不出时按未发布处理。
    /// </summary>
    private bool IsNoteFolderPublished(string folderId)
    {
        if (mSession == null)
        {
            return false;
        }

        return NotePublishCopy.IsPublished(NotePublishCopy.LoadOrEmpty(mSession.Profile), folderId);
    }

    /// <summary>
    /// 这篇已发布心得的中转站是否还有没写进站点稿的修改。读不到站点稿时不把界面判成脏，以免误报。
    /// </summary>
    private bool NoteFolderHasUnsyncedSiteChanges(string folderId)
    {
        if (mSession == null)
        {
            return false;
        }

        try
        {
            return NoteSiteDrift.HasUnsyncedSiteChanges(
                NoteFolderPath(folderId),
                NotePublishCopy.LoadOrEmpty(mSession.Profile),
                NoteIndexStore.NotesDir(mSession.Profile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 心得栏目下是否存在索引有、中转站没有的夹。
    /// </summary>
    private bool ChannelHasMissingNoteFolder()
    {
        if (mSession == null)
        {
            return false;
        }

        var channel = mSession.Profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return false;
        }

        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot);
        var channelDir = Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel).Replace('/', Path.DirectorySeparatorChar));
        return NoteStagePresence.MissingEntries(NotePublishCopy.LoadOrEmpty(mSession.Profile), channelDir).Count > 0;
    }

    /// <summary>
    /// 文案有变化时通知界面。额外名称用于可见性之类的派生属性。
    /// </summary>
    private void SetNotePublishText(ref string field, string value, string propertyName, string? extraPropertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Raise(propertyName);
        if (extraPropertyName != null)
        {
            Raise(extraPropertyName);
        }
    }
}
