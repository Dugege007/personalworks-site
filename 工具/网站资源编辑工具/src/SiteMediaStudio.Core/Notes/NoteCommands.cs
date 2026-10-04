namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 隐藏、恢复与未上页回收。已上页整篇删除不在本阶段。
/// </summary>
public static class NoteCommands
{
    /// <summary>
    /// 隐藏整篇。公网要等发布上线。
    /// </summary>
    public static string? Hide(WorkspaceProfile profile, string folderName)
    {
        var index = NoteIndexStore.Load(profile);
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, folderName, StringComparison.Ordinal));
        if (entry == null || entry.Draft || string.IsNullOrWhiteSpace(entry.Slug))
        {
            return "这篇还没上页，访客本来就看不到。";
        }

        entry.Hidden = true;
        foreach (var image in entry.Images)
        {
            image.Hidden = true;
        }

        NoteIndexStore.Save(profile, index);
        return null;
    }

    /// <summary>
    /// 取消隐藏。
    /// </summary>
    public static string? Restore(WorkspaceProfile profile, string folderName)
    {
        var index = NoteIndexStore.Load(profile);
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, folderName, StringComparison.Ordinal));
        if (entry == null || !entry.Hidden)
        {
            return "这篇没有处于隐藏。";
        }

        entry.Hidden = false;
        foreach (var image in entry.Images)
        {
            image.Hidden = false;
        }

        NoteIndexStore.Save(profile, index);
        return null;
    }

    /// <summary>
    /// 把未上页的夹送入回收站。已上页则拒绝。
    /// </summary>
    public static string? RecycleFolder(WorkspaceProfile profile, string folderFullPath)
    {
        var folderName = Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var reason = NoteRules.RecycleBlockReason(NoteIndexStore.Load(profile), folderName);
        if (reason != null)
        {
            return reason;
        }

        RecycleService.SendToRecycleBin(folderFullPath);
        return null;
    }

    /// <summary>
    /// 把星级、标签、名称和描述写进已有索引。没有这篇则忽略。
    /// </summary>
    public static void ApplyMeta(WorkspaceProfile profile, IntentItem item)
    {
        if (!NoteRules.IsNotesChannel(item.Channel) || string.IsNullOrWhiteSpace(item.WorkId))
        {
            return;
        }

        var index = NoteIndexStore.Load(profile);
        var entry = index.Notes.FirstOrDefault(note =>
            string.Equals(note.Folder, item.WorkId, StringComparison.Ordinal));
        if (entry == null)
        {
            return;
        }

        var intent = MediaIntentCodes.FromCode(item.Intent);
        if (intent == MediaIntent.StarsUpdate && item.Stars is int stars)
        {
            var image = FindImage(entry, item.Object, item.StageRel);
            if (image != null)
            {
                image.Stars = stars;
            }
            else if (IsBody(item))
            {
                entry.Body.Stars = stars;
            }
        }
        else if (intent == MediaIntent.TagsUpdate)
        {
            var tags = item.Tags ?? new List<string>();
            var keys = item.ObjectList ?? new List<string>();
            if (keys.Count == 0 && !string.IsNullOrWhiteSpace(item.Object))
            {
                keys.Add(item.Object);
            }

            var matched = false;
            foreach (var key in keys)
            {
                var image = FindImage(entry, key, null);
                if (image == null)
                {
                    continue;
                }

                image.Tags = tags.ToList();
                matched = true;
            }

            if (!matched && IsBody(item))
            {
                entry.Body.Tags = tags.ToList();
            }
        }
        else if (intent == MediaIntent.CopyUpdate)
        {
            var image = FindImage(entry, item.Object, item.StageRel);
            if (image != null)
            {
                image.DisplayName = item.Title ?? "";
                image.Description = item.Description ?? "";
            }
            else if (IsBody(item))
            {
                entry.Body.DisplayName = item.Title ?? "";
                entry.Body.Description = item.Description ?? "";
            }
        }
        else
        {
            return;
        }

        NoteIndexStore.Save(profile, index);
    }

    /// <summary>
    /// 从本批意图里拿走心得的星级、标签和文案，改写索引。
    /// </summary>
    public static bool PullMeta(WorkspaceProfile profile, IntentDocument document)
    {
        var taken = document.Items.Where(item =>
            NoteRules.IsNotesChannel(item.Channel)
            && item.Intent is MediaIntentCodes.StarsUpdate or MediaIntentCodes.TagsUpdate or MediaIntentCodes.CopyUpdate)
            .ToList();
        if (taken.Count == 0)
        {
            return false;
        }

        foreach (var item in taken)
        {
            ApplyMeta(profile, item);
            document.Items.Remove(item);
        }

        return true;
    }

    private static NoteImageMeta? FindImage(NoteEntry entry, string? objectKey, string? stageRel)
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            var key = JsonUtil.ToRel(objectKey);
            var byObject = entry.Images.FirstOrDefault(item =>
                string.Equals(item.ObjectKey, key, StringComparison.Ordinal));
            if (byObject != null)
            {
                return byObject;
            }
        }

        if (!string.IsNullOrWhiteSpace(stageRel))
        {
            var rel = JsonUtil.ToRel(stageRel);
            return entry.Images.FirstOrDefault(item =>
                string.Equals(item.StageRel, rel, StringComparison.Ordinal));
        }

        return null;
    }

    private static bool IsBody(IntentItem item)
    {
        var rel = item.StageRel ?? item.Object ?? "";
        return rel.EndsWith(NoteRules.BodyFileName, StringComparison.Ordinal);
    }
}
