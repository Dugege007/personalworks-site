namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 对象台账中的一条记录。
/// </summary>
public sealed class LedgerRecord
{
    public string Object { get; init; } = "";
    public string? StageRel { get; init; }
    public string? SourceStageRel { get; init; }
    public string Status { get; init; } = "";
}
