namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把一篇心得压图、入库并写下发布稿。中转站正文保持相对路径。
/// </summary>
public static class NotePublisher
{
    /// <summary>
    /// 压一张图。返回是否成功。
    /// </summary>
    public delegate bool PrepareImage(string sourceFullPath, string readyFullPath, bool alreadyWebp);

    /// <summary>
    /// 入库一张成片。
    /// </summary>
    public delegate bool IngestImage(string readyStageRel, string objectKey, string sourceStageRel);

    /// <summary>
    /// 执行上页。失败时不改发布索引。
    /// </summary>
    public static NotePublishOutcome Publish(
        WorkspaceProfile profile,
        string folderFullPath,
        IReadOnlySet<string>? markedStageRels,
        IReadOnlyDictionary<string, int>? starByStageRel,
        PrepareImage prepare,
        IngestImage ingest)
    {
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot);
        var index = NoteIndexStore.Load(profile);
        var plan = NoteRules.Plan(folderFullPath, stageRoot, index, markedStageRels, starByStageRel);
        if (!plan.Ok)
        {
            return NotePublishOutcome.Fail(plan.Error);
        }

        var notesDir = NoteIndexStore.NotesDir(profile);
        if (notesDir == null)
        {
            return NotePublishOutcome.Fail("工作区没有站点内容目录。");
        }

        foreach (var image in plan.Images)
        {
            var readyFull = Path.Combine(stageRoot, image.ReadyStageRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(readyFull)!);
            if (!prepare(image.SourceFullPath, readyFull, image.AlreadyWebp))
            {
                return NotePublishOutcome.Fail("压图失败：" + image.SourceStageRel);
            }

            if (!ingest(image.ReadyStageRel, image.ObjectKey, image.SourceStageRel))
            {
                return NotePublishOutcome.Fail("入库失败：" + image.ObjectKey);
            }
        }

        Directory.CreateDirectory(notesDir);
        File.WriteAllText(
            Path.Combine(notesDir, plan.Slug + ".md"),
            plan.RewrittenMarkdown,
            JsonUtil.Utf8NoBom);
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, plan.FolderName, StringComparison.Ordinal));
        if (entry == null)
        {
            entry = new NoteEntry();
            index.Notes.Add(entry);
        }

        entry.Folder = plan.FolderName;
        entry.Slug = plan.Slug;
        entry.Title = plan.Title;
        entry.Date = plan.Date;
        entry.Summary = plan.Summary;
        entry.Draft = false;
        entry.Hidden = false;
        entry.PreviewObjects = plan.PreviewObjects;
        entry.Body = plan.Body;
        entry.Images = plan.Images.Select(item => item.Meta).ToList();
        NoteIndexStore.Save(profile, index);
        return new NotePublishOutcome
        {
            Ingested = plan.Images.Select(item => item.ObjectKey).ToList(),
            Retired = plan.RetiredObjects
        };
    }
}

/// <summary>
/// 一次上页的结果。
/// </summary>
public sealed class NotePublishOutcome
{
    public string? Error { get; init; }
    public List<string> Ingested { get; init; } = new();
    public List<string> Retired { get; init; } = new();

    /// <summary>
    /// 失败结果。
    /// </summary>
    public static NotePublishOutcome Fail(string error)
    {
        return new NotePublishOutcome { Error = error };
    }
}
