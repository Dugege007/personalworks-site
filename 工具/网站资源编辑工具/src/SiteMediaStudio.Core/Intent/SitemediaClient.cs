using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 编排既有 sitemedia.mjs，不复制其闸门。
/// </summary>
public static class SitemediaClient
{
    private static readonly Regex RefCountRegex = new(
        @"内容层引用：(\d+)",
        RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(
        @"^\s+(https?://\S+|/\S+)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LedgerRenameRetryRegex = new(
        @"^台账改名重试：(\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LedgerRenameAttemptRegex = new(
        @"^台账改名重试中：(\d+)\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// ingest 参数；禁止附加 prune。
    /// </summary>
    public static IReadOnlyList<string> BuildIngestArgs(
        string stageRel,
        string objectKey,
        bool bump,
        string? sourceStageRel = null)
    {
        var argumentList = new List<string>
        {
            "ingest",
            stageRel,
            "--object",
            objectKey
        };
        if (bump)
        {
            argumentList.Add("--bump");
        }

        var source = JsonUtil.ToRel(sourceStageRel ?? "");
        var prepared = JsonUtil.ToRel(stageRel);
        if (!string.IsNullOrWhiteSpace(source)
            && !string.Equals(source, prepared, StringComparison.Ordinal))
        {
            argumentList.Add("--source-stage-rel");
            argumentList.Add(source);
        }

        return argumentList;
    }

    /// <summary>
    /// withdraw 参数；必须且只能带 dry-run 或 apply。
    /// </summary>
    public static IReadOnlyList<string> BuildWithdrawArgs(string objectKey, bool apply)
    {
        return new[]
        {
            "withdraw",
            objectKey,
            apply ? "--apply" : "--dry-run"
        };
    }

    /// <summary>
    /// 按工作区配置重映射 sitemedia 路径；不把 PersonalWorks 目录写死在调用方。
    /// </summary>
    public static Dictionary<string, string> BuildEnvironment(WorkspaceProfile profile)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WORKSPACE_ROOT"] = profile.ResolvedRoot,
            ["SITEMEDIA_STAGE_ROOT"] = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot),
            ["SITEMEDIA_PLACEHOLDERS_ROOT"] = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.PlaceholdersRoot)
        };

        if (!string.IsNullOrWhiteSpace(profile.LedgerPath))
        {
            env["SITEMEDIA_LEDGER"] = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.LedgerPath);
        }

        env["SITEMEDIA_CONTENT_ROOT"] = ResolveContentRoot(profile);

        if (!string.IsNullOrWhiteSpace(profile.Cli?.DeployCwd))
        {
            var deployEnv = Path.Combine(
                WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.Cli.DeployCwd),
                ".env.deploy");
            if (File.Exists(deployEnv))
            {
                env["SITEMEDIA_DEPLOY_ENV"] = deployEnv;
            }
        }

        return env;
    }

    /// <summary>
    /// 入库一条；调用方须已过闸门。
    /// </summary>
    public static SitemediaRunResult Ingest(
        WorkspaceProfile profile,
        string stageRel,
        string objectKey,
        bool bump,
        string? sourceStageRel = null,
        Action<string>? onOutputLine = null)
    {
        return Invoke(profile, BuildIngestArgs(stageRel, objectKey, bump, sourceStageRel), onOutputLine);
    }

    /// <summary>
    /// 撤下一条。apply 前须 dry-run 且引用为零。
    /// </summary>
    public static SitemediaRunResult Withdraw(
        WorkspaceProfile profile,
        string objectKey,
        bool apply,
        Action<string>? onOutputLine = null)
    {
        return Invoke(profile, BuildWithdrawArgs(objectKey, apply), onOutputLine);
    }

    /// <summary>
    /// Windows 创建进程命令行上限约 32767；刷新参数按此预算分批，避免误报找不到 node。
    /// </summary>
    public const int PurgeCommandCharBudget = 20_000;

    /// <summary>
    /// 腾讯云 PurgeUrlsCache 单次上限。
    /// </summary>
    public const int PurgeUrlBatchLimit = 1000;

    /// <summary>
    /// CDN URL 刷新参数；禁止 prune。条数少时即一整批。
    /// </summary>
    public static IReadOnlyList<string> BuildPurgeArgs(
        IReadOnlyList<string> objectList,
        IReadOnlyList<string> urlList,
        bool dryRun)
    {
        var batchList = BuildPurgeBatches(objectList, urlList, dryRun);
        if (batchList.Count == 0)
        {
            var empty = new List<string> { "purge" };
            if (dryRun)
            {
                empty.Add("--dry-run");
            }

            return empty;
        }

        return batchList[0];
    }

    /// <summary>
    /// 按命令行字符预算与 1000 条上限拆成多批 purge 参数。
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> BuildPurgeBatches(
        IReadOnlyList<string> objectList,
        IReadOnlyList<string> urlList,
        bool dryRun)
    {
        var pairList = new List<(string Flag, string Value)>();
        foreach (var objectKey in objectList.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            pairList.Add(("--object", objectKey));
        }

        foreach (var url in urlList.Where(item =>
                     item.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || item.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            pairList.Add(("--url", url));
        }

        if (pairList.Count == 0)
        {
            return Array.Empty<IReadOnlyList<string>>();
        }

        var batchList = new List<IReadOnlyList<string>>();
        var current = NewPurgeBatch(dryRun);
        var used = EstimateCommandChars(current);
        var pairCount = 0;
        foreach (var pair in pairList)
        {
            var extra = EstimateCommandChars(new[] { pair.Flag, pair.Value });
            if (current.Count > (dryRun ? 2 : 1)
                && (used + extra > PurgeCommandCharBudget || pairCount >= PurgeUrlBatchLimit))
            {
                batchList.Add(current);
                current = NewPurgeBatch(dryRun);
                used = EstimateCommandChars(current);
                pairCount = 0;
            }

            current.Add(pair.Flag);
            current.Add(pair.Value);
            used += extra;
            pairCount++;
        }

        batchList.Add(current);
        return batchList;
    }

    /// <summary>
    /// 估一条命令行的字符数（空格与必要引号）。
    /// </summary>
    public static int EstimateCommandChars(IEnumerable<string> argumentList)
    {
        var chars = 0;
        foreach (var argument in argumentList)
        {
            chars += 1 + argument.Length;
            if (argument.IndexOfAny([' ', '"']) >= 0)
            {
                chars += 2;
            }
        }

        return chars;
    }

    /// <summary>
    /// 提交或预演 CDN 刷新。参数过长时分批调用。
    /// </summary>
    public static SitemediaRunResult Purge(
        WorkspaceProfile profile,
        IReadOnlyList<string> objectList,
        IReadOnlyList<string> urlList,
        bool dryRun)
    {
        var batchList = BuildPurgeBatches(objectList, urlList, dryRun);
        if (batchList.Count == 0)
        {
            throw new InvalidOperationException("purge 需要至少一个对象键或 HTTPS URL。");
        }

        var stdOutList = new List<string>();
        var stdErrList = new List<string>();
        var refreshUrlList = new List<string>();
        var last = new SitemediaRunResult();
        foreach (var argumentList in batchList)
        {
            last = Invoke(profile, argumentList);
            if (!string.IsNullOrWhiteSpace(last.StdOut))
            {
                stdOutList.Add(last.StdOut);
            }

            if (!string.IsNullOrWhiteSpace(last.StdErr))
            {
                stdErrList.Add(last.StdErr);
            }

            refreshUrlList.AddRange(last.RefreshUrlList);
            if (!last.Ok)
            {
                break;
            }
        }

        return new SitemediaRunResult
        {
            Ok = last.Ok,
            ExitCode = last.ExitCode,
            StdOut = string.Join(Environment.NewLine, stdOutList),
            StdErr = string.Join(Environment.NewLine, stdErrList),
            ArgumentList = batchList.SelectMany(item => item).ToList(),
            RefreshUrlList = PendingPublishStore.DistinctPreserve(refreshUrlList),
            ContentRefCount = last.ContentRefCount
        };
    }

    /// <summary>
    /// 从 stdout 读取「内容层引用：N」。缺省视为无法确认。
    /// </summary>
    public static int? ParseContentRefCount(string stdout)
    {
        var match = RefCountRegex.Match(stdout ?? "");
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>
    /// 收集待刷 CDN 行；忽略说明句。
    /// </summary>
    public static IReadOnlyList<string> ParseRefreshUrls(string stdout)
    {
        var text = stdout ?? "";
        var marker = text.IndexOf("待刷新 CDN", StringComparison.Ordinal);
        if (marker < 0)
        {
            return Array.Empty<string>();
        }

        var slice = text[marker..];
        var urlList = new List<string>();
        foreach (Match match in UrlRegex.Matches(slice))
        {
            var url = match.Groups[1].Value.Trim();
            if (url.Contains("prune-assets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            urlList.Add(url);
        }

        return urlList;
    }

    /// <summary>
    /// 从 stdout 读取「台账改名重试：N」。没有则视为未重试。
    /// </summary>
    public static int ParseLedgerRenameRetries(string? stdout)
    {
        var match = LedgerRenameRetryRegex.Match(stdout ?? "");
        if (!match.Success)
        {
            return 0;
        }

        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>
    /// 解析一行「台账改名重试中：N」或「台账改名重试：N」。对不上则为 0。
    /// </summary>
    public static int ParseLedgerRenameAttempt(string? line)
    {
        var text = (line ?? "").Trim();
        var retrying = LedgerRenameAttemptRegex.Match(text);
        if (retrying.Success)
        {
            return int.Parse(retrying.Groups[1].Value);
        }

        return ParseLedgerRenameRetries(text);
    }

    /// <summary>
    /// 参数表不得出现 prune-assets。
    /// </summary>
    public static bool ContainsPrune(IEnumerable<string> argumentList)
    {
        return argumentList.Any(item =>
            item.Contains("prune-assets", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 新开一批 purge 参数头。
    /// </summary>
    private static List<string> NewPurgeBatch(bool dryRun)
    {
        var argumentList = new List<string> { "purge" };
        if (dryRun)
        {
            argumentList.Add("--dry-run");
        }

        return argumentList;
    }

    /// <summary>
    /// 内容扫描根：PersonalWorks 用 src，JSON 编目用编目所在目录。
    /// </summary>
    public static string ResolveContentRoot(WorkspaceProfile profile)
    {
        if (string.Equals(profile.SiteCatalog.Kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(profile.LedgerPath))
        {
            var ledgerDir = Path.GetDirectoryName(
                WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.LedgerPath));
            if (!string.IsNullOrWhiteSpace(ledgerDir))
            {
                var srcDir = Directory.GetParent(ledgerDir)?.FullName;
                if (!string.IsNullOrWhiteSpace(srcDir))
                {
                    return srcDir;
                }
            }
        }

        var catalogPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        return Path.GetDirectoryName(catalogPath) ?? profile.ResolvedRoot;
    }

    /// <summary>
    /// 启动 sitemedia 并带上重映射环境。
    /// </summary>
    private static SitemediaRunResult Invoke(
        WorkspaceProfile profile,
        IReadOnlyList<string> argumentList,
        Action<string>? onOutputLine = null)
    {
        if (ContainsPrune(argumentList))
        {
            throw new InvalidOperationException("sitemedia 调用禁止出现 --prune-assets。");
        }

        var script = ToolPaths.FindSitemediaScript(profile);
        if (script == null)
        {
            throw new FileNotFoundException("找不到 sitemedia.mjs。");
        }

        var cwd = Path.GetDirectoryName(script) ?? Directory.GetCurrentDirectory();
        var run = NodeHost.Run(
            script,
            argumentList,
            cwd,
            environment: BuildEnvironment(profile),
            onOutputLine: onOutputLine);
        return new SitemediaRunResult
        {
            Ok = run.Ok,
            ExitCode = run.ExitCode,
            StdOut = run.StdOut,
            StdErr = run.StdErr,
            ArgumentList = argumentList.ToList(),
            RefreshUrlList = ParseRefreshUrls(run.StdOut),
            ContentRefCount = ParseContentRefCount(run.StdOut)
        };
    }
}

/// <summary>
/// 一次 sitemedia 调用的结果。
/// </summary>
public sealed class SitemediaRunResult
{
    public bool Ok { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public IReadOnlyList<string> ArgumentList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RefreshUrlList { get; init; } = Array.Empty<string>();
    public int? ContentRefCount { get; init; }

    /// <summary>
    /// 给窗口与运行日志看的失败说明。
    /// </summary>
    public string FailureText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                return StdErr;
            }

            return string.IsNullOrWhiteSpace(StdOut) ? $"sitemedia 退出码 {ExitCode}。" : StdOut;
        }
    }
}
