using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 登记空壳作品时的技术键与投放箱文件夹约定。
/// </summary>
public static class WorkRegisterRules
{
    private static readonly Regex TechnicalKeyRegex = new(
        @"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ContentPoolChannelSet = new(StringComparer.Ordinal)
    {
        "digital-twin",
        "line-sim",
        "landscape-rendering",
        "landscape-cds",
        "landscape-photo",
        "humanist-photo",
        "portrait-photo",
        "real-world-photo"
    };

    /// <summary>
    /// 作品 id 是否为小写短横线技术键。
    /// </summary>
    public static bool IsTechnicalKey(string? workId)
    {
        return !string.IsNullOrWhiteSpace(workId) && TechnicalKeyRegex.IsMatch(workId);
    }

    /// <summary>
    /// 现实摄影、游戏摄影、AI摄影的作品 id 用中转站项目夹名。
    /// </summary>
    public static bool IsPhotoFolderChannel(string? channelKey)
    {
        return channelKey is "real-world-photo" or "game-photo" or "ai-photo";
    }

    /// <summary>
    /// 项目夹名：非空、无路径分隔、无引号，且不以点开头。
    /// </summary>
    public static bool IsProjectFolderName(string? workId)
    {
        if (string.IsNullOrWhiteSpace(workId) || workId != workId.Trim() || workId.Length > 80)
        {
            return false;
        }

        if (workId.StartsWith('.')
            || workId.Contains('/')
            || workId.Contains('\\')
            || workId.Contains('"')
            || workId.Contains('\n')
            || workId.Contains('\r'))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 登记与编辑接受的作品 id。摄影投放夹允许项目夹名，其余栏目仍要技术键。
    /// </summary>
    public static bool IsAcceptedWorkId(string? channelKey, string? workId)
    {
        if (IsTechnicalKey(workId))
        {
            return true;
        }

        return IsPhotoFolderChannel(channelKey) && IsProjectFolderName(workId);
    }

    /// <summary>
    /// 是否可写入 works.ts 内容池。
    /// </summary>
    public static bool IsContentPoolChannel(string? channelKey)
    {
        return !string.IsNullOrWhiteSpace(channelKey) && ContentPoolChannelSet.Contains(channelKey);
    }

    /// <summary>
    /// 是否允许确认登记：内容池、形象场次或游戏开发。
    /// </summary>
    public static bool IsRegisterableChannel(string? channelKey)
    {
        return IsContentPoolChannel(channelKey)
            || string.Equals(channelKey, "profile", StringComparison.Ordinal)
            || string.Equals(channelKey, "game-dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// 空壳作品的年份初值，取本机当年。
    /// </summary>
    public static string DefaultYear()
    {
        return DateTime.Now.Year.ToString("0000");
    }

    /// <summary>
    /// 形象场次与游戏空壳不写开始日、地点。
    /// </summary>
    public static bool SupportsScheduleFields(string? channelKey)
    {
        return !string.Equals(channelKey, "profile", StringComparison.Ordinal)
            && !string.Equals(channelKey, "game-dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// 从夹名日期前缀抽出开始日初值；没有或无法排成日历则空。
    /// </summary>
    public static string SeedStartedOn(string? folderId)
    {
        return TryNormalizeStartedOn(StageFolderClaim.ReadLeadingDate(folderId), out var startedOn)
            ? startedOn
            : "";
    }

    /// <summary>
    /// 把开始日收成 <c>YYYY-MM-DD</c> / <c>YYYY-MM</c> / <c>YYYY</c>；空串表示未填。
    /// </summary>
    public static bool TryNormalizeStartedOn(string? value, out string normalized)
    {
        normalized = "";
        var raw = (value ?? "").Trim().Replace('/', '-');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Regex.IsMatch(raw, @"^\d{8}$", RegexOptions.CultureInvariant))
        {
            raw = raw[..4] + "-" + raw[4..6] + "-" + raw[6..8];
        }
        else if (Regex.IsMatch(raw, @"^\d{6}$", RegexOptions.CultureInvariant))
        {
            raw = raw[..4] + "-" + raw[4..6];
        }

        if (Regex.IsMatch(raw, @"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant))
        {
            if (!DateTime.TryParseExact(
                    raw,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out _))
            {
                return false;
            }

            normalized = raw;
            return true;
        }

        if (Regex.IsMatch(raw, @"^\d{4}-\d{2}$", RegexOptions.CultureInvariant))
        {
            if (!DateTime.TryParseExact(
                    raw + "-01",
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out _))
            {
                return false;
            }

            normalized = raw;
            return true;
        }

        if (Regex.IsMatch(raw, @"^\d{4}$", RegexOptions.CultureInvariant))
        {
            normalized = raw;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 有开始日时取前四位作年份，否则用回退值或当年。
    /// </summary>
    public static string YearFromStartedOn(string? startedOn, string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(startedOn) && startedOn.Length >= 4)
        {
            return startedOn[..4];
        }

        return string.IsNullOrWhiteSpace(fallback) ? DefaultYear() : fallback.Trim();
    }

    /// <summary>
    /// 地点去掉首尾空白；禁止路径分隔与换行。
    /// </summary>
    public static bool TryNormalizePlace(string? value, out string normalized)
    {
        normalized = (value ?? "").Trim();
        if (normalized.Length == 0)
        {
            return true;
        }

        if (normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal)
            || normalized.Contains('\n')
            || normalized.Contains('\r')
            || normalized.Length > 32)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 转义写入 TypeScript 字符串字面量的文本。
    /// </summary>
    public static string EscapeTsString(string? value)
    {
        return (value ?? "").Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// 文件夹名已是技术键时可作为 id 初值，否则不填。
    /// </summary>
    public static string? SeedId(string? folderId)
    {
        return IsTechnicalKey(folderId) ? folderId : null;
    }

    /// <summary>
    /// 摄影投放夹用项目夹名作 id 初值；其它栏目仍只预填技术键。
    /// </summary>
    public static string? SeedIdForChannel(string? channelKey, string? folderId)
    {
        if (IsPhotoFolderChannel(channelKey) && IsProjectFolderName(folderId))
        {
            return folderId;
        }

        return SeedId(folderId);
    }

    /// <summary>
    /// 题名初值用去掉日期前缀后的文件夹名，不自动覆盖已填题名。
    /// </summary>
    public static string SeedTitle(string? folderId)
    {
        return StageFolderClaim.StripLeadingDate(folderId);
    }

    /// <summary>
    /// 栏目下作品夹的相对路径；folderId 可以含一层已登记容器。
    /// </summary>
    public static string StageFolder(string channel, string folderId)
    {
        return JsonUtil.ToRel(channel + "/" + folderId);
    }

    /// <summary>
    /// 投放箱作品夹是否允许登记：栏目下第一层拍摄夹，或年份 / 点名容器下的一层。容器夹本身不可登记。
    /// </summary>
    public static bool IsAllowedStageFolder(string channel, string folderRel, ChannelProfile? channelProfile)
    {
        var normalized = JsonUtil.ToRel(folderRel);
        var prefix = channel + "/";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = normalized[prefix.Length..];
        if (string.IsNullOrWhiteSpace(rest))
        {
            return false;
        }

        var partList = rest.Split('/');
        if (partList.Any(string.IsNullOrWhiteSpace)
            || partList.Any(part => part.StartsWith('.') || MediaPathRules.IsStock(part)))
        {
            return false;
        }

        if (partList.Length == 1)
        {
            return !StageWorkFolder.IsContainer(channelProfile, partList[0]);
        }

        return partList.Length == 2 && StageWorkFolder.IsContainer(channelProfile, partList[0]);
    }

    /// <summary>
    /// 从投放箱文件夹路径取出作品夹名。
    /// </summary>
    public static string FolderIdFromStageFolder(string? stageFolder)
    {
        if (string.IsNullOrWhiteSpace(stageFolder))
        {
            return "";
        }

        var normalized = JsonUtil.ToRel(stageFolder);
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized[(slash + 1)..];
    }
}
