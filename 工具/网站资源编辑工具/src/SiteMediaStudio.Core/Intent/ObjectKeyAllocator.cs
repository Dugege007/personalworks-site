namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 新上页对象键用投放文件名，放在作品目录下。同名已占用时追加 <c>-02</c>。
/// 同一作品且未 bump 时，沿用该中转路径上已撤下的键。其它作品改分新键。已有对象键不改名。
/// </summary>
public static class ObjectKeyAllocator
{

    /// <summary>
    /// 为一条投放箱上页分配对象键。
    /// </summary>
    public static string Allocate(
        WorkspaceSession session,
        string channel,
        string workId,
        string extension,
        string? stageRel,
        bool bump,
        ISet<string> reservedSet)
    {
        var ext = NormalizeExt(extension);
        if (!bump && !string.IsNullOrWhiteSpace(stageRel))
        {
            var reused = FindWithdrawnObject(session, stageRel);
            if (!string.IsNullOrWhiteSpace(reused)
                && string.Equals(Path.GetExtension(reused), ext, StringComparison.OrdinalIgnoreCase)
                && ReuseWithdrawnKey(session, channel, workId, reused))
            {
                reservedSet.Add(reused);
                return reused;
            }
        }

        var channelProfile = session.Profile.Channels.FirstOrDefault(item => item.Key == channel);
        if (string.Equals(channelProfile?.ObjectKeyPattern, "flat-file", StringComparison.OrdinalIgnoreCase))
        {
            return AllocateFlatFile(session, channel, stageRel, ext, reservedSet);
        }

        return AllocateNamedFile(session, WorkDirectoryPrefix(channel, workId), stageRel, ext, reservedSet);
    }

    /// <summary>
    /// 作品目录下用原文件名。扩展名用正式位规格。同名或视频封面已被占用时追加两位序号。
    /// </summary>
    private static string AllocateNamedFile(
        WorkspaceSession session,
        string prefix,
        string? stageRel,
        string extension,
        ISet<string> reservedSet)
    {
        var stem = ReadStageStem(stageRel);
        if (string.IsNullOrWhiteSpace(stem))
        {
            throw new InvalidOperationException($"无法从投放路径为 {prefix} 分配对象键。");
        }

        var reservePoster = string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase);
        for (var number = 1; number < 1000; number++)
        {
            var suffix = number == 1 ? "" : "-" + number.ToString("00");
            var objectKey = prefix + stem + suffix + extension;
            if (reservedSet.Contains(objectKey) || KeyExists(session, objectKey))
            {
                continue;
            }

            if (reservePoster)
            {
                var poster = VideoEncodeRules.PosterObjectKey(objectKey);
                if (reservedSet.Contains(poster) || KeyExists(session, poster))
                {
                    continue;
                }

                reservedSet.Add(poster);
            }

            reservedSet.Add(objectKey);
            return objectKey;
        }

        throw new InvalidOperationException($"无法为 {prefix}{stem} 分配新的对象键。");
    }

    /// <summary>
    /// 形象等扁平栏目沿用投放文件名，冲突时追加两位序号。
    /// </summary>
    private static string AllocateFlatFile(
        WorkspaceSession session,
        string channel,
        string? stageRel,
        string extension,
        ISet<string> reservedSet)
    {
        var stem = ReadStageStem(stageRel);
        if (string.IsNullOrWhiteSpace(stem))
        {
            throw new InvalidOperationException($"无法从投放路径为 {channel} 分配扁平对象键。");
        }

        for (var number = 1; number < 1000; number++)
        {
            var suffix = number == 1 ? "" : "-" + number.ToString("00");
            var objectKey = $"{channel}/{stem}{suffix}{extension}";
            if (reservedSet.Contains(objectKey) || KeyExists(session, objectKey))
            {
                continue;
            }

            reservedSet.Add(objectKey);
            return objectKey;
        }

        throw new InvalidOperationException($"无法为 {channel}/{stem} 分配新的扁平对象键。");
    }

    /// <summary>
    /// 取投放文件主名，不含扩展名。
    /// </summary>
    private static string ReadStageStem(string? stageRel)
    {
        var stageName = Path.GetFileName(stageRel?.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetFileNameWithoutExtension(stageName) ?? "";
    }

    /// <summary>
    /// 已撤下键只在仍属于本次目标作品时沿用。换作品则改分新键。
    /// </summary>
    private static bool ReuseWithdrawnKey(
        WorkspaceSession session,
        string channel,
        string workId,
        string objectKey)
    {
        var channelProfile = session.Profile.Channels.FirstOrDefault(item => item.Key == channel);
        if (string.Equals(channelProfile?.ObjectKeyPattern, "flat-file", StringComparison.OrdinalIgnoreCase))
        {
            return objectKey.StartsWith(channel + "/", StringComparison.Ordinal);
        }

        return objectKey.StartsWith(WorkDirectoryPrefix(channel, workId), StringComparison.Ordinal);
    }

    /// <summary>
    /// 同一 stageRel 处于 withdrawn 时找出原键。是否沿用由调用方按目标作品决定。
    /// 只看台账字段，不走投放箱显示索引；显示索引会让已撤下行把原片路径让给仍有效的新对象。
    /// </summary>
    public static string? FindWithdrawnObject(WorkspaceSession session, string stageRel)
    {
        foreach (var record in session.LedgerDict.Values)
        {
            if (!string.Equals(record.StageRel, stageRel, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(record.Status, "withdrawn", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(record.Object))
            {
                return record.Object;
            }
        }

        return null;
    }

    /// <summary>
    /// 作品目录前缀。现实摄影、游戏摄影、AI摄影为 <c>photo/{channel}/{项目夹名}/</c>。
    /// </summary>
    public static string WorkDirectoryPrefix(string channel, string workId)
    {
        if (channel is "real-world-photo" or "game-photo" or "ai-photo")
        {
            return $"photo/{channel}/{workId}/";
        }

        return $"{channel}/{workId}/";
    }

    /// <summary>
    /// 统一扩展名为小写且带点。
    /// </summary>
    public static string NormalizeExt(string extension)
    {
        var trimmed = string.IsNullOrWhiteSpace(extension) ? ".png" : extension.Trim();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// 台账或正式位是否已有该键。
    /// </summary>
    private static bool KeyExists(WorkspaceSession session, string objectKey)
    {
        if (session.LedgerDict.ContainsKey(objectKey))
        {
            return true;
        }

        foreach (var work in session.Works)
        {
            foreach (var media in work.Media)
            {
                if (string.Equals(media.Src, objectKey, StringComparison.Ordinal)
                    || string.Equals(media.Poster, objectKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        var placeholdersRoot = WorkspaceProfileLoader.ResolveUnderRoot(
            session.Profile,
            session.Profile.PlaceholdersRoot);
        var disk = Path.Combine(placeholdersRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(disk);
    }
}
