namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 对照行的配对来源。
/// </summary>
public enum CompareRowKind
{
    Paired,
    StageOnly,
    SiteOnly,
    ManualPin
}

/// <summary>
/// 对照视图中的一行：左投放箱、右站点。
/// </summary>
public sealed class CompareRow
{
    public required CompareRowKind Kind { get; init; }
    public StageItem? Stage { get; init; }
    public SiteItem? Site { get; init; }

    /// <summary>
    /// 行状态短标。
    /// </summary>
    public string StatusLabel => Kind switch
    {
        CompareRowKind.Paired => "已配对",
        CompareRowKind.ManualPin => "手动钉",
        CompareRowKind.StageOnly => "仅中转站",
        CompareRowKind.SiteOnly => "仅站点",
        _ => ""
    };
}

/// <summary>
/// 维护者指定的投放箱与站点对照。
/// </summary>
public sealed class ManualPin
{
    public string StageRel { get; set; } = "";
    public string Object { get; set; } = "";
    public string WorkId { get; set; } = "";
}
