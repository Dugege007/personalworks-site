using System.Text.Json.Serialization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 修订条目的生成来源。网页只读元数据，不读 Git。
/// </summary>
public static class NoteRevisionSource
{
    /// <summary>
    /// 用户在资源编辑工具中手改摘要，或因夹名变更写下的标题修订。
    /// </summary>
    public const string Manual = "manual";

    /// <summary>
    /// 助手根据内容差异生成。本阶段只预留取值，自动摘要在后续阶段接入。
    /// </summary>
    public const string Assistant = "assistant";

    /// <summary>
    /// 上页或从旧 <c>modifiedAt</c> 迁入时由工具写入。摘要可先为空。
    /// </summary>
    public const string Publish = "publish";
}

/// <summary>
/// 一次修订。时间新的排在后面。
/// </summary>
public sealed class NoteRevision
{
    public string At { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// 夹内 <c>心得.json</c>。站点展示用的主副标题来自夹名，不写进 Markdown 开头。
/// </summary>
public sealed class NoteConfig
{
    public string CreatedAt { get; set; } = "";

    /// <summary>
    /// 旧稿里的修改时间戳。读取时迁入 <see cref="Revisions"/>，新写不再落盘。
    /// </summary>
    public List<string>? ModifiedAt { get; set; }

    public List<NoteRevision> Revisions { get; set; } = new();

    /// <summary>
    /// 展示用完整标题。有副标题时为「主标题——副标题」。
    /// </summary>
    public string Title { get; set; } = "";

    public string MainTitle { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Slug { get; set; } = "";

    /// <summary>
    /// 列表用短摘要，与单次修订说明不是同一字段。
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// 本次读取把旧 <c>modifiedAt</c> 收进了修订列表，或丢弃了已被修订列表取代的旧字段。下次保存应写回。
    /// </summary>
    [JsonIgnore]
    public bool LegacyModifiedAtMigrated { get; set; }
}

/// <summary>
/// 心得发布索引里的一篇。
/// </summary>
public sealed class NoteEntry
{
    public string Folder { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string MainTitle { get; set; } = "";
    public string Subtitle { get; set; } = "";

    /// <summary>
    /// 创建日 <c>YYYY-MM-DD</c>。排序仍用这一天，不用最近修改日覆盖。
    /// </summary>
    public string Date { get; set; } = "";

    public string CreatedAt { get; set; } = "";

    /// <summary>
    /// 最近一次修订时间。没有修订时与 <see cref="CreatedAt"/> 相同。
    /// </summary>
    public string RevisedAt { get; set; } = "";

    public string Summary { get; set; } = "";
    public List<NoteRevision> Revisions { get; set; } = new();
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
    /// <summary>
    /// 带主副标题与修订列表的索引。读取端仍接受缺省或为 1 的旧文件。
    /// </summary>
    public const int CurrentVersion = 2;

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

    /// <summary>
    /// 导致整篇不能上页的文件或引用。相对心得夹，或正文里的原始引用。没有具体文件时为空。
    /// </summary>
    public string BlockingReference { get; init; } = "";
    public string FolderName { get; init; } = "";
    public string Slug { get; init; } = "";
    public string Title { get; init; } = "";
    public string MainTitle { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Date { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string RevisedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public List<NoteRevision> Revisions { get; init; } = new();
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

/// <summary>
/// 从夹名拆出的主副标题。
/// </summary>
public sealed class NoteFolderTitles
{
    public string MainTitle { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
}

/// <summary>
/// 夹名长度与合法性的三级结果。警告仍可保存，禁止则不能保存。
/// </summary>
public enum NoteFolderNameVerdict
{
    Ok,
    Warn,
    Block
}

/// <summary>
/// 夹名校验结果。计数含破折号，按字符串长度。
/// </summary>
public sealed class NoteFolderNameCheck
{
    public NoteFolderNameVerdict Verdict { get; init; }
    public string Message { get; init; } = "";
    public int Length { get; init; }
    public NoteFolderTitles Titles { get; init; } = new();
}

/// <summary>
/// 按夹名与正文解析出的展示标题。时间戳夹名不会被当成标题。
/// </summary>
public sealed class NoteTitleSet
{
    public bool Resolved { get; init; }
    public string MainTitle { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
}

/// <summary>
/// 资源编辑工具保存夹名与修订摘要的结果。
/// </summary>
public sealed class NoteMetaSaveResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public string FolderName { get; init; } = "";
    public string FolderFullPath { get; init; } = "";

    /// <summary>
    /// 失败结果。夹名保持原样。
    /// </summary>
    public static NoteMetaSaveResult Fail(string message)
    {
        return new NoteMetaSaveResult { Message = message };
    }
}
