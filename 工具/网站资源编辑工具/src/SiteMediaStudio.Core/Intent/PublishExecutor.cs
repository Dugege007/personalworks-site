namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 发布上线：核对撤下引用 → deploy → 待撤 apply → purge。确认前由调用方预演。
/// </summary>
public static class PublishExecutor
{
    public const int DeployTimeoutMs = 900_000;

    /// <summary>
    /// 确认框用的只读说明。
    /// </summary>
    public static string RenderPreview(WorkspaceProfile profile, PendingPublish pending)
    {
        var script = PublishPaths.FindDeployScript(profile) ?? "scripts/deploy.mjs";
        var args = string.Equals(pending.Deploy, "spa", StringComparison.OrdinalIgnoreCase)
            ? " --spa"
            : "";
        var lines = new List<string>
        {
            $"将执行：node {Path.GetFileName(script)}{args}",
            "工作目录：" + (PublishPaths.FindDeployCwd(profile) ?? profile.ResolvedRoot)
        };
        if (pending.IngestObjectList.Count > 0)
        {
            lines.Add($"待同步入库对象 {pending.IngestObjectList.Count} 个。");
        }

        if (pending.WithdrawObjectList.Count > 0)
        {
            lines.Add($"待撤下 {pending.WithdrawObjectList.Count} 个对象（先发静态包，再删正式位与 COS；目录已空则删空目录）。");
            lines.Add("发布前先核对撤下引用。正文已经不再引用的待撤键，会从尺寸表和 Exif 中去掉。正文仍引用的，停在发布前，正式文件保留。");
        }

        if (pending.IngestObjectList.Count == 0 && pending.WithdrawObjectList.Count == 0)
        {
            lines.Add("无待入库或待撤对象，将只发静态包。");
        }

        var purgeCount = PendingPublishStore.DistinctPreserve(
            pending.PurgeUrlList.Concat(pending.IngestObjectList).Concat(pending.WithdrawObjectList)).Count;
        lines.Add($"随后刷新 CDN 缓存（约 {purgeCount} 条），只失效缓存，不删 CDN 文件。");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 发布结束后的窗口标题与正文：成功写「已成功发布上线」，失败列出已完成步与失败步。
    /// </summary>
    public static PublishResultText RenderResult(BatchRunResult result)
    {
        var lines = new List<string>();
        if (result.Ok)
        {
            lines.Add("已成功发布上线。");
            AppendCompleted(lines, result);
            AppendRefreshUrls(lines, result);
        AppendTimings(lines, result);
        CommandResourceResult.AppendBlock(lines, result.ResourceList);
        lines.Add("");
        lines.Add("日志已写入本机 SiteMediaStudio/run.log。");
            return new PublishResultText
            {
                Title = "已成功发布上线",
                Body = string.Join(Environment.NewLine, lines)
            };
        }

        if (result.CompletedStepList.Count == 0)
        {
            lines.Add("尚未完成任何步。");
        }
        else
        {
            AppendCompleted(lines, result);
        }

        lines.Add("");
        lines.Add("失败：");
        lines.Add("- " + (string.IsNullOrWhiteSpace(result.FailedStep) ? "未知步骤" : result.FailedStep));
        if (!string.IsNullOrWhiteSpace(result.FailureText))
        {
            lines.Add("");
            lines.Add(result.FailureText.Trim());
        }

        lines.Add("");
        lines.Add("队列已保留，可查明原因后再次发布上线。");
        AppendTimings(lines, result);
        CommandResourceResult.AppendBlock(lines, result.ResourceList);
        lines.Add("");
        lines.Add("运行日志见本机 SiteMediaStudio/run.log。");
        return new PublishResultText
        {
            Title = "发布失败，队列已保留",
            Body = string.Join(Environment.NewLine, lines)
        };
    }

    /// <summary>
    /// 写出已完成步。
    /// </summary>
    private static void AppendCompleted(List<string> lines, BatchRunResult result)
    {
        if (result.CompletedStepList.Count == 0)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add("");
        }

        lines.Add("成功：");
        foreach (var step in result.CompletedStepList)
        {
            lines.Add("- " + step);
        }
    }

