using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 从栏目内相对路径取出作品夹；已登记公司 / 类别夹与年份夹本身不作为作品。
/// </summary>
public static class StageWorkFolder
{
    private static readonly Regex YearFolderRegex = new(
        @"^\d{4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 该第一层文件夹是否为容器：点名公司 / 类别夹，或已开启的整段四位年份夹。
    /// </summary>
    public static bool IsContainer(ChannelProfile? channel, string? folderName)
    {
        if (channel == null || string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        if (IsYearFolder(channel, folderName))
        {
            return true;
        }

        return channel.StageContainerFolders.Any(name =>
            string.Equals(name, folderName, StringComparison.Ordinal));
    }

    /// <summary>
    /// 路径第一段（去掉栏目键前缀后）是否为年份夹。
    /// </summary>
    public static bool TryReadYearFolder(ChannelProfile? channel, string? path, out string year)
    {
        year = "";
        if (channel is not { YearContainers: true } || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var rel = JsonUtil.ToRel(path).Trim('/');
        var keyPrefix = channel.Key + "/";
        if (rel.StartsWith(keyPrefix, StringComparison.Ordinal))
        {
            rel = rel[keyPrefix.Length..];
        }

        if (string.IsNullOrWhiteSpace(rel))
        {
            return false;
        }

        var first = rel.Split('/')[0];
        if (!IsYearFolder(channel, first))
        {
            return false;
        }

        year = first;
        return true;
    }

    /// <summary>
    /// 栏目开启年份容器，且夹名整段为四位数字。带日期的拍摄夹不是年份容器。
    /// </summary>
    public static bool IsYearFolder(ChannelProfile? channel, string? folderName)
    {
        return channel is { YearContainers: true }
            && !string.IsNullOrWhiteSpace(folderName)
            && YearFolderRegex.IsMatch(folderName);
    }

    /// <summary>
    /// 读取作品夹猜测。容器夹根下的文件不算作品；点号前缀与 stock 排除。
    /// </summary>
    public static bool TryRead(
        string afterChannel,
        ChannelProfile? channel,
        out string workIdGuess,
        out string folderUnderChannel)
    {
        workIdGuess = "";
        folderUnderChannel = "";
        var rel = JsonUtil.ToRel(afterChannel);
        if (string.IsNullOrWhiteSpace(rel) || !rel.Contains('/'))
        {
            return false;
        }

        var partList = rel.Split('/');
        var first = partList[0];
        if (IsContainer(channel, first))
        {
            if (partList.Length < 3)
            {
                return false;
            }

            workIdGuess = partList[1];
            folderUnderChannel = first + "/" + partList[1];
        }
        else
        {
            workIdGuess = first;
            folderUnderChannel = first;
        }

        return !string.IsNullOrWhiteSpace(workIdGuess)
            && !workIdGuess.StartsWith('.')
            && !MediaPathRules.IsStock(workIdGuess);
    }

    /// <summary>
    /// 栏目是否使用扁平对象键（文件直接落在栏目根）。
    /// </summary>
    public static bool IsFlatFile(ChannelProfile? channel)
    {
        return string.Equals(channel?.ObjectKeyPattern, "flat-file", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按栏目键查找配置。
    /// </summary>
    public static ChannelProfile? FindChannel(WorkspaceProfile? profile, string? channelKey)
    {
        if (profile == null || string.IsNullOrWhiteSpace(channelKey))
        {
            return null;
        }

        return profile.Channels.FirstOrDefault(item =>
            string.Equals(item.Key, channelKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// 作品夹下的批次夹名；没有下一级或下一级是成片目录时为空。
    /// </summary>
    public static bool TryReadBatchFolder(string? stageRel, ChannelProfile? channel, out string batchFolder)
    {
        batchFolder = "";
        if (!TryReadDirectoryAfterChannel(stageRel, channel, out var afterChannel)
            || !TryReadFolderPath(afterChannel, channel, out _, out var folderUnderChannel))
        {
            return false;
        }

        var rel = JsonUtil.ToRel(afterChannel);
        var prefix = folderUnderChannel + "/";
        if (!rel.StartsWith(prefix, StringComparison.Ordinal) || rel.Length <= prefix.Length)
        {
            return false;
        }

        var rest = rel[prefix.Length..];
        var slash = rest.IndexOf('/');
        batchFolder = slash < 0 ? rest : rest[..slash];
        return !string.IsNullOrWhiteSpace(batchFolder)
            && !batchFolder.StartsWith('.')
            && !MediaPathRules.IsStock(batchFolder);
    }

    /// <summary>
    /// 从台账或站点 <c>stageRel</c> 取出作品夹，规则与投放箱扫描相同：容器下取第二层，忽略批次夹与成片目录。
    /// </summary>
    public static bool TryReadFromStageRel(
        string? stageRel,
        ChannelProfile? channel,
        out string workIdGuess,
        out string folderUnderChannel,
        out bool channelRoot)
    {
        workIdGuess = "";
        folderUnderChannel = "";
        channelRoot = false;
        if (!TryReadDirectoryAfterChannel(stageRel, channel, out var afterChannel))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(afterChannel))
        {
            channelRoot = true;
            return true;
        }

        return TryReadFolderPath(afterChannel, channel, out workIdGuess, out folderUnderChannel);
    }

    /// <summary>
    /// 去掉栏目段、成片目录与文件名后的作品夹路径。
    /// </summary>
    public static bool TryReadFolderPath(
        string afterChannelDir,
        ChannelProfile? channel,
        out string workIdGuess,
        out string folderUnderChannel)
    {
        workIdGuess = "";
        folderUnderChannel = "";
        var rel = JsonUtil.ToRel(afterChannelDir);
        if (string.IsNullOrWhiteSpace(rel))
        {
            return false;
        }

        var partList = rel.Split('/');
        var first = partList[0];
        if (IsContainer(channel, first))
        {
            if (partList.Length < 2)
            {
                return false;
            }

            workIdGuess = partList[1];
            folderUnderChannel = first + "/" + partList[1];
        }
        else
        {
            workIdGuess = first;
            folderUnderChannel = first;
        }

        return !string.IsNullOrWhiteSpace(workIdGuess)
            && !workIdGuess.StartsWith('.')
            && !MediaPathRules.IsStock(workIdGuess);
    }

    /// <summary>
    /// 去掉成片目录或文件名后，再去掉父目录与栏目第一段。
    /// </summary>
    private static bool TryReadDirectoryAfterChannel(
        string? stageRel,
        ChannelProfile? channel,
        out string afterChannel)
    {
        afterChannel = "";
        if (string.IsNullOrWhiteSpace(stageRel))
        {
            return false;
        }

        var rel = JsonUtil.ToRel(stageRel);
        var ready = rel.IndexOf("/.site-ready/", StringComparison.OrdinalIgnoreCase);
        var parent = ready > 0
            ? rel[..ready]
            : HasFileName(rel)
                ? rel[..rel.LastIndexOf('/')]
                : rel;
        parent = StripStageParent(parent, channel);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        var slash = parent.IndexOf('/');
        afterChannel = slash < 0 ? "" : parent[(slash + 1)..];
        return true;
    }

    /// <summary>
    /// 路径以栏目父目录开头时去掉这一段。没有父目录或对不上时保持原样。
    /// </summary>
    private static string StripStageParent(string parent, ChannelProfile? channel)
    {
        if (channel == null || string.IsNullOrWhiteSpace(channel.StageParent))
        {
            return parent;
        }

        var prefix = JsonUtil.ToRel(channel.StageParent.Trim().Trim('/')) + "/";
        if (!parent.StartsWith(prefix, StringComparison.Ordinal))
        {
            return parent;
        }

        return parent[prefix.Length..];
    }

    /// <summary>
    /// 最后一段带媒体扩展名时视为文件，而不是作品夹。
    /// </summary>
    private static bool HasFileName(string rel)
    {
        var slash = rel.LastIndexOf('/');
        if (slash <= 0)
        {
            return MediaPathRules.IsMediaExtension(Path.GetExtension(rel));
        }

        return MediaPathRules.IsMediaExtension(Path.GetExtension(rel[(slash + 1)..]));
    }
}
