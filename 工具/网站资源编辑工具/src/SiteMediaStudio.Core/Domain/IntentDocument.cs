namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一批标记操作的可执行描述。
/// </summary>
public sealed class IntentDocument
{
    public int Version { get; set; } = 1;
    public string WorkspaceRoot { get; set; } = "";
    public string? ProfileName { get; set; }
    public string Mode { get; set; } = "prompt";
    public string CreatedAt { get; set; } = "";
    public List<IntentItem> Items { get; set; } = new();
    public IntentOptions Options { get; set; } = new();
}

/// <summary>
/// 意图文件中的单条。
/// </summary>
public sealed class IntentItem
{
    public string Intent { get; set; } = "";
    public string? WorkId { get; set; }
    public string? Channel { get; set; }
    public string? Object { get; set; }
    public string? StageRel { get; set; }

    /// <summary>
    /// 中转站原片相对路径；压成 <c>.site-ready</c> 后 <see cref="StageRel"/> 会改写，此字段仍指向原名。
    /// </summary>
    public string? SourceStageRel { get; set; }

    public string? StageFolder { get; set; }
    public string? StageRelBefore { get; set; }
    public string? StageRelAfter { get; set; }
    public string? Title { get; set; }
    public string? StartedOn { get; set; }
    public string? Place { get; set; }
    public string? Year { get; set; }

    /// <summary>
    /// 摄影类型键。登记与 <c>tags.update</c> 使用，顺序不保证，写入时再排。
    /// </summary>
    public List<string>? Themes { get; set; }

    /// <summary>
    /// 自由标签。四位年份与类型键不进此列。
    /// </summary>
    public List<string>? Tags { get; set; }

    public string? LabelBefore { get; set; }

    /// <summary>
    /// 文案目标：<c>channel</c> / <c>work</c> / <c>media</c>。
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// 文案描述；空串表示未填并删键。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 站点显示顺序；仅 <c>media.reorder</c> 使用，值为该作品带 <c>src</c> 的对象键。
    /// </summary>
    public List<string>? ObjectList { get; set; }

    /// <summary>
    /// 0～5 星。仅 <c>stars.update</c> 使用。0 表示清除字段。
    /// </summary>
    public int? Stars { get; set; }
}

/// <summary>
/// 批次级选项。
/// </summary>
public sealed class IntentOptions
{
    public bool Relabel { get; set; } = true;
    public string Deploy { get; set; } = "none";
    public bool Bump { get; set; }
}

/// <summary>
/// 预演报告中的一行。
/// </summary>
public sealed class PreviewLine
{
    public required IntentItem Item { get; init; }
    public required GateDecision Decision { get; init; }
}

/// <summary>
/// 一次预演的汇总。
/// </summary>
public sealed class PreviewReport
{
    public required IntentDocument Document { get; init; }
    public IReadOnlyList<PreviewLine> Lines { get; init; } = Array.Empty<PreviewLine>();
    public string PatchPreview { get; init; } = "";
    public bool HasHardError => Lines.Any(line => !line.Decision.Allowed);
}
