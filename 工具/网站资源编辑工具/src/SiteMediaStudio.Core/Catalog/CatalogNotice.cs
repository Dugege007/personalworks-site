namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 须脱敏而未脱敏、以及无 src 占位槽的提示文案。不自动脱敏、不自动补图。
/// </summary>
public static class CatalogNotice
{
    public const string ConstructionChannelKey = "landscape-cds";

    /// <summary>
    /// 须脱敏后才能上页的栏目。新增领域时只往此集合加键。
    /// </summary>
    private static readonly HashSet<string> DesenseChannelKeySet = new(StringComparer.Ordinal)
    {
        ConstructionChannelKey
    };

    /// <summary>
    /// 是否为景观施工图栏目。
    /// </summary>
    public static bool IsConstructionChannel(string? channelKey)
    {
        return string.Equals(channelKey, ConstructionChannelKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// 该栏目的网图须先脱敏。
    /// </summary>
    public static bool RequiresDesense(string? channelKey)
    {
        return !string.IsNullOrWhiteSpace(channelKey)
            && DesenseChannelKeySet.Contains(channelKey);
    }

    /// <summary>
    /// 已可上页的脱敏成片：`.desense.jpg` 或 `.webp`。其它扩展视为未脱敏。
    /// </summary>
    public static bool IsPublicReadyConstructionFile(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return false;
        }

        var name = Path.GetFileName(pathOrName);
        if (string.Equals(Path.GetExtension(name), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.EndsWith(".desense.jpg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 栏目须脱敏且文件尚未成片。
    /// </summary>
    public static bool IsUnsigned(string? channelKey, string? pathOrName)
    {
        return RequiresDesense(channelKey) && !IsPublicReadyConstructionFile(pathOrName);
    }

    /// <summary>
    /// 投放箱条目是否为须脱敏而未脱敏。
    /// </summary>
    public static bool IsUnsigned(StageItem item)
    {
        return IsUnsigned(item.ChannelKey, item.FullPath ?? item.StageRel);
    }

    /// <summary>
    /// 站点条目是否为须脱敏而未脱敏。
    /// </summary>
    public static bool IsUnsigned(SiteItem item)
    {
        return IsUnsigned(item.ChannelKey, item.FullPath ?? item.ObjectKey);
    }

    /// <summary>
    /// 投放箱条目是否为未脱敏施工图。保留旧名给闸门。
    /// </summary>
    public static bool IsUnsignedConstruction(StageItem item)
    {
        return IsUnsigned(item);
    }

    /// <summary>
    /// 投放箱已发布、站点已隐藏、须脱敏而未脱敏：共用同一置灰。
    /// </summary>
    public static bool NeedsChromeDim(bool isSite, bool isHidden, bool isPublished, bool isUnsigned)
    {
        return isUnsigned || (isSite ? isHidden : isPublished);
    }

    /// <summary>
    /// 投放箱条目是否落入当前脱敏筛。
    /// </summary>
    public static bool MatchesSense(StageItem item, SenseFilterKind filter)
    {
        return MatchesSense(item.ChannelKey, item.FullPath ?? item.StageRel, filter);
    }

    /// <summary>
    /// 站点条目是否落入当前脱敏筛。
    /// </summary>
    public static bool MatchesSense(SiteItem item, SenseFilterKind filter)
    {
        return MatchesSense(item.ChannelKey, item.FullPath ?? item.ObjectKey, filter);
    }

    /// <summary>
    /// 对照行：任一侧命中即保留，便于看未脱敏与已脱敏的配对差。
    /// </summary>
    public static bool MatchesSense(CompareRow row, SenseFilterKind filter)
    {
        if (filter == SenseFilterKind.All)
        {
            return true;
        }

        var stageOk = row.Stage != null && MatchesSense(row.Stage, filter);
        var siteOk = row.Site != null && MatchesSense(row.Site, filter);
        return stageOk || siteOk;
    }

    /// <summary>
    /// 按栏目与文件名判定脱敏筛。非须脱敏栏目只有「全部」能命中。
    /// </summary>
    public static bool MatchesSense(string? channelKey, string? pathOrName, SenseFilterKind filter)
    {
        if (filter == SenseFilterKind.All)
        {
            return true;
        }

        if (!RequiresDesense(channelKey))
        {
            return false;
        }

        var ready = IsPublicReadyConstructionFile(pathOrName);
        return filter == SenseFilterKind.Signed ? ready : !ready;
    }

    /// <summary>
    /// 当前栏目 / 项目范围内的警告条正文；无则空串。
    /// </summary>
    public static string BuildBar(WorkspaceSession session, string? channelKey, string? workId)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return "";
        }

        var unsignedCount = 0;
        if (RequiresDesense(channelKey))
        {
            unsignedCount = session.StageItems.Count(item =>
                item.ChannelKey == channelKey
                && StageWorkScope.BelongsToWork(session, item, workId)
                && !IsPublicReadyConstructionFile(item.FullPath ?? item.StageRel));
        }

        return ComposeBar(session, channelKey, workId, unsignedCount);
    }

    /// <summary>
    /// 用已数好的未脱敏张数拼警告条，不再扫投放箱。
    /// </summary>
    public static string ComposeBar(
        WorkspaceSession session,
        string? channelKey,
        string? workId,
        int unsignedCount)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return "";
        }

        var lineList = new List<string>();
        if (RequiresDesense(channelKey) && unsignedCount > 0)
        {
            var kind = IsConstructionChannel(channelKey) ? "施工图未脱敏" : "未脱敏";
            lineList.Add($"{kind} {unsignedCount} 张（须 .desense.jpg 或 webp），禁止上页。不自动脱敏。");
        }

        var emptySlotCount = CountEmptySrcSlots(session, channelKey, workId);
        if (emptySlotCount > 0)
        {
            lineList.Add($"无 src 占位槽 {emptySlotCount} 个，不能隐藏。不自动补图。");
        }

        return string.Join(" ", lineList);
    }

    /// <summary>
    /// 统计图像条目中尚未写入 src 的占位槽。
    /// </summary>
    public static int CountEmptySrcSlots(WorkspaceSession session, string? channelKey, string? workId)
    {
        return session.Works
            .Where(work =>
                (string.IsNullOrWhiteSpace(channelKey) || work.Channel == channelKey)
                && (string.IsNullOrWhiteSpace(workId) || work.Id == workId))
            .Sum(work => work.Media.Count(media =>
                string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(media.Src)));
    }
}