    /// <summary>
    /// 只写已提交刷新的 http(s) 条数，不逐条列出地址。
    /// </summary>
    private static void AppendRefreshUrls(List<string> lines, BatchRunResult result)
    {
        var count = result.RefreshUrlList
            .Where(url =>
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (count == 0)
        {
            return;
        }

        lines.Add("");
        lines.Add("已提交刷新的地址" + count + "条");
    }

    /// <summary>
    /// 写出各步耗时与合计。
    /// </summary>
    private static void AppendTimings(List<string> lines, BatchRunResult result)
    {
        CommandStepTiming.AppendBlock(lines, result.StepTimingList, result.Elapsed);
    }

    /// <summary>
    /// 按队列发布。任一步失败则停，不清队列。进度按命令步回报，不定时假推进。
    /// </summary>
    public static BatchRunResult Run(
        WorkspaceProfile profile,
        PendingPublish pending,
        IProgress<PublishProgress>? progress = null)
    {
        var tracker = new PublishProgressTracker(pending, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "正在校验发布队列。");
        if (!PendingPublishStore.HasDeployReady(profile))
        {
            return FailTracked(tracker, "发布", "本工作区缺少 .env.deploy。");
        }

        if (!PendingPublishStore.BelongsToWorkspace(profile, pending)
            || !PendingPublishStore.HasValidDeployKind(pending))
        {
            return FailTracked(tracker, "发布", "发布队列不属于当前工作区，或部署方式无效。");
        }

        if (ContainsPrune(pending))
        {
            return FailTracked(tracker, "发布", "队列或参数不得包含 prune-assets。");
        }

        var completedList = new List<string>();
        var resourceList = new List<CommandResourceResult>();
        var refreshUrlList = new List<string>(pending.PurgeUrlList);
        var spa = string.Equals(pending.Deploy, "spa", StringComparison.OrdinalIgnoreCase);

        if (pending.WithdrawObjectList.Count > 0)
        {
            tracker.Begin(PublishProgressPlanner.TidyKey, "正在核对撤下引用。");
            ContentPatchRunResult tidy;
            try
            {
                if (string.IsNullOrWhiteSpace(profile.ProfilePath))
                {
                    throw new InvalidOperationException("工作区配置路径为空，无法核对撤下引用。");
                }

                tidy = ContentPatchClient.TidySatellites(profile.ProfilePath, pending.WithdrawObjectList);
            }
            catch (Exception ex)
            {
                RunLog.Append("核对撤下引用", false, ex.Message);
                return FailTracked(tracker, "核对撤下引用", ex.Message, completedList, resourceList: resourceList);
            }

            RunLog.Append("核对撤下引用", tidy.Ok, tidy.StdOut + tidy.StdErr);
            if (!tidy.Ok)
            {
                return FailTracked(tracker, "核对撤下引用", tidy.FailureText, completedList, resourceList: resourceList);
            }

            var strippedCount = tidy.Report?.Stripped.Count ?? 0;
            completedList.Add(strippedCount > 0
                ? $"已从尺寸表和 Exif 去掉 {strippedCount} 个正文不再引用的旧键"
                : "待撤键的尺寸表和 Exif 无需整理");
        }

        var deployScript = PublishPaths.FindDeployScript(profile)!;
        var deployCwd = PublishPaths.FindDeployCwd(profile)!;
        var deployArgs = spa ? new[] { "--spa" } : Array.Empty<string>();
        tracker.Begin(PublishProgressPlanner.BuildKey, spa ? "正在发布静态包。" : "正在完整发布。");
        NodeRunResult deploy;
        try
        {
            deploy = NodeHost.Run(
                deployScript,
                deployArgs,
                deployCwd,
                DeployTimeoutMs,
                onOutputLine: line => tracker.OnDeployLine(line, spa));
        }
        catch (Exception ex)
        {
            RunLog.Append("发布 deploy", false, ex.Message);
            return FailTracked(tracker, "发布", ex.Message, completedList, resourceList: resourceList);
        }

        RunLog.Append("发布 deploy", deploy.Ok, deploy.StdOut + deploy.StdErr);
        if (!deploy.Ok)
        {
            foreach (var objectKey in pending.IngestObjectList)
            {
                resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), false));
            }

