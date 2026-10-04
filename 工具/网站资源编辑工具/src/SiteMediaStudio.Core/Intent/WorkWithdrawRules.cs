namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 整项撤下：收集一部作品要在发布时删掉的对象键，含视频伴生封面。
/// </summary>
public static class WorkWithdrawRules
{
    /// <summary>
    /// 正式位根目录。配置尚未解析出工作区根时返回空。
    /// </summary>
    public static string? ResolvePlaceholders(WorkspaceProfile? profile)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.ResolvedRoot)
            || string.IsNullOrWhiteSpace(profile.PlaceholdersRoot))
        {
            return null;
        }

        return WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.PlaceholdersRoot);
    }

    /// <summary>
    /// 该作品内容里的 src、显式 poster，以及台账或正式位里已经存在的伴生封面。
    /// </summary>
    public static List<string> CollectObjectKeys(
        WorkCatalogItem work,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict = null,
        string? placeholdersRoot = null)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var media in work.Media)
        {
            Add(list, seen, media.Src);
            Add(list, seen, media.Poster);
            if (string.IsNullOrWhiteSpace(media.Poster)
                && VideoEncodeRules.IsFormalVideoObject(media.Src))
            {
                var poster = VideoEncodeRules.PosterObjectKey(media.Src!);
                if (HasLedgerOrFile(poster, ledgerDict, placeholdersRoot))
                {
                    Add(list, seen, poster);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// 按栏目与作品编号找到已入编记录。
    /// </summary>
    public static WorkCatalogItem? FindWork(IEnumerable<WorkCatalogItem>? workList, string? channel, string? workId)
    {
        if (workList == null || string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(workId))
        {
            return null;
        }

        return workList.FirstOrDefault(work =>
            string.Equals(work.Channel, channel, StringComparison.Ordinal)
            && string.Equals(work.Id, workId, StringComparison.Ordinal));
    }

    private static void Add(List<string> list, HashSet<string> seen, string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        var rel = JsonUtil.ToRel(objectKey);
        if (rel.Length == 0 || !seen.Add(rel))
        {
            return;
        }

        list.Add(rel);
    }

    private static bool HasLedgerOrFile(
        string objectKey,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        string? placeholdersRoot)
    {
        if (ledgerDict != null && ledgerDict.ContainsKey(objectKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(placeholdersRoot))
        {
            return false;
        }

        var abs = Path.Combine(placeholdersRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(abs);
    }
}
