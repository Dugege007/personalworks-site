using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 已入编作品与投放箱夹名的认领：去掉日期前缀后对题名；同题多名再按夹名日期对齐作品 id。
/// </summary>
public static class StageFolderClaim
{
    private static readonly Regex LeadingDateRegex = new(
        @"^(?<date>\d{8}|\d{6})(?:[_\s\u3000]+|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 去掉夹名开头的 <c>YYYYMMDD</c> / <c>YYYYMM</c> 及随后空白或下划线。
    /// </summary>
    public static string StripLeadingDate(string? folderId)
    {
        var name = (folderId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var match = LeadingDateRegex.Match(name);
        return match.Success ? name[match.Length..].Trim() : name;
    }

    /// <summary>
    /// 夹名开头的 <c>YYYYMMDD</c> / <c>YYYYMM</c>；没有则空串。
    /// </summary>
    public static string ReadLeadingDate(string? folderId)
    {
        var match = LeadingDateRegex.Match((folderId ?? "").Trim());
        return match.Success ? match.Groups["date"].Value : "";
    }

    /// <summary>
    /// 夹名本身或去掉日期后的剩余段是否等于作品题名。
    /// </summary>
    public static bool TitleMatchesFolder(string? title, string? folderId)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(folderId))
        {
            return false;
        }

        if (string.Equals(title, folderId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rest = StripLeadingDate(folderId);
        return !string.IsNullOrWhiteSpace(rest)
            && string.Equals(title, rest, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 该夹是否已被已入编作品认领：夹名等于作品 id，或去掉日期后等于题名。
    /// </summary>
    public static bool TryClaim(
        IReadOnlyList<WorkCatalogItem> registeredList,
        string channel,
        string folderId,
        WorkspaceProfile? profile,
        out WorkCatalogItem? work)
    {
        work = null;
        _ = profile;
        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(folderId))
        {
            return false;
        }

        var matchList = registeredList
            .Where(item => item.Channel == channel && !item.IsUnregistered)
            .Where(item =>
                string.Equals(item.Id, folderId, StringComparison.Ordinal)
                || TitleMatchesFolder(item.Title, folderId))
            .ToList();
        if (matchList.Count == 0)
        {
            return false;
        }

        if (matchList.Count == 1)
        {
            work = matchList[0];
            return true;
        }

        work = PreferDatedWork(matchList, folderId);
        return work != null;
    }

    /// <summary>
    /// 同题多名时按夹名日期对齐作品 id 或 stageFolder；对不上则不认领。
    /// </summary>
    private static WorkCatalogItem? PreferDatedWork(IReadOnlyList<WorkCatalogItem> matchList, string folderId)
    {
        var date = ReadLeadingDate(folderId);
        if (string.IsNullOrEmpty(date))
        {
            return null;
        }

        var datedList = matchList.Where(item => WorkMatchesFolderDate(item, folderId, date)).ToList();
        return datedList.Count == 1 ? datedList[0] : null;
    }

    /// <summary>
    /// 作品 id 或登记夹是否带有该夹的日期前缀。
    /// </summary>
    private static bool WorkMatchesFolderDate(WorkCatalogItem item, string folderId, string date)
    {
        if (item.Id.Equals(date, StringComparison.Ordinal)
            || item.Id.EndsWith("-" + date, StringComparison.Ordinal)
            || item.Id.EndsWith("_" + date, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(item.StageFolder))
        {
            return false;
        }

        var stageFolderId = WorkRegisterRules.FolderIdFromStageFolder(item.StageFolder);
        return string.Equals(stageFolderId, folderId, StringComparison.Ordinal)
            || stageFolderId.StartsWith(date, StringComparison.Ordinal);
    }

    /// <summary>
    /// 从投放箱条目取出用于认领的作品夹名。
    /// </summary>
    public static string FolderIdOf(StageItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.WorkIdGuess))
        {
            return item.WorkIdGuess;
        }

        var guess = item.StageFolderGuess ?? "";
        var slash = guess.LastIndexOf('/');
        return slash >= 0 ? guess[(slash + 1)..] : guess;
    }
}
