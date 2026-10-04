namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把意图条目绑回当前工作区编目，供预演闸门使用。
/// </summary>
public static class IntentCatalogBinder
{
    /// <summary>
    /// 按台账路径与对象键对齐投放箱 / 站点条目。
    /// </summary>
    public static IReadOnlyList<(IntentItem Item, StageItem? Stage, SiteItem? Site)> Bind(
        WorkspaceSession session,
        IntentDocument document)
    {
        var resolvedList = new List<(IntentItem, StageItem?, SiteItem?)>();
        foreach (var item in document.Items)
        {
            var stageRel = string.IsNullOrWhiteSpace(item.StageRel) ? null : JsonUtil.ToRel(item.StageRel);
            var objectKey = string.IsNullOrWhiteSpace(item.Object) ? null : JsonUtil.ToRel(item.Object);
            var stage = stageRel == null
                ? null
                : session.StageItems.FirstOrDefault(candidate =>
                    string.Equals(JsonUtil.ToRel(candidate.StageRel), stageRel, StringComparison.Ordinal));
            var site = objectKey == null
                ? null
                : session.SiteItems.FirstOrDefault(candidate =>
                    string.Equals(candidate.ObjectKey, objectKey, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(item.WorkId)
                        || string.Equals(candidate.WorkId, item.WorkId, StringComparison.Ordinal)));
            resolvedList.Add((item, stage, site));
        }

        return resolvedList;
    }

    /// <summary>
    /// 本批上页共用的目标作品；多作品时取第一条上页的 workId。
    /// </summary>
    public static string? ReadIngestTargetWorkId(IntentDocument document)
    {
        foreach (var item in document.Items)
        {
            if (item.Intent == MediaIntentCodes.StageIngest && !string.IsNullOrWhiteSpace(item.WorkId))
            {
                return item.WorkId;
            }
        }

        return null;
    }

    /// <summary>
    /// 补全工作区字段，并把本机执行固定为机械、不发布。
    /// </summary>
    public static void PrepareForCli(IntentDocument document, WorkspaceSession session)
    {
        if (string.IsNullOrWhiteSpace(document.WorkspaceRoot))
        {
            document.WorkspaceRoot = session.Profile.ResolvedRoot;
        }

        if (string.IsNullOrWhiteSpace(document.ProfileName))
        {
            document.ProfileName = session.Profile.Name;
        }

        document.Mode = "direct";
        document.Options.Deploy = "none";
        if (string.IsNullOrWhiteSpace(document.CreatedAt))
        {
            document.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
    }
}
