using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 读写工作区配置。
/// </summary>
public static class WorkspaceProfileLoader
{
    /// <summary>
    /// 从磁盘读取并解析工作区根的绝对路径。
    /// </summary>
    public static WorkspaceProfile Load(string profilePath)
    {
        var fullPath = Path.GetFullPath(profilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到工作区配置。", fullPath);
        }

        var json = File.ReadAllText(fullPath);
        var profile = JsonSerializer.Deserialize<WorkspaceProfile>(json, JsonUtil.Options)
            ?? throw new InvalidDataException("工作区配置无法解析。");
        if (profile.Version < 1)
        {
            throw new InvalidDataException("工作区配置 version 无效。");
        }

        var profileDir = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException("无法确定配置文件目录。");
        var root = string.IsNullOrWhiteSpace(profile.Root)
            ? profileDir
            : Path.GetFullPath(Path.Combine(profileDir, profile.Root));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("工作区根目录不存在：" + root);
        }

        profile.ProfilePath = fullPath;
        profile.ResolvedRoot = root;
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = Path.GetFileName(root);
        }

        return profile;
    }

    /// <summary>
    /// 把相对工作区根的路径收成绝对路径。
    /// </summary>
    public static string ResolveUnderRoot(WorkspaceProfile profile, string relative)
    {
        var normalized = relative.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(profile.ResolvedRoot, normalized));
    }

    /// <summary>
    /// 按栏目规则拼投放箱下的栏目文件夹名。
    /// </summary>
    public static string ChannelStageFolderName(WorkspaceProfile profile, ChannelProfile channel)
    {
        if (string.Equals(profile.StageFolderPattern, "ZhKey", StringComparison.OrdinalIgnoreCase))
        {
            return $"{channel.Zh}（{channel.Key}）";
        }

        return channel.Key;
    }

    /// <summary>
    /// 投放箱根下的栏目目录。有父目录时为 <c>父目录/栏目夹</c>。
    /// </summary>
    public static string ChannelStageRelative(WorkspaceProfile profile, ChannelProfile channel)
    {
        var folder = ChannelStageFolderName(profile, channel);
        if (string.IsNullOrWhiteSpace(channel.StageParent))
        {
            return folder;
        }

        var parent = JsonUtil.ToRel(channel.StageParent.Trim().Trim('/'));
        return parent + "/" + folder;
    }
}