            return FailTracked(
                tracker,
                "发布",
                deploy.StdErr.Length > 0 ? deploy.StdErr : deploy.StdOut,
                completedList,
                resourceList: resourceList);
        }

        completedList.Add(spa ? "已发静态包" : "已完整发布");
        foreach (var objectKey in pending.IngestObjectList)
        {
            resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), true));
        }

        var withdrawOk = 0;
        if (pending.WithdrawObjectList.Count > 0)
        {
            tracker.Begin(PublishProgressPlanner.WithdrawKey, "正在撤下。");
        }

        for (var i = 0; i < pending.WithdrawObjectList.Count; i++)
        {
            var objectKey = pending.WithdrawObjectList[i];
            tracker.SetBatchFraction(
                i,
                pending.WithdrawObjectList.Count,
                0,
                "预演撤下 " + objectKey);
            SitemediaRunResult dryRun;
            try
            {
                dryRun = SitemediaClient.Withdraw(profile, objectKey, apply: false);
            }
            catch (Exception ex)
            {
                resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), false));
                RunLog.Append("撤下预演 " + objectKey, false, ex.Message);
                return FailTracked(tracker, "撤下预演", ex.Message, completedList, withdrawOk, resourceList);
            }

            RunLog.Append("撤下预演 " + objectKey, dryRun.Ok, dryRun.StdOut);
            if (!dryRun.Ok)
            {
                resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), false));
                return FailTracked(tracker, "撤下预演", dryRun.FailureText, completedList, withdrawOk, resourceList);
            }

            if (dryRun.ContentRefCount is null or > 0)
            {
                var reason = dryRun.ContentRefCount > 0
                    ? $"内容层仍引用该对象（{dryRun.ContentRefCount} 处），未调用 withdraw --apply。"
                    : "撤下预演未给出引用计数，未调用 withdraw --apply。";
                resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), false));
                return FailTracked(
                    tracker,
                    "撤下",
                    reason + Environment.NewLine + dryRun.StdOut,
                    completedList,
                    withdrawOk,
                    resourceList);
            }

            var withdrawName = Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar));
            tracker.SetDetail("执行撤下 " + objectKey);
            SitemediaRunResult apply;
            try
            {
                apply = SitemediaClient.Withdraw(
                    profile,
                    objectKey,
                    apply: true,
                    onOutputLine: line =>
                    {
                        var retries = SitemediaClient.ParseLedgerRenameAttempt(line);
                        if (retries > 0)
                        {
                            tracker.NoteLedgerRenameAttempt(withdrawName, retries);
                        }
                    });
            }
            catch (Exception ex)
            {
                tracker.CommitLedgerRenameRetries(withdrawName, 0);
                resourceList.Add(CommandResourceResult.Of(withdrawName, false));
                RunLog.Append("撤下 " + objectKey, false, ex.Message);
                return FailTracked(tracker, "撤下", ex.Message, completedList, withdrawOk, resourceList);
            }

            RunLog.Append("撤下 " + objectKey, apply.Ok, apply.StdOut + apply.StdErr);
            tracker.CommitLedgerRenameRetries(withdrawName, SitemediaClient.ParseLedgerRenameRetries(apply.StdOut));
            if (!apply.Ok)
            {
                resourceList.Add(CommandResourceResult.Of(Path.GetFileName(objectKey.Replace('/', Path.DirectorySeparatorChar)), false));
                return FailTracked(tracker, "撤下", apply.FailureText, completedList, withdrawOk, resourceList);
            }

            resourceList.Add(CommandResourceResult.Of(
                withdrawName,
                true,
                SitemediaClient.ParseLedgerRenameRetries(apply.StdOut)));
            withdrawOk++;
            refreshUrlList.AddRange(apply.RefreshUrlList);
            tracker.CompleteCurrent();
        }

        if (pending.WithdrawObjectList.Count > 0)
        {
            completedList.Add($"撤下 {withdrawOk}/{pending.WithdrawObjectList.Count}");
        }

        var purgeObjectList = pending.IngestObjectList.Concat(pending.WithdrawObjectList).ToList();
        var purgeUrlList = PendingPublishStore.DistinctPreserve(refreshUrlList)
            .Where(url =>
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (purgeObjectList.Count > 0 || purgeUrlList.Count > 0)
        {
            tracker.Begin(PublishProgressPlanner.PurgeKey, "正在提交 CDN 刷新。");
            SitemediaRunResult purge;
            try
            {
                purge = SitemediaClient.Purge(profile, purgeObjectList, purgeUrlList, dryRun: false);
            }
            catch (Exception ex)
            {
                RunLog.Append("CDN 刷新", false, ex.Message);
                return FailTracked(tracker, "CDN 刷新", ex.Message, completedList, withdrawOk, resourceList);
            }

            RunLog.Append("CDN 刷新", purge.Ok, purge.StdOut + purge.StdErr);
            if (!purge.Ok)
            {
                return FailTracked(tracker, "CDN 刷新", purge.FailureText, completedList, withdrawOk, resourceList);
            }

            completedList.Add("已提交 CDN 刷新");
        }
        else
        {
            completedList.Add("无 CDN 刷新（仅页面）");
        }

        tracker.Begin(PublishProgressPlanner.FinishKey, "正在清空待发布队列。");
        PendingPublishStore.ClearIfCurrent(profile);
        tracker.Finish("发布上线完成。");
        return tracker.Stamp(new BatchRunResult
        {
            Ok = true,
            CompletedStepList = completedList,
            WithdrawOk = withdrawOk,
            RefreshUrlList = PendingPublishStore.DistinctPreserve(refreshUrlList),
            ResourceList = resourceList
        }, "发布");
    }

    /// <summary>
    /// 队列部署方式与刷新 URL 不得含 prune。
    /// </summary>
    public static bool ContainsPrune(PendingPublish pending)
    {
        return pending.Deploy.Contains("prune", StringComparison.OrdinalIgnoreCase)
            || pending.PurgeUrlList.Any(url => url.Contains("prune-assets", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 只看将执行的命令行是否带 --prune-assets。说明里的「禁止」句不算。
    /// </summary>
    public static bool CommandLineHasPrune(string preview)
    {
        var firstLine = preview.Split('\n')[0];
        return firstLine.Contains("--prune-assets", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 组装失败结果。
    /// </summary>
    private static BatchRunResult Fail(
        string step,
        string text,
        IReadOnlyList<string>? completedList = null,
        int withdrawOk = 0,
        IReadOnlyList<CommandResourceResult>? resourceList = null)
    {
        return new BatchRunResult
        {
            Ok = false,
            FailedStep = step,
            FailureText = text,
            CompletedStepList = completedList ?? Array.Empty<string>(),
            WithdrawOk = withdrawOk,
            ResourceList = resourceList ?? Array.Empty<CommandResourceResult>()
        };
    }

    /// <summary>
    /// 失败时先回写进度，再组装结果。
    /// </summary>
    private static BatchRunResult FailTracked(
        PublishProgressTracker tracker,
        string step,
        string text,
        IReadOnlyList<string>? completedList = null,
        int withdrawOk = 0,
        IReadOnlyList<CommandResourceResult>? resourceList = null)
    {
        tracker.Fail(text);
        return tracker.Stamp(Fail(step, text, completedList, withdrawOk, resourceList), "发布");
    }
}

/// <summary>
/// 发布结束后弹窗用的标题与正文。
/// </summary>
public sealed class PublishResultText
{
    public required string Title { get; init; }
    public required string Body { get; init; }
}
