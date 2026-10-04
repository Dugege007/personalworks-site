namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 单条资源上的操作意图。
/// </summary>
public enum MediaIntent
{
    None = 0,
    StageIngest,
    StageRecycle,
    SiteHide,
    SiteRestore,
    SiteWithdraw,
    WorkRegister,
    WorkUpdate,
    WorkWithdraw,
    StageRelocate,
    CopyUpdate,
    MediaReorder,
    TagsUpdate,
    StarsUpdate
}

/// <summary>
/// 意图与契约字符串之间的转换。
/// </summary>
public static class MediaIntentCodes
{
    public const string StageIngest = "stage.ingest";
    public const string StageRecycle = "stage.recycle";
    public const string SiteHide = "site.hide";
    public const string SiteRestore = "site.restore";
    public const string SiteWithdraw = "site.withdraw";
    public const string WorkRegister = "work.register";
    public const string WorkUpdate = "work.update";
    public const string WorkWithdraw = "work.withdraw";
    public const string StageRelocate = "stage.relocate";
    public const string CopyUpdate = "copy.update";
    public const string MediaReorder = "media.reorder";
    public const string TagsUpdate = "tags.update";
    public const string StarsUpdate = "stars.update";

    /// <summary>
    /// 把枚举写成意图文件中的字符串。
    /// </summary>
    public static string ToCode(MediaIntent intent)
    {
        return intent switch
        {
            MediaIntent.StageIngest => StageIngest,
            MediaIntent.StageRecycle => StageRecycle,
            MediaIntent.SiteHide => SiteHide,
            MediaIntent.SiteRestore => SiteRestore,
            MediaIntent.SiteWithdraw => SiteWithdraw,
            MediaIntent.WorkRegister => WorkRegister,
            MediaIntent.WorkUpdate => WorkUpdate,
            MediaIntent.WorkWithdraw => WorkWithdraw,
            MediaIntent.StageRelocate => StageRelocate,
            MediaIntent.CopyUpdate => CopyUpdate,
            MediaIntent.MediaReorder => MediaReorder,
            MediaIntent.TagsUpdate => TagsUpdate,
            MediaIntent.StarsUpdate => StarsUpdate,
            _ => "none"
        };
    }

    /// <summary>
    /// 把意图文件中的字符串读回枚举。
    /// </summary>
    public static MediaIntent FromCode(string? code)
    {
        return code switch
        {
            StageIngest => MediaIntent.StageIngest,
            StageRecycle => MediaIntent.StageRecycle,
            SiteHide => MediaIntent.SiteHide,
            SiteRestore => MediaIntent.SiteRestore,
            SiteWithdraw => MediaIntent.SiteWithdraw,
            WorkRegister => MediaIntent.WorkRegister,
            WorkUpdate => MediaIntent.WorkUpdate,
            WorkWithdraw => MediaIntent.WorkWithdraw,
            StageRelocate => MediaIntent.StageRelocate,
            CopyUpdate => MediaIntent.CopyUpdate,
            MediaReorder => MediaIntent.MediaReorder,
            TagsUpdate => MediaIntent.TagsUpdate,
            StarsUpdate => MediaIntent.StarsUpdate,
            _ => MediaIntent.None
        };
    }
}
