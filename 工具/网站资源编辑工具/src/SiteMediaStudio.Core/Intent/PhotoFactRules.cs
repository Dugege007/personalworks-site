namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 摄影新作品的年份、地点、类型，以及自由标签的收口。
/// </summary>
public static class PhotoFactRules
{
    /// <summary>
    /// 冻结类型键与中文。汇总顺序为风光、人文、人像、游戏、AI。
    /// </summary>
    public readonly record struct PhotoTheme(string Key, string Zh);

    /// <summary>
    /// 下拉里的一项。类型与默认自由标签按展示顺序排，不是新的类型键。
    /// </summary>
    public readonly record struct PhotoPrimaryFacet(string Id, bool IsType);

    private static readonly HashSet<string> FactChannelSet = new(StringComparer.Ordinal)
    {
        "landscape-photo",
        "humanist-photo",
        "portrait-photo",
        "real-world-photo"
    };

    /// <summary>
    /// 单张照片的类型候选，顺序固定。项目本身不写类型。
    /// </summary>
    public static readonly IReadOnlyList<PhotoTheme> ThemeList = new PhotoTheme[]
    {
        new("landscape-photo", "风光"),
        new("humanist-photo", "人文"),
        new("portrait-photo", "人像"),
        new("game-photo", "游戏"),
        new("ai-photo", "AI")
    };

    /// <summary>
    /// 类型与默认自由标签的下拉顺序：风光、展馆、人像、人文、随拍、游戏、AI。
    /// </summary>
    public static readonly IReadOnlyList<PhotoPrimaryFacet> PrimaryFacetList = new PhotoPrimaryFacet[]
    {
        new("landscape-photo", true),
        new("展馆", false),
        new("portrait-photo", true),
        new("humanist-photo", true),
        new("随拍", false),
        new("game-photo", true),
        new("ai-photo", true)
    };

    /// <summary>
    /// 摄影下拉里始终出现的自由标签。不是新的类型门。
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultTagList = new[]
    {
        "展馆",
        "随拍"
    };

    /// <summary>
    /// 汇总栏里「无标签」的标识。不是自由标签文字。
    /// </summary>
    public const string UntaggedId = "untagged";

    /// <summary>
    /// 该栏目的新作品是否必须带年份与地点。类型写在单张照片上。
    /// </summary>
    public static bool RequiresFacts(string? channel)
    {
        return !string.IsNullOrWhiteSpace(channel) && FactChannelSet.Contains(channel);
    }

    /// <summary>
    /// 从开始日或已写年份读出四位公历年。不把当年填进去。
    /// </summary>
    public static bool TryReadExplicitYear(string? startedOn, string? year, out string explicitYear)
    {
        explicitYear = "";
        if (WorkRegisterRules.TryNormalizeStartedOn(startedOn, out var started)
            && started.Length >= 4
            && IsFourDigitYear(started[..4]))
        {
            explicitYear = started[..4];
            return true;
        }

        var raw = (year ?? "").Trim();
        if (IsFourDigitYear(raw))
        {
            explicitYear = raw;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 地点是否已写成非空城市名。
    /// </summary>
    public static bool HasPlace(string? place)
    {
        return WorkRegisterRules.TryNormalizePlace(place, out var city) && city.Length > 0;
    }

    /// <summary>
    /// 已上页照片的类型并集，按风光、人文、人像、游戏、AI 排列。没有的类型不出现。
    /// </summary>
    public static List<string> ThemesOfPublishedMedia(WorkCatalogItem work)
    {
        var picked = work.Media
            .Where(item => !string.IsNullOrWhiteSpace(item.Src))
            .SelectMany(item => item.Themes);
        return NormalizeThemes(picked);
    }

    /// <summary>
    /// 只保留冻结类型键，并按风光、人文、人像、游戏、AI 排列。
    /// </summary>
    public static List<string> NormalizeThemes(IEnumerable<string>? themeList)
    {
        var picked = new HashSet<string>(
            (themeList ?? Array.Empty<string>()).Select(item => (item ?? "").Trim()),
            StringComparer.Ordinal);
        return ThemeList.Where(theme => picked.Contains(theme.Key)).Select(theme => theme.Key).ToList();
    }

    /// <summary>
    /// 登记必填年份与地点。类型写在单张照片上，不在项目上。
    /// </summary>
    public static string? DescribeMissingRegister(string? startedOn, string? year, string? place, IEnumerable<string>? themeList)
    {
        _ = themeList;
        return DescribeMissing(
            TryReadExplicitYear(startedOn, year, out _),
            HasPlace(place),
            true);
    }

    /// <summary>
    /// 已入编作品上页只核年份与地点。类型在照片上，不挡项目上页。
    /// </summary>
    public static string? DescribeMissingIngest(WorkCatalogItem work)
    {
        return DescribeMissing(
            TryReadExplicitYear(work.StartedOn, work.Year, out _),
            HasPlace(work.Place),
            true);
    }

    /// <summary>
    /// 自由标签去重。四位年份与类型键拒绝，不静默丢掉。
    /// </summary>
    public static bool TryNormalizeTags(IEnumerable<string>? tagList, out List<string> normalized, out string? error)
    {
        normalized = new List<string>();
        error = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in tagList ?? Array.Empty<string>())
        {
            var tag = (raw ?? "").Trim();
            if (tag.Length == 0 || !seen.Add(tag))
            {
                continue;
            }

            if (IsFourDigitYear(tag))
            {
                error = "年份不是标签：" + tag;
                return false;
            }

            if (ThemeList.Any(theme => theme.Key == tag))
            {
                error = "类型不进自由标签：" + tag;
                return false;
            }

            if (tag == "无标签" || tag == UntaggedId)
            {
                error = "无标签是筛选，不能当作标签：" + tag;
                return false;
            }

            normalized.Add(tag);
        }

        return true;
    }

    /// <summary>
    /// 按年份、地点、类型的顺序拼出缺项。
    /// </summary>
    public static string? DescribeMissing(bool hasYear, bool hasPlace, bool hasType)
    {
        var nameList = new List<string>();
        if (!hasYear)
        {
            nameList.Add("年份");
        }

        if (!hasPlace)
        {
            nameList.Add("地点");
        }

        if (!hasType)
        {
            nameList.Add("类型");
        }

        return nameList.Count == 0 ? null : "缺" + string.Join("、", nameList) + "。";
    }

    /// <summary>
    /// 是否为摄影默认自由标签。
    /// </summary>
    public static bool IsDefaultTag(string? label)
    {
        var text = (label ?? "").Trim();
        return DefaultTagList.Contains(text);
    }

    /// <summary>
    /// 已上页的一张是否既没有类型也没有自由标签。
    /// </summary>
    public static bool IsUntagged(IEnumerable<string>? themeList, IEnumerable<string>? tagList)
    {
        if (NormalizeThemes(themeList).Count > 0)
        {
            return false;
        }

        if (!TryNormalizeTags(tagList, out var tags, out _))
        {
            return false;
        }

        return tags.Count == 0;
    }

    /// <summary>
    /// 是否为 1900–2100 的四位年份。
    /// </summary>
    private static bool IsFourDigitYear(string value)
    {
        if (value.Length != 4 || value.Any(ch => !char.IsDigit(ch)))
        {
            return false;
        }

        var number = int.Parse(value);
        return number is >= 1900 and <= 2100;
    }
}
