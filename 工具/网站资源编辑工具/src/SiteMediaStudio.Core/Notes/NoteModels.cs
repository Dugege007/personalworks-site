namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 夹内 <c>心得.json</c>。标题来自正文一级标题，不写进 Markdown 开头。
/// </summary>
public sealed class NoteConfig
{
    public string CreatedAt { get; set; } = "";
    public List<string> ModifiedAt { get; set; } = new();
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
}

/// <summary>
/// 心得发布索引里的一篇。
/// </summary>
public sealed class NoteEntry
{
    public string Folder { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool Draft { get; set; }
    public bool Hidden { get; set; }
    public List<string> PreviewObjects { get; set; } = new();
    public NoteResourceMeta Body { get; set; } = new();
    public List<NoteImageMeta> Images { get; set; } = new();
}

/// <summary>
/// 正文卡上的星级、标签与检视文案。
/// </summary>
public sealed class NoteResourceMeta
{
    public int Stars { get; set; }
    public List<string> Tags { get; set; } = new();
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// 一张已入库或待入库的配图。
/// </summary>
public sealed class NoteImageMeta
{
    public string StageRel { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public int Stars { get; set; }
    public List<string> Tags { get; set; } = new();
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Hidden { get; set; }
}

/// <summary>
/// <c>index.json</c> 根。
/// </summary>
public sealed class NoteIndexFile
{
    public int Version { get; set; } = 1;
    public List<NoteEntry> Notes { get; set; } = new();
}

/// <summary>
/// 一篇上页前的计划，不写盘。
/// </summary>
public sealed class NotePublishPlan
{
    public bool Ok { get; init; }
    public string Error { get; init; } = "";
    public string FolderName { get; init; } = "";
    public string Slug { get; init; } = "";
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Summary { get; init; } = "";
    public string RewrittenMarkdown { get; init; } = "";
    public List<NoteImageJob> Images { get; init; } = new();
    public List<string> PreviewObjects { get; init; } = new();
    public List<string> RetiredObjects { get; init; } = new();
    public NoteResourceMeta Body { get; init; } = new();
}

/// <summary>
/// 一张要压图并入库的原片。
/// </summary>
public sealed class NoteImageJob
{
    public string SourceFullPath { get; init; } = "";
    public string SourceStageRel { get; init; } = "";
    public string ReadyStageRel { get; init; } = "";
    public string ObjectKey { get; init; } = "";
    public bool AlreadyWebp { get; init; }
    public NoteImageMeta Meta { get; init; } = new();
}
