using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 保存心得标题、副标题与修订摘要。展示名只按规则拼接，夹名和站点稿都不在这一步改写。
/// </summary>
public static class NoteMetaEditor
{
    /// <summary>
    /// 修订摘要沿用这个入口。请求的名称与当前夹名相同时，不把夹名当成展示标题。
    /// 名称不同时，按「——」拆成标题和副标题再保存，仍然不重命名夹。
    /// </summary>
    public static NoteMetaSaveResult Save(
        WorkspaceProfile profile,
        string folderFullPath,
        string requestedFolderName,
        IReadOnlyList<string> revisionSummaries,
        DateTime localNow)
    {
        var currentFull = Path.GetFullPath(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var currentName = Path.GetFileName(currentFull);
        var requested = (requestedFolderName ?? "").Trim();
        if (string.Equals(currentName, requested, StringComparison.Ordinal))
        {
            if (!Directory.Exists(currentFull) || !File.Exists(Path.Combine(currentFull, NoteRules.BodyFileName)))
            {
                return NoteMetaSaveResult.Fail("没有正文.md。");
            }

            var config = NoteConfigStore.Ensure(currentFull, currentName);
            var resolved = NoteConfigStore.ResolveTitles(currentFull, currentName, config);
            if (!resolved.Resolved)
            {
                return SaveSummariesOnly(profile, currentFull, revisionSummaries);
            }

            return SaveTitles(profile, currentFull, resolved.MainTitle, resolved.Subtitle, revisionSummaries, localNow);
        }

        var parsed = NoteRules.ParseFolderTitles(requested);
        return SaveTitles(profile, folderFullPath, parsed.MainTitle, parsed.Subtitle, revisionSummaries, localNow);
    }

    /// <summary>
    /// 还没有可展示的标题时，只改修订摘要，不拿时间戳夹名去填标题。
    /// </summary>
    private static NoteMetaSaveResult SaveSummariesOnly(
        WorkspaceProfile profile,
        string folderFullPath,
        IReadOnlyList<string> revisionSummaries)
    {
        NoteIndexFile index;
        try
        {
            index = NoteIndexStore.Load(profile);
        }
        catch (NoteIndexCorruptException ex)
        {
            return NoteMetaSaveResult.Fail(ex.Message);
        }

        var currentName = Path.GetFileName(folderFullPath);
        var config = NoteConfigStore.Ensure(folderFullPath, currentName);
        if (revisionSummaries.Count != config.Revisions.Count)
        {
            return NoteMetaSaveResult.Fail("修订列表已变化，请重新打开后再改摘要。");
        }

        var summariesChanged = ApplySummaries(config, revisionSummaries);
        if (!summariesChanged)
        {
            return Unchanged(currentName, folderFullPath);
        }

        NoteConfigStore.Save(folderFullPath, config);
        return new NoteMetaSaveResult
        {
            Ok = true,
            FolderName = currentName,
            FolderFullPath = folderFullPath,
            Changed = true,
            PendingUpdate = IsPublished(index, currentName)
        };
    }

    /// <summary>
    /// 校验分开的标题和副标题，写入 <c>心得.json</c>。不重命名夹，也不改 <c>index.json</c> 与站点正文。
    /// 索引里已有同夹已发布条目且展示名或修订摘要有变化时，<see cref="NoteMetaSaveResult.PendingUpdate"/> 为真。
    /// </summary>
    public static NoteMetaSaveResult SaveTitles(
        WorkspaceProfile profile,
        string folderFullPath,
        string mainTitle,
        string subtitle,
        IReadOnlyList<string> revisionSummaries,
        DateTime localNow)
    {
        var currentFull = Path.GetFullPath(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!Directory.Exists(currentFull) || !File.Exists(Path.Combine(currentFull, NoteRules.BodyFileName)))
        {
            return NoteMetaSaveResult.Fail("没有正文.md。");
        }

        var check = NoteRules.CheckDisplayTitle(mainTitle, subtitle);
        if (check.Verdict == NoteFolderNameVerdict.Block)
        {
            return NoteMetaSaveResult.Fail(check.Message);
        }

        NoteIndexFile index;
        try
        {
            index = NoteIndexStore.Load(profile);
        }
        catch (NoteIndexCorruptException ex)
        {
            return NoteMetaSaveResult.Fail(ex.Message);
        }

        var currentName = Path.GetFileName(currentFull);
        var config = NoteConfigStore.Ensure(currentFull, currentName);
        if (revisionSummaries.Count != config.Revisions.Count)
        {
            return NoteMetaSaveResult.Fail("修订列表已变化，请重新打开后再改摘要。");
        }

        var summariesChanged = ApplySummaries(config, revisionSummaries, detectOnly: true);

        var main = check.Titles.MainTitle;
        var sub = check.Titles.Subtitle;
        var display = check.Titles.DisplayTitle;
        var previousTitle = (config.Title ?? "").Trim();
        var titleChanged = !string.Equals(previousTitle, display, StringComparison.Ordinal);
        var fieldsChanged = !string.Equals(config.MainTitle ?? "", main, StringComparison.Ordinal)
            || !string.Equals(config.Subtitle ?? "", sub, StringComparison.Ordinal);
        if (!summariesChanged && !titleChanged && !fieldsChanged)
        {
            return Unchanged(currentName, currentFull);
        }

        ApplySummaries(config, revisionSummaries);

        config.MainTitle = main;
        config.Subtitle = sub;
        config.Title = display;
        var recordedTitleRevision = titleChanged && previousTitle.Length > 0;
        if (recordedTitleRevision)
        {
            config.Revisions.Add(new NoteRevision
            {
                At = localNow.ToString(NoteConfigStore.TimeFormat, CultureInfo.InvariantCulture),
                Summary = "更新标题",
                Source = NoteRevisionSource.Manual
            });
        }

        // 手改修订已经说明的标题不再交给下一次发布重复生成。尚未记录的标题变化仍保留在快照里。
        NoteRevisionBaselineCapture.AcknowledgeDisplayedTitle(config, recordedTitleRevision);
        NoteConfigStore.Save(currentFull, config);

        return new NoteMetaSaveResult
        {
            Ok = true,
            Message = check.Verdict == NoteFolderNameVerdict.Warn ? check.Message : "",
            FolderName = currentName,
            FolderFullPath = currentFull,
            Changed = true,
            PendingUpdate = IsPublished(index, currentName) && (titleChanged || summariesChanged)
        };
    }

    /// <summary>
    /// 写回修订摘要。只检查时不改配置。有变化则把来源记为手改。
    /// </summary>
    private static bool ApplySummaries(NoteConfig config, IReadOnlyList<string> revisionSummaries, bool detectOnly = false)
    {
        var changed = false;
        for (var i = 0; i < config.Revisions.Count; i++)
        {
            var summary = revisionSummaries[i] ?? "";
            if (string.Equals(config.Revisions[i].Summary, summary, StringComparison.Ordinal))
            {
                continue;
            }

            changed = true;
            if (detectOnly)
            {
                continue;
            }

            config.Revisions[i].Summary = summary;
            config.Revisions[i].Source = NoteRevisionSource.Manual;
        }

        return changed;
    }

    /// <summary>
    /// 索引里是否已有这个夹的已发布条目。
    /// </summary>
    private static bool IsPublished(NoteIndexFile index, string folderName)
    {
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, folderName, StringComparison.Ordinal));
        return entry != null && !entry.Draft && !string.IsNullOrWhiteSpace(entry.Slug);
    }

    /// <summary>
    /// 没有改动时的成功结果。不写盘。
    /// </summary>
    private static NoteMetaSaveResult Unchanged(string folderName, string folderFullPath)
    {
        return new NoteMetaSaveResult
        {
            Ok = true,
            FolderName = folderName,
            FolderFullPath = folderFullPath
        };
    }
}
