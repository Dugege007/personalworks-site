namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 索引有条目、中转站却没有对应夹时的提示。不提供绕过中转站、直接改站点稿的入口。
/// </summary>
public static class NoteStagePresence
{
    /// <summary>
    /// 列表与底栏使用的短句。
    /// </summary>
    public const string MissingFolderNotice = "请先回填中转站";

    /// <summary>
    /// 选中缺夹条目时，检视栏使用的完整说明。
    /// </summary>
    public const string MissingFolderDetail = "索引里已有这篇心得，中转站里还没有对应的文件夹。请先回填中转站。";

    /// <summary>
    /// 列出索引中有、栏目目录下却没有同名夹的条目。同一夹名只保留第一条。
    /// </summary>
    public static IReadOnlyList<NoteEntry> MissingEntries(NoteIndexFile? index, string? channelDirectory)
    {
        var presentSet = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(channelDirectory) && Directory.Exists(channelDirectory))
        {
            foreach (var dir in Directory.EnumerateDirectories(channelDirectory))
            {
                presentSet.Add(Path.GetFileName(dir));
            }
        }

        var entryList = new List<NoteEntry>();
        var seenSet = new HashSet<string>(StringComparer.Ordinal);
        if (index?.Notes == null)
        {
            return entryList;
        }

        foreach (var entry in index.Notes)
        {
            var folder = (entry?.Folder ?? "").Trim();
            if (folder.Length == 0 || !seenSet.Add(folder) || presentSet.Contains(folder))
            {
                continue;
            }

            entryList.Add(entry!);
        }

        return entryList;
    }
}
