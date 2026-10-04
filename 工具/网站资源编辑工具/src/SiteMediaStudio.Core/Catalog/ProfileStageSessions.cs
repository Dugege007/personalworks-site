namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 形象照场次夹是投放整理，不是未登记作品；每个夹各成一条已入编场次。
/// </summary>
public static class ProfileStageSessions
{
    /// <summary>
    /// 把投放箱里尚未入编的形象照场次夹收成场次作品，不进未登记。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Merge(
        IReadOnlyList<WorkCatalogItem> registeredList,
        IReadOnlyList<StageItem> stageList)
    {
        var resultList = registeredList.ToList();
        var knownSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in resultList)
        {
            if (work.Channel != "profile" || work.IsUnregistered)
            {
                continue;
            }

            Remember(knownSet, work.Id);
            Remember(knownSet, work.Title);
            var folderId = WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder);
            Remember(knownSet, folderId);
            Remember(knownSet, WorkRegisterRules.SeedTitle(folderId));
        }

        foreach (var item in stageList)
        {
            if (item.ChannelKey != "profile"
                || string.IsNullOrWhiteSpace(item.WorkIdGuess)
                || item.IsStock
                || MediaPathRules.IsStock(item.WorkIdGuess)
                || item.WorkIdGuess.StartsWith('.'))
            {
                continue;
            }

            var folderId = item.WorkIdGuess;
            var title = WorkRegisterRules.SeedTitle(folderId);
            if (knownSet.Contains(folderId) || knownSet.Contains(title))
            {
                continue;
            }

            var stageFolder = string.IsNullOrWhiteSpace(item.StageFolderGuess)
                ? WorkRegisterRules.StageFolder("profile", folderId)
                : JsonUtil.ToRel("profile/" + item.StageFolderGuess);
            resultList.Add(new WorkCatalogItem
            {
                Id = title,
                Channel = "profile",
                Title = title,
                SourceKind = "site-profile",
                SupportsAppend = true,
                StageFolder = stageFolder,
                Media = Array.Empty<WorkMediaItem>()
            });
            Remember(knownSet, folderId);
            Remember(knownSet, title);
        }

        return resultList;
    }

    /// <summary>
    /// 写入非空认领键。
    /// </summary>
    private static void Remember(ISet<string> knownSet, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            knownSet.Add(value);
        }
    }
}
