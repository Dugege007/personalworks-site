namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机执行成功后等待「发布上线」的队列。
/// </summary>
public sealed class PendingPublish
{
    public int Version { get; set; } = 1;
    public string ProfilePath { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Deploy { get; set; } = "spa";
    public List<string> WithdrawObjectList { get; set; } = new();
    public List<string> IngestObjectList { get; set; } = new();
    public List<string> PurgeUrlList { get; set; } = new();

    /// <summary>
    /// 本批只有文案、没有隐藏 / 上页 / 撤下。
    /// </summary>
    public bool CopyOnly { get; set; }
}

/// <summary>
/// 读写本机待发布队列。
/// </summary>
public static class PendingPublishStore
{
    /// <summary>
    /// %AppData%/SiteMediaStudio/pending-publish.json
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "pending-publish.json");
        }
    }

    /// <summary>
    /// 本机步成功后应写入的队列片段；仅回收则 null。
    /// </summary>
    public static PendingPublish? FromDocument(
        WorkspaceProfile profile,
        IntentDocument document,
        IReadOnlyList<string> refreshUrlList,
        WorkspaceSession? session = null)
    {
        var ingestList = document.Items
            .Where(item =>
                item.Intent == MediaIntentCodes.StageIngest
                && !string.IsNullOrWhiteSpace(item.Object)
                && (session == null || !IntentGate.IsRedundantIngest(session, item)))
            .Select(item => item.Object!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var withdrawList = ExpandWithdrawObjects(document, session)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hideOnly = document.Items.Any(item =>
            item.Intent == MediaIntentCodes.SiteHide
            && (session == null || !IntentGate.IsRedundantHide(session, item)));
        var restoreOnly = document.Items.Any(item =>
            item.Intent == MediaIntentCodes.SiteRestore
            && (session == null || !IntentGate.IsRedundantRestore(session, item)));
        var copyOnly = document.Items.Any(item =>
            item.Intent is MediaIntentCodes.CopyUpdate or MediaIntentCodes.WorkUpdate or MediaIntentCodes.MediaReorder or MediaIntentCodes.TagsUpdate or MediaIntentCodes.StarsUpdate);
        var workWithdraw = document.Items.Any(item => item.Intent == MediaIntentCodes.WorkWithdraw);
        if (ingestList.Count == 0 && withdrawList.Count == 0 && !hideOnly && !restoreOnly && !copyOnly && !workWithdraw)
        {
            return null;
        }

        return new PendingPublish
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Deploy = ingestList.Count > 0 ? "full" : "spa",
            WithdrawObjectList = withdrawList,
            IngestObjectList = ingestList,
            PurgeUrlList = DistinctPreserve(refreshUrlList),
            CopyOnly = copyOnly && !hideOnly && !restoreOnly && !workWithdraw && ingestList.Count == 0 && withdrawList.Count == 0
        };
    }

    /// <summary>
    /// 逐张撤下取对象键；整项撤下展开该作品的 src 与伴生封面。
    /// </summary>
    private static IEnumerable<string> ExpandWithdrawObjects(IntentDocument document, WorkspaceSession? session)
    {
        foreach (var item in document.Items)
        {
            if (item.Intent == MediaIntentCodes.SiteWithdraw && !string.IsNullOrWhiteSpace(item.Object))
            {
                yield return JsonUtil.ToRel(item.Object);
                continue;
            }

            if (item.Intent != MediaIntentCodes.WorkWithdraw)
            {
                continue;
            }

            var work = WorkWithdrawRules.FindWork(session?.Works, item.Channel, item.WorkId);
            if (work != null && session != null)
            {
                foreach (var objectKey in WorkWithdrawRules.CollectObjectKeys(
                    work,
                    session.LedgerDict,
                    WorkWithdrawRules.ResolvePlaceholders(session.Profile)))
                {
                    yield return objectKey;
                }

                continue;
            }

            if (item.ObjectList == null)
            {
                continue;
            }

            foreach (var objectKey in item.ObjectList)
            {
                if (!string.IsNullOrWhiteSpace(objectKey))
                {
                    yield return JsonUtil.ToRel(objectKey);
                }
            }
        }
    }

    /// <summary>
    /// 同工作区合并；有 full 则保持 full。
    /// </summary>
    public static PendingPublish Merge(PendingPublish? existing, PendingPublish incoming)
    {
        if (existing == null
            || !string.Equals(existing.ProfilePath, incoming.ProfilePath, StringComparison.OrdinalIgnoreCase))
        {
            return incoming;
        }

        return new PendingPublish
        {
            Version = 1,
            ProfilePath = incoming.ProfilePath,
            WorkspaceRoot = incoming.WorkspaceRoot,
            CreatedAt = existing.CreatedAt,
            Deploy = string.Equals(existing.Deploy, "full", StringComparison.OrdinalIgnoreCase)
                || string.Equals(incoming.Deploy, "full", StringComparison.OrdinalIgnoreCase)
                ? "full"
                : "spa",
            WithdrawObjectList = DistinctPreserve(existing.WithdrawObjectList.Concat(incoming.WithdrawObjectList)),
            IngestObjectList = DistinctPreserve(existing.IngestObjectList.Concat(incoming.IngestObjectList)),
            PurgeUrlList = DistinctPreserve(existing.PurgeUrlList.Concat(incoming.PurgeUrlList)),
            CopyOnly = existing.CopyOnly && incoming.CopyOnly
        };
    }

    /// <summary>
    /// 本工作区是否具备 deploy 凭证与脚本。
    /// </summary>
    public static bool HasDeployReady(WorkspaceProfile profile)
    {
        return !string.IsNullOrWhiteSpace(PublishPaths.FindDeployEnv(profile))
            && !string.IsNullOrWhiteSpace(PublishPaths.FindDeployScript(profile));
    }

    /// <summary>
    /// 磁盘队列是否属于当前工作区。
    /// </summary>
    public static bool BelongsToWorkspace(WorkspaceProfile profile, PendingPublish? pending)
    {
        return pending != null
            && string.Equals(pending.ProfilePath, profile.ProfilePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 队列部署方式是否为 spa 或 full。
    /// </summary>
    public static bool HasValidDeployKind(PendingPublish pending)
    {
        return string.Equals(pending.Deploy, "spa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pending.Deploy, "full", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 无本区有效队列时，内存中合成只发静态包的 spa。
    /// </summary>
    public static PendingPublish CreateManualSpa(WorkspaceProfile profile)
    {
        return new PendingPublish
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Deploy = "spa",
            CopyOnly = true
        };
    }

    /// <summary>
    /// 本区有效队列照用；无队列或属其它工作区则合成 spa，不改磁盘上它区队列。
    /// </summary>
    public static PendingPublish ResolveForPublish(WorkspaceProfile profile, PendingPublish? pending)
    {
        if (BelongsToWorkspace(profile, pending) && HasValidDeployKind(pending!))
        {
            return pending!;
        }

        return CreateManualSpa(profile);
    }

    /// <summary>
    /// 当前工作区是否具备发布凭证。无本区队列也可发静态包。
    /// </summary>
    public static bool CanPublish(WorkspaceProfile profile, PendingPublish? pending)
    {
        if (!HasDeployReady(profile))
        {
            return false;
        }

        if (pending == null || !BelongsToWorkspace(profile, pending))
        {
            return true;
        }

        return HasValidDeployKind(pending);
    }

    /// <summary>
    /// 底栏只读说明：有无队列、是否本工作区、有无凭证。按钮不因无队列置灰。
    /// </summary>
    public static string StatusLine(WorkspaceProfile profile, PendingPublish? pending)
    {
        var ready = HasDeployReady(profile);
        if (pending == null)
        {
            return ready
                ? "无待发布队列，可点「发布上线」发静态包。"
                : "无待发布队列。本工作区无 .env.deploy，发布会提示缺少凭证。";
        }

        if (!BelongsToWorkspace(profile, pending))
        {
            return ready
                ? "待发布队列属于其它工作区。当前可点「发布上线」发静态包。"
                : "待发布队列属于其它工作区。本工作区无 .env.deploy，发布会提示缺少凭证。";
        }

        if (!HasValidDeployKind(pending))
        {
            return "待发布队列无效。";
        }

        var partList = new List<string> { "待发布 " + pending.Deploy };
        if (pending.IngestObjectList.Count > 0)
        {
            partList.Add("入库 " + pending.IngestObjectList.Count);
        }

        if (pending.WithdrawObjectList.Count > 0)
        {
            partList.Add("待撤 " + pending.WithdrawObjectList.Count);
        }
        else if (pending.IngestObjectList.Count == 0)
        {
            partList.Add(pending.CopyOnly ? "仅文案" : "仅隐藏");
        }

        partList.Add(ready
            ? "可点「发布上线」"
            : "本工作区无 .env.deploy，发布会提示缺少凭证");
        return string.Join(" · ", partList) + "。";
    }

    /// <summary>
    /// 读取队列；缺失或损坏视为无。
    /// </summary>
    public static PendingPublish? Load(string? path = null)
    {
        var file = path ?? FilePath;
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PendingPublish>(
                File.ReadAllText(file),
                JsonUtil.Options);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 与磁盘队列合并后写回。
    /// </summary>
    public static void MergeSave(PendingPublish incoming, string? path = null)
    {
        var file = path ?? FilePath;
        var merged = Merge(Load(file), incoming);
        File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(merged, JsonUtil.Options), JsonUtil.Utf8NoBom);
    }

    /// <summary>
    /// 发布成功后清空。
    /// </summary>
    public static void Clear(string? path = null)
    {
        var file = path ?? FilePath;
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// 仅当磁盘队列属于当前工作区时清空，避免合成 spa 误删它区队列。
    /// </summary>
    public static void ClearIfCurrent(WorkspaceProfile profile, string? path = null)
    {
        if (BelongsToWorkspace(profile, Load(path)))
        {
            Clear(path);
        }
    }

    /// <summary>
    /// 去重并保持出现顺序。
    /// </summary>
    public static List<string> DistinctPreserve(IEnumerable<string> valueList)
    {
        var seenSet = new HashSet<string>(StringComparer.Ordinal);
        var resultList = new List<string>();
        foreach (var value in valueList)
        {
            if (string.IsNullOrWhiteSpace(value) || !seenSet.Add(value))
            {
                continue;
            }

            resultList.Add(value);
        }

        return resultList;
    }
}

/// <summary>
/// 发布脚本与凭证路径，不把仓库布局写死在调用方。
/// </summary>
public static class PublishPaths
{
    /// <summary>
    /// `{deployCwd}/.env.deploy`，缺失则 null。
    /// </summary>
    public static string? FindDeployEnv(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Cli?.DeployCwd))
        {
            return null;
        }

        var path = Path.Combine(
            WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.Cli.DeployCwd),
            ".env.deploy");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// `{deployCwd}/scripts/deploy.mjs`，缺失则 null。
    /// </summary>
    public static string? FindDeployScript(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Cli?.DeployCwd))
        {
            return null;
        }

        var path = Path.Combine(
            WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.Cli.DeployCwd),
            "scripts",
            "deploy.mjs");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 发布脚本工作目录。
    /// </summary>
    public static string? FindDeployCwd(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Cli?.DeployCwd))
        {
            return null;
        }

        var path = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.Cli.DeployCwd);
        return Directory.Exists(path) ? path : null;
    }
}
