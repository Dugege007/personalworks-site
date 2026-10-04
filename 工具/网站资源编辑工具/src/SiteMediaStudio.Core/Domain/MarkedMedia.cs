namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一条已标记资源；<see cref="WorkId"/> 为打标时的所属作品，切项目后仍按此写入意图。
/// </summary>
public readonly record struct MarkedMedia(
    MediaIntent Intent,
    StageItem? Stage,
    SiteItem? Site,
    string? WorkId = null);
