namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 站点编目中的一条作品。
/// </summary>
public sealed class WorkCatalogItem
{
    public required string Id { get; init; }
    public required string Channel { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// 项目描述；游戏为 <c>lead</c>。未填可为空或占位句。
    /// </summary>
    public string Summary { get; init; } = "";

    public string SourceKind { get; init; } = "works";
    public bool SupportsAppend { get; init; } = true;
    public bool IsUnregistered { get; init; }

    /// <summary>
    /// 心得整篇已隐藏。访客索引不列出，工具里资源标已隐藏。
    /// </summary>
    public bool IsHidden { get; init; }
    public string? StageFolder { get; init; }
    public string Year { get; init; } = "";
    public string? StartedOn { get; init; }
    public string? Place { get; init; }

    /// <summary>
    /// 作品级类型键。旧三栏可空，空则上页仍把栏目本身当作类型。
    /// </summary>
    public IReadOnlyList<string> Themes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 作品级自由标签。
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public IReadOnlyList<WorkMediaItem> Media { get; init; } = Array.Empty<WorkMediaItem>();

    /// <summary>
    /// 已隐藏资源的星级。键为对象键。不进访客页。
    /// </summary>
    public Dictionary<string, int> ParkedStarDict { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 本作品已隐藏、仍在站点页的对象键。
    /// </summary>
    public HashSet<string> HiddenObjectSet { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 作品下的一条媒体。
/// </summary>
public sealed class WorkMediaItem
{
    public string Kind { get; init; } = "image";
    public string Label { get; init; } = "";

    /// <summary>
    /// 可选显示名；未填则网页仍用 <c>Label</c>。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 可选资源描述；未填则网页回退项目描述。
    /// </summary>
    public string? Description { get; init; }

    public string? Src { get; init; }

    /// <summary>
    /// 这一张的类型键。
    /// </summary>
    public IReadOnlyList<string> Themes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 这一张的自由标签。
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 0～5 星。缺省或 0 不写出，读侧视为 0。
    /// </summary>
    public int? Stars { get; init; }

    public string? Poster { get; init; }
    public int ReferenceCount { get; init; } = 1;
    public bool IsPrimary { get; init; }
}
