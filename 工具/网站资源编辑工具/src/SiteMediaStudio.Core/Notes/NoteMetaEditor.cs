using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 保存心得夹名与修订摘要。夹名决定主副标题；slug 保持原有 ASCII 值。
/// </summary>
public static class NoteMetaEditor
{
    /// <summary>
    /// 校验夹名、写回修订摘要，并在夹名变化时重命名。禁止项不落盘。
    /// </summary>
    public static NoteMetaSaveResult Save(
        WorkspaceProfile profile,
        string folderFullPath,
        string requestedFolderName,
        IReadOnlyList<string> revisionSummaries,
        DateTime localNow)
    {
        var currentFull = Path.GetFullPath(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!Directory.Exists(currentFull) || !File.Exists(Path.Combine(currentFull, NoteRules.BodyFileName)))
        {
            return NoteMetaSaveResult.Fail("没有正文.md。");
        }

        var requested = (requestedFolderName ?? "").Trim();
        var check = NoteRules.CheckFolderName(requested);
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

        for (var i = 0; i < config.Revisions.Count; i++)
        {
            var summary = revisionSummaries[i] ?? "";
            if (string.Equals(config.Revisions[i].Summary, summary, StringComparison.Ordinal))
            {
                continue;
            }

            config.Revisions[i].Summary = summary;
            config.Revisions[i].Source = NoteRevisionSource.Manual;
        }

        var previousTitle = config.Title ?? "";
        var resolved = NoteConfigStore.ResolveTitles(currentFull, requested, config);
        if (resolved.Resolved)
        {
            config.MainTitle = resolved.MainTitle;
            config.Subtitle = resolved.Subtitle;
            config.Title = resolved.DisplayTitle;
        }

        var renamed = !string.Equals(currentName, requested, StringComparison.Ordinal);
        if (renamed
            && !string.IsNullOrWhiteSpace(previousTitle)
            && !string.Equals(previousTitle, config.Title, StringComparison.Ordinal))
        {
            config.Revisions.Add(new NoteRevision
            {
                At = localNow.ToString(NoteConfigStore.TimeFormat, CultureInfo.InvariantCulture),
                Summary = "更新标题",
                Source = NoteRevisionSource.Manual
            });
        }

        var parent = Path.GetDirectoryName(currentFull);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return NoteMetaSaveResult.Fail("找不到心得夹的上级目录。");
        }

        var target = renamed ? Path.Combine(parent, requested) : currentFull;
        if (renamed && Directory.Exists(target))
        {
            return NoteMetaSaveResult.Fail("这个夹名已经有了。");
        }

        if (renamed)
        {
            Directory.Move(currentFull, target);
        }

        NoteConfigStore.Save(target, config);
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, currentName, StringComparison.Ordinal));
        if (entry != null)
        {
            entry.Folder = requested;
            entry.Title = config.Title ?? "";
            entry.MainTitle = config.MainTitle ?? "";
            entry.Subtitle = config.Subtitle ?? "";
            entry.CreatedAt = config.CreatedAt ?? "";
            entry.RevisedAt = NoteConfigStore.RevisedAt(config) ?? "";
            entry.Revisions = config.Revisions.Select(NoteConfigStore.CopyRevision).ToList();
            if (index.Version < NoteIndexFile.CurrentVersion)
            {
                index.Version = NoteIndexFile.CurrentVersion;
            }

            NoteIndexStore.Save(profile, index);
        }

        return new NoteMetaSaveResult
        {
            Ok = true,
            Message = check.Verdict == NoteFolderNameVerdict.Warn ? check.Message : "",
            FolderName = requested,
            FolderFullPath = target
        };
    }
}
