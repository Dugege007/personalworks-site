namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 内容层已引用的一份图像。
/// </summary>
public sealed class SiteItem
{
    public required string WorkId { get; init; }
    public required string ChannelKey { get; init; }
    public required string WorkTitle { get; init; }

    /// <summary>
    /// 所属项目已发布描述。
    /// </summary>
    public string WorkSummary { get; init; } = "";

    public required string Label { get; init; }

    /// <summary>
    /// 资源可选显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 资源可选描述。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 内容层星级。缺字段为 0。
    /// </summary>
    public int Stars { get; init; }

    public required string ObjectKey { get; init; }
    public int Index { get; init; }
    public int Total { get; init; }
    public string? StageRel { get; init; }

    /// <summary>
    /// 台账原片相对路径；无则空。
    /// </summary>
    public string? SourceStageRel { get; init; }
    public string? FullPath { get; init; }
    public string? LedgerStatus { get; init; }
    public int ReferenceCount { get; init; }
    public bool IsStock { get; init; }

    /// <summary>
    /// 台账仍在、内容层已无引用，仅站点网格展示。
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// 只改显示序号，其它字段保持不变。
    /// </summary>
    public SiteItem WithDisplay(int index, int total)
    {
        return new SiteItem
        {
            WorkId = WorkId,
            ChannelKey = ChannelKey,
            WorkTitle = WorkTitle,
            WorkSummary = WorkSummary,
            Label = Label,
            DisplayName = DisplayName,
            Description = Description,
            Stars = Stars,
            ObjectKey = ObjectKey,
            Index = index,
            Total = total,
            StageRel = StageRel,
            SourceStageRel = SourceStageRel,
            FullPath = FullPath,
            LedgerStatus = LedgerStatus,
            ReferenceCount = ReferenceCount,
            IsStock = IsStock,
            IsHidden = IsHidden
        };
    }
}
