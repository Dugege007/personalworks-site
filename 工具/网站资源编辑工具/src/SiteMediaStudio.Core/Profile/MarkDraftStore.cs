using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机未执行标记；不进 Git，不写内容层。
/// </summary>
public sealed class MarkDraftFile
{
    public int Version { get; set; } = 1;
    public string ProfilePath { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public List<MarkDraftEntry> Entries { get; set; } = new();
}

/// <summary>
/// 一张资源上的未执行意图。
/// </summary>
public sealed class MarkDraftEntry
{
    public string Key { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Channel { get; set; } = "";
    public string? WorkId { get; set; }
    public string UpdatedAt { get; set; } = "";
}

/// <summary>
/// 项目栏副标前的计数：上页、隐藏、撤下、回收；零项省略。
/// </summary>
public readonly record struct MarkCountTally(int Ingest, int Hide, int Withdraw, int Recycle)
{
    /// <summary>
    /// 四项合计。
    /// </summary>
    public int Total => Ingest + Hide + Withdraw + Recycle;

    /// <summary>
    /// 是否有任一项。
    /// </summary>
    public bool HasAny => Total > 0;
}

/// <summary>
/// 原子读写 %AppData%/SiteMediaStudio/marks.json。
/// </summary>
public static class MarkDraftStore
{
    /// <summary>
    /// 本机标记文件路径。
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "marks.json");
        }
    }

    /// <summary>
    /// 投放箱卡片键，与网格 <c>CardKey</c> 一致。
    /// </summary>
    public static string StageKey(string stageRel)
    {
        return "stage:" + JsonUtil.ToRel(stageRel);
    }

    /// <summary>
    /// 站点卡片键，与网格 <c>CardKey</c> 一致。
    /// </summary>
    public static string SiteKey(string objectKey)
    {
        return "site:" + JsonUtil.ToRel(objectKey);
    }

    /// <summary>
    /// 由意图条生成卡片键；文案条无对应标记键。压图 / 压码改写 <c>StageRel</c> 后仍用原片路径。
    /// </summary>
    public static string? KeyOf(IntentItem item)
    {
        if (item == null || item.Intent == MediaIntentCodes.CopyUpdate
            || item.Intent == MediaIntentCodes.WorkRegister)
        {
            return null;
        }

        if (item.Intent is MediaIntentCodes.SiteHide
            or MediaIntentCodes.SiteRestore
            or MediaIntentCodes.SiteWithdraw)
        {
            return string.IsNullOrWhiteSpace(item.Object) ? null : SiteKey(item.Object);
        }

        var stageRel = string.IsNullOrWhiteSpace(item.SourceStageRel) ? item.StageRel : item.SourceStageRel;
        return string.IsNullOrWhiteSpace(stageRel) ? null : StageKey(stageRel);
    }

    /// <summary>
    /// 本批媒体条对应的标记键；文案条不计。须在执行改写路径之前调用。
    /// </summary>
    public static IReadOnlyList<string> KeysOf(IntentDocument document)
    {
        if (document == null)
        {
            return Array.Empty<string>();
        }

        return document.Items
            .Select(KeyOf)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 由投放箱或站点条目生成卡片键。
    /// </summary>
    public static string? KeyOf(StageItem? stage, SiteItem? site)
    {
        if (stage != null && !string.IsNullOrWhiteSpace(stage.StageRel))
        {
            return StageKey(stage.StageRel);
        }

        if (site != null && !string.IsNullOrWhiteSpace(site.ObjectKey))
        {
            return SiteKey(site.ObjectKey);
        }

        return null;
    }

    /// <summary>
    /// 读取标记；缺失或损坏视为空文件，不抛。
    /// </summary>
    public static MarkDraftFile Load(string? path = null)
    {
        var file = path ?? FilePath;
        if (!File.Exists(file))
        {
            return new MarkDraftFile();
        }

        try
        {
            return JsonSerializer.Deserialize<MarkDraftFile>(File.ReadAllText(file), JsonUtil.Options)
                ?? new MarkDraftFile();
        }
        catch (JsonException)
        {
            return new MarkDraftFile();
        }
        catch (IOException)
        {
            return new MarkDraftFile();
        }
    }

    /// <summary>
    /// 工作区匹配且键相同则返回该条；否则空。
    /// </summary>
    public static MarkDraftEntry? Find(MarkDraftFile file, WorkspaceProfile profile, string key)
    {
        if (file == null || profile == null || string.IsNullOrWhiteSpace(key) || !MatchesWorkspace(file, profile))
        {
            return null;
        }

        return file.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// 写入或清除一条标记；意图为空则删掉该条；文件空则删除。
    /// </summary>
    public static void Commit(
        WorkspaceProfile profile,
        string key,
        MediaIntent intent,
        string channel,
        string? workId,
        string? path = null)
    {
        CommitMany(
            profile,
            new[] { new MarkDraftWrite(key, intent, channel, workId) },
            path);
    }

    /// <summary>
    /// 批量写入或清除；文件空则删除。
    /// </summary>
    public static void CommitMany(
        WorkspaceProfile profile,
        IEnumerable<MarkDraftWrite> writeList,
        string? path = null)
    {
        if (profile == null)
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            file = NewFile(profile);
        }

        foreach (var write in writeList)
        {
            if (string.IsNullOrWhiteSpace(write.Key))
            {
                continue;
            }

            file.Entries.RemoveAll(entry =>
                string.Equals(entry.Key, write.Key, StringComparison.Ordinal));
            if (write.Intent == MediaIntent.None)
            {
                continue;
            }

            file.Entries.Add(new MarkDraftEntry
            {
                Key = write.Key,
                Intent = MediaIntentCodes.ToCode(write.Intent),
                Channel = write.Channel ?? "",
                WorkId = string.IsNullOrWhiteSpace(write.WorkId) ? null : write.WorkId,
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        if (file.Entries.Count == 0)
        {
            Delete(filePath);
            return;
        }

        Save(file, filePath);
    }

    /// <summary>
    /// 删掉已处理的标记；文件空则删除。上页逐条成功即可调用，不必等整批结束。
    /// </summary>
    public static void RemoveKeys(WorkspaceProfile profile, IEnumerable<string> keyList, string? path = null)
    {
        if (profile == null)
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            return;
        }

        var removeSet = new HashSet<string>(
            keyList.Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.Ordinal);
        if (removeSet.Count == 0)
        {
            return;
        }

        file.Entries.RemoveAll(entry => removeSet.Contains(entry.Key));
        if (file.Entries.Count == 0)
        {
            Delete(filePath);
            return;
        }

        Save(file, filePath);
    }

    /// <summary>
    /// 编目里已不存在的键丢掉，避免侧栏幽灵计数。
    /// </summary>
    public static void PruneMissing(WorkspaceProfile profile, WorkspaceSession session, string? path = null)
    {
        if (profile == null || session == null)
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            return;
        }

        var liveSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in session.StageItems)
        {
            liveSet.Add(StageKey(stage.StageRel));
        }

        foreach (var site in session.SiteItems)
        {
            liveSet.Add(SiteKey(site.ObjectKey));
        }

        var before = file.Entries.Count;
        file.Entries.RemoveAll(entry => !liveSet.Contains(entry.Key));
        if (file.Entries.Count == before)
        {
            return;
        }

        if (file.Entries.Count == 0)
        {
            Delete(filePath);
            return;
        }

        Save(file, filePath);
    }

    /// <summary>
    /// 当前文件是否属于该工作区。
    /// </summary>
    public static bool MatchesWorkspace(MarkDraftFile file, WorkspaceProfile profile)
    {
        if (file == null || profile == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(file.ProfilePath)
            && string.Equals(file.ProfilePath, profile.ProfilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(file.WorkspaceRoot ?? "", profile.ResolvedRoot ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按意图累加四项计数；上页含恢复显示。
    /// </summary>
    public static MarkCountTally Tally(IEnumerable<MediaIntent> intentList)
    {
        var ingest = 0;
        var hide = 0;
        var withdraw = 0;
        var recycle = 0;
        foreach (var intent in intentList)
        {
            switch (intent)
            {
                case MediaIntent.StageIngest:
                case MediaIntent.SiteRestore:
                    ingest++;
                    break;
                case MediaIntent.SiteHide:
                    hide++;
                    break;
                case MediaIntent.SiteWithdraw:
                    withdraw++;
                    break;
                case MediaIntent.StageRecycle:
                    recycle++;
                    break;
            }
        }

        return new MarkCountTally(ingest, hide, withdraw, recycle);
    }

    /// <summary>
    /// 按栏目或项目筛出计数；栏目节点计该栏全部，作品节点只计该作品。
    /// </summary>
    public static MarkCountTally TallyForNav(
        MarkDraftFile file,
        WorkspaceProfile profile,
        string channel,
        string? workId,
        IReadOnlyCollection<string>? workIdSet)
    {
        if (!MatchesWorkspace(file, profile) || string.IsNullOrWhiteSpace(channel))
        {
            return default;
        }

        return Tally(file.Entries
            .Where(entry => MatchesNav(entry, channel, workId, workIdSet))
            .Select(entry => MediaIntentCodes.FromCode(entry.Intent)));
    }

    /// <summary>
    /// 零项省略，顺序为上页、隐藏、撤下、回收。
    /// </summary>
    public static IReadOnlyList<(MediaIntent Intent, int Count)> VisibleParts(MarkCountTally tally)
    {
        var partList = new List<(MediaIntent, int)>(4);
        if (tally.Ingest > 0)
        {
            partList.Add((MediaIntent.StageIngest, tally.Ingest));
        }

        if (tally.Hide > 0)
        {
            partList.Add((MediaIntent.SiteHide, tally.Hide));
        }

        if (tally.Withdraw > 0)
        {
            partList.Add((MediaIntent.SiteWithdraw, tally.Withdraw));
        }

        if (tally.Recycle > 0)
        {
            partList.Add((MediaIntent.StageRecycle, tally.Recycle));
        }

        return partList;
    }

    /// <summary>
    /// 由当前编目找回投放箱或站点条目。
    /// </summary>
    public static (StageItem? Stage, SiteItem? Site) Resolve(WorkspaceSession session, MarkDraftEntry entry)
    {
        if (session == null || entry == null || string.IsNullOrWhiteSpace(entry.Key))
        {
            return (null, null);
        }

        if (entry.Key.StartsWith("stage:", StringComparison.Ordinal))
        {
            var rel = entry.Key["stage:".Length..];
            var stage = session.StageItems.FirstOrDefault(item =>
                string.Equals(JsonUtil.ToRel(item.StageRel), rel, StringComparison.Ordinal));
            return (stage, null);
        }

        if (entry.Key.StartsWith("site:", StringComparison.Ordinal))
        {
            var objectKey = entry.Key["site:".Length..];
            var site = session.SiteItems.FirstOrDefault(item =>
                string.Equals(JsonUtil.ToRel(item.ObjectKey), objectKey, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(entry.WorkId)
                    || string.Equals(item.WorkId, entry.WorkId, StringComparison.Ordinal)));
            return (null, site);
        }

        return (null, null);
    }

    /// <summary>
    /// 删除标记文件；缺失不报错。
    /// </summary>
    public static void Delete(string? path = null)
    {
        var file = path ?? FilePath;
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// 条目是否落在该栏目、作品或作品集合内。
    /// </summary>
    private static bool MatchesNav(
        MarkDraftEntry entry,
        string channel,
        string? workId,
        IReadOnlyCollection<string>? workIdSet)
    {
        if (!string.Equals(entry.Channel, channel, StringComparison.Ordinal))
        {
            return false;
        }

        if (workIdSet != null)
        {
            return !string.IsNullOrWhiteSpace(entry.WorkId) && workIdSet.Contains(entry.WorkId);
        }

        if (string.IsNullOrWhiteSpace(workId))
        {
            return true;
        }

        return string.Equals(entry.WorkId, workId, StringComparison.Ordinal);
    }

    private static MarkDraftFile NewFile(WorkspaceProfile profile)
    {
        return new MarkDraftFile
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot
        };
    }

    private static void Save(MarkDraftFile file, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(file, JsonUtil.Options);
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, json, JsonUtil.Utf8NoBom);
        try
        {
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}

/// <summary>
/// 一条待写入的标记。
/// </summary>
public readonly record struct MarkDraftWrite(string Key, MediaIntent Intent, string Channel, string? WorkId);
