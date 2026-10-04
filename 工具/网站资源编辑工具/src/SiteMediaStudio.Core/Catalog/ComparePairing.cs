namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 按台账对象键与手动钉生成对照行；不以文件夹名当作项目。
/// </summary>
public static class ComparePairing
{
    /// <summary>
    /// 在当前栏目 / 作品范围内配对投放箱与站点条目。
    /// </summary>
    public static IReadOnlyList<CompareRow> Build(
        WorkspaceSession session,
        string channelKey,
        string? workId,
        IReadOnlyList<ManualPin> pinList)
    {
        var siteList = session.SiteItems
            .Where(item => item.ChannelKey == channelKey)
            .Where(item => workId == null || item.WorkId == workId)
            .ToList();
        var stageList = session.StageItems
            .Where(item => item.ChannelKey == channelKey)
            .Where(item => StageBelongsToScope(session, item, workId, pinList))
            .ToList();

        var usedStageSet = new HashSet<StageItem>();
        var usedSiteSet = new HashSet<SiteItem>();
        var rowList = new List<CompareRow>();

        foreach (var pin in pinList)
        {
            var stage = stageList.FirstOrDefault(item =>
                string.Equals(item.StageRel, pin.StageRel, StringComparison.Ordinal));
            var site = siteList.FirstOrDefault(item =>
                string.Equals(item.ObjectKey, pin.Object, StringComparison.Ordinal)
                && string.Equals(item.WorkId, pin.WorkId, StringComparison.Ordinal));
            if (stage == null || site == null || usedStageSet.Contains(stage) || usedSiteSet.Contains(site))
            {
                continue;
            }

            usedStageSet.Add(stage);
            usedSiteSet.Add(site);
            rowList.Add(new CompareRow
            {
                Kind = CompareRowKind.ManualPin,
                Stage = stage,
                Site = site
            });
        }

        foreach (var site in siteList)
        {
            if (usedSiteSet.Contains(site))
            {
                continue;
            }

            var stage = stageList.FirstOrDefault(item =>
                !usedStageSet.Contains(item) && IsAutoPair(item, site));
            if (stage != null)
            {
                usedStageSet.Add(stage);
                usedSiteSet.Add(site);
                rowList.Add(new CompareRow
                {
                    Kind = CompareRowKind.Paired,
                    Stage = stage,
                    Site = site
                });
                continue;
            }

            usedSiteSet.Add(site);
            rowList.Add(new CompareRow
            {
                Kind = CompareRowKind.SiteOnly,
                Site = site
            });
        }

        foreach (var stage in stageList)
        {
            if (usedStageSet.Contains(stage))
            {
                continue;
            }

            rowList.Add(new CompareRow
            {
                Kind = CompareRowKind.StageOnly,
                Stage = stage
            });
        }

        return rowList;
    }

    /// <summary>
    /// 台账对象键或内容层 src 一致才自动配对。
    /// </summary>
    public static bool IsAutoPair(StageItem stage, SiteItem site)
    {
        if (!string.IsNullOrWhiteSpace(stage.MatchedObject)
            && string.Equals(stage.MatchedObject, site.ObjectKey, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(stage.StageRel)
            && !string.IsNullOrWhiteSpace(site.StageRel)
            && string.Equals(stage.StageRel, site.StageRel, StringComparison.Ordinal);
    }

    /// <summary>
    /// 作品范围内：文件夹名不等于项目 id 时，仍按对象键、已认领夹或手动钉收入投放箱。
    /// </summary>
    private static bool StageBelongsToScope(
        WorkspaceSession session,
        StageItem item,
        string? workId,
        IReadOnlyList<ManualPin> pinList)
    {
        if (StageWorkScope.BelongsToWork(session, item, workId))
        {
            return true;
        }

        return workId != null
            && pinList.Any(pin =>
                pin.WorkId == workId
                && string.Equals(pin.StageRel, item.StageRel, StringComparison.Ordinal));
    }
}
