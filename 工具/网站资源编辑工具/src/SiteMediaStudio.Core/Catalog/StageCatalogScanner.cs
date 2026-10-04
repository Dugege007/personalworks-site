namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 按工作区配置扫描投放箱图像、视频与游戏发布包。
/// </summary>
public static class StageCatalogScanner
{
    /// <summary>
    /// 扫描全部栏目投放箱，并尽量用台账或同名对象键配对。
    /// </summary>
    public static IReadOnlyList<StageItem> Scan(
        WorkspaceProfile profile,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict)
    {
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot);
        var placeholdersRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.PlaceholdersRoot);
        var itemList = new List<StageItem>();
        if (!Directory.Exists(stageRoot))
        {
            return itemList;
        }

        var ledgerByStageDict = BuildStageIndex(ledgerDict);

        foreach (var channel in profile.Channels)
        {
            var folderName = WorkspaceProfileLoader.ChannelStageRelative(profile, channel);
            var channelDir = Path.GetFullPath(Path.Combine(
                stageRoot,
                folderName.Replace('/', Path.DirectorySeparatorChar)));
            if (!Directory.Exists(channelDir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(channelDir, "*", SearchOption.AllDirectories))
            {
                if (!MediaPathRules.IsCatalogFile(file) && !NoteRules.IsBodyFile(channel.Key, file))
                {
                    continue;
                }

                itemList.Add(BuildFileItem(
                    channel,
                    stageRoot,
                    channelDir,
                    placeholdersRoot,
                    ledgerDict,
                    ledgerByStageDict,
                    file));
            }

            foreach (var packDir in Directory.EnumerateDirectories(channelDir, "*", SearchOption.AllDirectories))
            {
                var packItem = TryBuildPackItem(
                    channel,
                    stageRoot,
                    channelDir,
                    placeholdersRoot,
                    ledgerDict,
                    ledgerByStageDict,
                    packDir);
                if (packItem != null)
                {
                    itemList.Add(packItem);
                }
            }
        }

        itemList.Sort((a, b) => StageRelOrder.Compare(a.StageRel, b.StageRel));
        return itemList;
    }

    /// <summary>
    /// 组装一条图像或视频投放箱条目。
    /// </summary>
    private static StageItem BuildFileItem(
        ChannelProfile channel,
        string stageRoot,
        string channelDir,
        string placeholdersRoot,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        IReadOnlyDictionary<string, LedgerRecord> ledgerByStageDict,
        string file)
    {
        var stageRel = JsonUtil.ToRel(Path.GetRelativePath(stageRoot, file));
        var afterChannel = JsonUtil.ToRel(Path.GetRelativePath(channelDir, file));
        TryReadWorkFolder(afterChannel, channel, out var workIdGuess, out var stageFolderGuess);
        var objectGuess = $"{channel.Key}/{afterChannel}";
        ResolveMatch(
            stageRel,
            objectGuess,
            placeholdersRoot,
            ledgerDict,
            ledgerByStageDict,
            fileExists: true,
            out var matched,
            out var status,
            out var webPath);
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = file,
            ChannelKey = channel.Key,
            WorkIdGuess = workIdGuess,
            StageFolderGuess = stageFolderGuess,
            Length = new FileInfo(file).Length,
            LastWriteUtc = File.GetLastWriteTimeUtc(file),
            MatchedObject = matched,
            WebFullPath = webPath,
            LedgerStatus = status,
            IsStock = MediaPathRules.IsStock(stageRel) || MediaPathRules.IsStock(matched)
        };
    }

    /// <summary>
    /// 作品夹下的 <c>webgl</c> / <c>build</c> 收成一条整包；模板夹与包内嵌套目录跳过。
    /// </summary>
    private static StageItem? TryBuildPackItem(
        ChannelProfile channel,
        string stageRoot,
        string channelDir,
        string placeholdersRoot,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        IReadOnlyDictionary<string, LedgerRecord> ledgerByStageDict,
        string packDir)
    {
        if (!MediaPathRules.IsPackDirectory(packDir)
            || MediaPathRules.IsInsidePackDirectory(packDir)
            || MediaPathRules.IsDerivedStageRel(packDir))
        {
            return null;
        }

        var afterChannel = JsonUtil.ToRel(Path.GetRelativePath(channelDir, packDir));
        if (!TryReadWorkFolder(afterChannel, channel, out var workIdGuess, out var stageFolderGuess)
            || MediaPathRules.IsGamePackTemplate(workIdGuess))
        {
            return null;
        }

        var stageRel = JsonUtil.ToRel(Path.GetRelativePath(stageRoot, packDir));
        var objectGuess = $"{channel.Key}/{afterChannel}/";
        ResolveMatch(
            stageRel,
            objectGuess,
            placeholdersRoot,
            ledgerDict,
            ledgerByStageDict,
            fileExists: false,
            out var matched,
            out var status,
            out var webPath);
        var workFolder = Path.GetDirectoryName(packDir);
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = packDir,
            ChannelKey = channel.Key,
            WorkIdGuess = workIdGuess,
            StageFolderGuess = stageFolderGuess,
            Length = ReadPackLength(packDir),
            LastWriteUtc = Directory.GetLastWriteTimeUtc(packDir),
            MatchedObject = matched,
            WebFullPath = webPath,
            LedgerStatus = status,
            IsStock = MediaPathRules.IsStock(stageRel) || MediaPathRules.IsStock(matched),
            IsPack = true,
            ThumbPath = GamePackThumb.Resolve(workFolder, packDir)
        };
    }

    /// <summary>
    /// 读取栏目内作品夹猜测。
    /// </summary>
    private static bool TryReadWorkFolder(
        string afterChannel,
        ChannelProfile channel,
        out string? workIdGuess,
        out string? stageFolderGuess)
    {
        workIdGuess = null;
        stageFolderGuess = null;
        if (!StageWorkFolder.TryRead(afterChannel, channel, out var guessedId, out var folderUnderChannel))
        {
            return false;
        }

        workIdGuess = guessedId;
        stageFolderGuess = folderUnderChannel;
        return true;
    }

    /// <summary>
    /// 按台账投放路径、同名对象键或正式位存在性解析配对。对象已指向另一条投放路径时不再占用。
    /// </summary>
    private static void ResolveMatch(
        string stageRel,
        string objectGuess,
        string placeholdersRoot,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        IReadOnlyDictionary<string, LedgerRecord> ledgerByStageDict,
        bool fileExists,
        out string? matched,
        out string? status,
        out string? webPath)
    {
        ledgerByStageDict.TryGetValue(stageRel, out var byStage);
        LedgerRecord? byObject = null;
        var claimedByOtherStage = false;
        if (byStage == null)
        {
            ledgerDict.TryGetValue(objectGuess, out byObject);
            if (byObject == null && objectGuess.EndsWith('/'))
            {
                ledgerDict.TryGetValue(objectGuess.TrimEnd('/'), out byObject);
            }

            // 对象已记在另一条投放路径上时，同名路径或正式位不能再把它配给当前文件。
            if (byObject != null && ClaimsAnotherStage(byObject, stageRel))
            {
                claimedByOtherStage = true;
                byObject = null;
            }
        }

        byStage ??= FindDerivedLedger(stageRel, ledgerByStageDict);
        matched = byStage?.Object ?? (byObject != null ? objectGuess : null);
        var destRel = (matched ?? objectGuess).TrimEnd('/');
        var destPath = Path.Combine(placeholdersRoot, destRel.Replace('/', Path.DirectorySeparatorChar));
        if (matched == null
            && !claimedByOtherStage
            && (fileExists ? File.Exists(destPath) : Directory.Exists(destPath)))
        {
            matched = objectGuess;
        }

        status = byStage?.Status ?? byObject?.Status;
        webPath = null;
        if (string.IsNullOrWhiteSpace(matched))
        {
            return;
        }

        var candidate = Path.Combine(
            placeholdersRoot,
            matched.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        if (fileExists ? File.Exists(candidate) : Directory.Exists(candidate))
        {
            webPath = candidate;
        }
    }

    /// <summary>
    /// 发布包体积按夹内文件合计，读失败时记 0。
    /// </summary>
    private static long ReadPackLength(string packDir)
    {
        try
        {
            return Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path).Length)
                .Sum();
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    /// 按台账 <c>stageRel</c> 建索引，再补 <c>sourceStageRel</c>。
    /// 两条都仍有效时，先写入的 <c>stageRel</c> 不让位；已撤下的历史行让给 published / stock。
    /// </summary>
    public static Dictionary<string, LedgerRecord> BuildStageIndex(
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict)
    {
        var ledgerByStageDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        foreach (var record in ledgerDict.Values)
        {
            TryAddStageKey(ledgerByStageDict, record.StageRel, record);
        }

        foreach (var record in ledgerDict.Values)
        {
            TryAddStageKey(ledgerByStageDict, record.SourceStageRel, record);
        }

        return ledgerByStageDict;
    }

    /// <summary>
    /// 空键跳过。键已被仍有效的台账占用时不改写；占用方已撤下时，published / stock 可以替换。
    /// </summary>
    private static void TryAddStageKey(
        Dictionary<string, LedgerRecord> ledgerByStageDict,
        string? key,
        LedgerRecord record)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var rel = JsonUtil.ToRel(key);
        if (!ledgerByStageDict.TryGetValue(rel, out var existing))
        {
            ledgerByStageDict[rel] = record;
            return;
        }

        // 撤下后再以新对象键上页时，旧行的 stageRel 仍指原片，新行只把原片写在 sourceStageRel。
        if (!IsCurrentLedger(existing) && IsCurrentLedger(record))
        {
            ledgerByStageDict[rel] = record;
        }
    }

    /// <summary>
    /// 台账投放路径非空，且不是当前文件。
    /// </summary>
    private static bool ClaimsAnotherStage(LedgerRecord record, string stageRel)
    {
        return !string.IsNullOrWhiteSpace(record.StageRel)
            && !string.Equals(record.StageRel, stageRel, StringComparison.Ordinal);
    }

    /// <summary>
    /// 台账仍代表当前正式位。已撤下只留历史，不能独占原片路径。
    /// </summary>
    private static bool IsCurrentLedger(LedgerRecord record)
    {
        return string.Equals(record.Status, "published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.Status, "stock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 原片与同目录 <c>.site-ready</c> 下成片共用台账：先对文件名，再对「序号 + 空格」的说明名；图像与视频不互认。
    /// 相机名、批次前缀、槽位补零不对文件名，须靠台账 <c>sourceStageRel</c>。
    /// </summary>
    public static LedgerRecord? FindDerivedLedger(
        string stageRel,
        IReadOnlyDictionary<string, LedgerRecord> ledgerByStage)
    {
        var rel = JsonUtil.ToRel(stageRel);
        var slash = rel.LastIndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        var prefix = rel[..slash] + "/.site-ready/";
        var stem = Path.GetFileNameWithoutExtension(rel);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return null;
        }

        LedgerRecord? byStem = null;
        LedgerRecord? byIndex = null;
        var indexHits = 0;
        var hasIndex = TryReadLeadingIndex(stem, out var index);
        foreach (var pair in ledgerByStage)
        {
            if (!pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ledgerStem = Path.GetFileNameWithoutExtension(pair.Key);
            if (string.Equals(ledgerStem, stem, StringComparison.OrdinalIgnoreCase))
            {
                byStem = pair.Value;
                break;
            }

            if (hasIndex
                && string.Equals(ledgerStem, index, StringComparison.OrdinalIgnoreCase)
                && SameMediaClass(rel, pair.Key))
            {
                byIndex = pair.Value;
                indexHits++;
            }
        }

        return byStem ?? (indexHits == 1 ? byIndex : null);
    }

    /// <summary>
    /// 读取「01」或「01 车间总览」这类序号；「04_1」不视为序号。
    /// </summary>
    private static bool TryReadLeadingIndex(string stem, out string index)
    {
        index = "";
        var count = 0;
        while (count < stem.Length && char.IsDigit(stem[count]))
        {
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        if (count < stem.Length && stem[count] is not (' ' or '\u3000'))
        {
            return false;
        }

        index = stem[..count];
        return true;
    }

    /// <summary>
    /// 图像与视频分属不同媒类，避免演示片吃掉同序号成片。
    /// </summary>
    private static bool SameMediaClass(string leftPath, string rightPath)
    {
        var leftVideo = MediaPathRules.IsVideoFile(leftPath);
        var rightVideo = MediaPathRules.IsVideoFile(rightPath);
        return leftVideo == rightVideo;
    }
}
