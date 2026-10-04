namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 台账与常驻位在界面上的中文名称。内部键仍是 published / withdrawn。
/// </summary>
public static class PublishStatus
{
    public const string Published = "已发布";
    public const string Hidden = "已隐藏";
    public const string Unsigned = "未脱敏";
    public const string Withdrawn = "已撤下";
    public const string Stock = "常驻";
    public const string Unpublished = "未上页";
    public const string Paired = "已配对";
    public const string Unlisted = "未登记";
    public const string None = "无";

    /// <summary>
    /// 台账英文字段译成中文；空则返回空串。
    /// </summary>
    public static string ToChinese(string? ledgerStatus)
    {
        if (string.IsNullOrWhiteSpace(ledgerStatus))
        {
            return "";
        }

        if (string.Equals(ledgerStatus, "published", StringComparison.OrdinalIgnoreCase))
        {
            return Published;
        }

        if (string.Equals(ledgerStatus, "withdrawn", StringComparison.OrdinalIgnoreCase))
        {
            return Withdrawn;
        }

        if (string.Equals(ledgerStatus, "stock", StringComparison.OrdinalIgnoreCase))
        {
            return Stock;
        }

        return ledgerStatus;
    }

    /// <summary>
    /// 检视栏「台账」一行：有状态用中文，否则「无」。
    /// </summary>
    public static string LedgerLine(string? ledgerStatus)
    {
        var zh = ToChinese(ledgerStatus);
        return string.IsNullOrEmpty(zh) ? None : zh;
    }

    /// <summary>
    /// 投放箱卡片与灯箱外圈用的发布状态。
    /// </summary>
    public static string ForStage(StageItem stage)
    {
        if (stage.IsStock)
        {
            return Stock;
        }

        if (stage.IsPendingWithdraw)
        {
            return Withdrawn;
        }

        var zh = ToChinese(stage.LedgerStatus);
        if (!string.IsNullOrEmpty(zh))
        {
            return zh;
        }

        return string.IsNullOrWhiteSpace(stage.MatchedObject) ? Unpublished : Paired;
    }

    /// <summary>
    /// 站点卡片与灯箱外圈用的发布状态。
    /// </summary>
    public static string ForSite(SiteItem site)
    {
        if (site.IsHidden)
        {
            return Hidden;
        }

        if (site.IsStock)
        {
            return Stock;
        }

        var zh = ToChinese(site.LedgerStatus);
        return string.IsNullOrEmpty(zh) ? Unlisted : zh;
    }
}
