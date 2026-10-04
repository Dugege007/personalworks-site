namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把栏目下的作品按投放箱年份夹分成左侧树的一层。未开启年份容器时不分组。
/// </summary>
public static class NavYearLayout
{
    /// <summary>
    /// 按年份夹归并已入编与未登记作品。没有年份夹的作品留在栏根。
    /// </summary>
    public static Result Arrange(
        ChannelProfile channel,
        IReadOnlyList<WorkCatalogItem> registeredList,
        IReadOnlyList<WorkCatalogItem> unregisteredList,
        IEnumerable<StageItem> stageItems)
    {
        if (!channel.YearContainers)
        {
            return new Result(registeredList, Array.Empty<YearRow>(), unregisteredList);
        }

        var yearByFolderId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in stageItems)
        {
            if (!string.Equals(item.ChannelKey, channel.Key, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(item.WorkIdGuess)
                || !StageWorkFolder.TryReadYearFolder(channel, item.StageFolderGuess, out var year))
            {
                continue;
            }

            yearByFolderId.TryAdd(item.WorkIdGuess, year);
        }

        var yearWorkDict = new SortedDictionary<string, List<WorkCatalogItem>>(StringComparer.Ordinal);
        var looseRegistered = new List<WorkCatalogItem>();
        var looseUnregistered = new List<WorkCatalogItem>();
        Place(channel, registeredList, yearByFolderId, yearWorkDict, looseRegistered);
        Place(channel, unregisteredList, yearByFolderId, yearWorkDict, looseUnregistered);
        var rowList = yearWorkDict
            .Select(pair => new YearRow(pair.Key, pair.Value))
            .ToList();
        return new Result(looseRegistered, rowList, looseUnregistered);
    }

    /// <summary>
    /// 年份行与栏根上未进年份夹的作品。
    /// </summary>
    public sealed class Result
    {
        public Result(
            IReadOnlyList<WorkCatalogItem> looseRegistered,
            IReadOnlyList<YearRow> yearRows,
            IReadOnlyList<WorkCatalogItem> looseUnregistered)
        {
            LooseRegistered = looseRegistered;
            YearRows = yearRows;
            LooseUnregistered = looseUnregistered;
        }

        public IReadOnlyList<WorkCatalogItem> LooseRegistered { get; }
        public IReadOnlyList<YearRow> YearRows { get; }
        public IReadOnlyList<WorkCatalogItem> LooseUnregistered { get; }
    }

    /// <summary>
    /// 同一年份夹下的作品，已入编在前。
    /// </summary>
    public sealed class YearRow
    {
        public YearRow(string year, IReadOnlyList<WorkCatalogItem> works)
        {
            Year = year;
            Works = works;
        }

        public string Year { get; }
        public IReadOnlyList<WorkCatalogItem> Works { get; }
    }

    /// <summary>
    /// 能读出年份夹的作品归入该年，其余留在栏根。
    /// </summary>
    private static void Place(
        ChannelProfile channel,
        IReadOnlyList<WorkCatalogItem> workList,
        IReadOnlyDictionary<string, string> yearByFolderId,
        IDictionary<string, List<WorkCatalogItem>> yearWorkDict,
        List<WorkCatalogItem> looseList)
    {
        foreach (var work in workList)
        {
            var year = ResolveYear(channel, work, yearByFolderId);
            if (year == null)
            {
                looseList.Add(work);
                continue;
            }

            if (!yearWorkDict.TryGetValue(year, out var bucket))
            {
                bucket = new List<WorkCatalogItem>();
                yearWorkDict[year] = bucket;
            }

            bucket.Add(work);
        }
    }

    /// <summary>
    /// 先看作品自己的投放夹，再按投放箱扫描到的夹名补年份。
    /// </summary>
    private static string? ResolveYear(
        ChannelProfile channel,
        WorkCatalogItem work,
        IReadOnlyDictionary<string, string> yearByFolderId)
    {
        if (StageWorkFolder.TryReadYearFolder(channel, work.StageFolder, out var fromFolder))
        {
            return fromFolder;
        }

        if (yearByFolderId.TryGetValue(work.Id, out var fromId))
        {
            return fromId;
        }

        var folderId = WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder);
        if (!string.IsNullOrWhiteSpace(folderId)
            && yearByFolderId.TryGetValue(folderId, out var fromGuess))
        {
            return fromGuess;
        }

        return null;
    }
}
