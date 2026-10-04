using System.Diagnostics;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PublishOrchestrationTests
{
    [Fact]
    public void Pending_FromDocument_IngestIsFull_HideIsSpa_RecycleIsNull()
    {
        var ingest = new IntentDocument
        {
            Items =
            {
                new IntentItem { Intent = MediaIntentCodes.StageIngest, Object = "demo-render/a/01.png" }
            }
        };
        var hide = new IntentDocument
        {
            Items =
            {
                new IntentItem { Intent = MediaIntentCodes.SiteHide, Object = "demo-render/a/02.png" }
            }
        };
        var recycle = new IntentDocument
        {
            Items =
            {
                new IntentItem { Intent = MediaIntentCodes.StageRecycle, StageRel = "demo-render/a/x.png" }
            }
        };
        var profile = new WorkspaceProfile { ProfilePath = @"D:\ws\profile.json", ResolvedRoot = @"D:\ws" };

        var ingestPending = PendingPublishStore.FromDocument(profile, ingest, new[] { "https://cdn.example/a/01.png" });
        Assert.NotNull(ingestPending);
        Assert.Equal("full", ingestPending!.Deploy);
        Assert.Contains("demo-render/a/01.png", ingestPending.IngestObjectList);

        var hidePending = PendingPublishStore.FromDocument(profile, hide, Array.Empty<string>());
        Assert.NotNull(hidePending);
        Assert.Equal("spa", hidePending!.Deploy);

        var restore = new IntentDocument
        {
            Items =
            {
                new IntentItem { Intent = MediaIntentCodes.SiteRestore, Object = "demo-render/a/02.png" }
            }
        };
        var restorePending = PendingPublishStore.FromDocument(profile, restore, Array.Empty<string>());
        Assert.NotNull(restorePending);
        Assert.Equal("spa", restorePending!.Deploy);
        Assert.Empty(restorePending.IngestObjectList);

        Assert.Null(PendingPublishStore.FromDocument(profile, recycle, Array.Empty<string>()));

        var copy = new IntentDocument
        {
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.CopyUpdate,
                    Target = "work",
                    Channel = "demo-render",
                    WorkId = "demo-park",
                    Description = "草稿说明。"
                }
            }
        };
        var copyPending = PendingPublishStore.FromDocument(profile, copy, Array.Empty<string>());
        Assert.NotNull(copyPending);
        Assert.Equal("spa", copyPending!.Deploy);
        Assert.True(copyPending.CopyOnly);
        Assert.Contains("仅文案", PendingPublishStore.StatusLine(profile, copyPending), StringComparison.Ordinal);
    }

    [Fact]
    public void Pending_Merge_KeepsFull_AndUnionsKeys()
    {
        var first = new PendingPublish
        {
            ProfilePath = @"D:\ws\profile.json",
            Deploy = "spa",
            WithdrawObjectList = { "a/01.png" },
            CreatedAt = "t0"
        };
        var second = new PendingPublish
        {
            ProfilePath = @"D:\ws\profile.json",
            Deploy = "full",
            IngestObjectList = { "a/09.png" }
        };
        var merged = PendingPublishStore.Merge(first, second);
        Assert.Equal("full", merged.Deploy);
        Assert.Contains("a/01.png", merged.WithdrawObjectList);
        Assert.Contains("a/09.png", merged.IngestObjectList);
        Assert.Equal("t0", merged.CreatedAt);
    }

    [Fact]
    public void PublishPreview_SpaAndFull_OmitPrune()
    {
        var profile = new WorkspaceProfile
        {
            ProfilePath = @"D:\ws\profile.json",
            ResolvedRoot = @"D:\ws",
            Cli = new CliConfig { DeployCwd = "PersonalSite" }
        };
        var spa = PublishExecutor.RenderPreview(profile, new PendingPublish
        {
            Deploy = "spa",
            WithdrawObjectList = { "a/01.png" }
        });
        var spaCommand = spa.Split('\n')[0];
        Assert.Contains("--spa", spaCommand, StringComparison.Ordinal);
        Assert.Contains("待撤下 1", spa, StringComparison.Ordinal);
        Assert.Contains("尺寸表和 Exif", spa, StringComparison.Ordinal);
        Assert.Contains("刷新 CDN 缓存", spa, StringComparison.Ordinal);
        Assert.Contains("不删 CDN 文件", spa, StringComparison.Ordinal);
        Assert.DoesNotContain("--prune-assets", spaCommand, StringComparison.OrdinalIgnoreCase);

        var full = PublishExecutor.RenderPreview(profile, new PendingPublish
        {
            Deploy = "full",
            IngestObjectList = { "a/09.png" }
        });
        var fullCommand = full.Split('\n')[0];
        Assert.DoesNotContain("--spa", fullCommand, StringComparison.Ordinal);
        Assert.Contains("待同步入库对象 1", full, StringComparison.Ordinal);
        Assert.DoesNotContain("--prune-assets", fullCommand, StringComparison.OrdinalIgnoreCase);
        var emptySpa = PublishExecutor.RenderPreview(profile, new PendingPublish { Deploy = "spa" });
        Assert.Contains("无待入库或待撤对象，将只发静态包。", emptySpa, StringComparison.Ordinal);
        Assert.False(PublishExecutor.ContainsPrune(new PendingPublish { Deploy = "full" }));
        Assert.False(PublishExecutor.CommandLineHasPrune(spa));
        Assert.False(PublishExecutor.CommandLineHasPrune(full));
        Assert.True(PublishExecutor.CommandLineHasPrune("将执行：node deploy.mjs --prune-assets\n工作目录：D:\\ws"));
    }

    [Fact]
    public void PublishResult_AllOk_SaysPublishedOnline()
    {
        var text = PublishExecutor.RenderResult(new BatchRunResult
        {
            Ok = true,
            CompletedStepList = ["已完整发布", "撤下 1/1", "已提交 CDN 刷新"],
            RefreshUrlList =
            [
                "https://cdn.duhongbo.com/digital-twin/a/08.mp4",
                "/placeholders/digital-twin/a/08.mp4"
            ]
        });
        Assert.Equal("已成功发布上线", text.Title);
        Assert.Contains("已成功发布上线。", text.Body, StringComparison.Ordinal);
        Assert.Contains("成功：", text.Body, StringComparison.Ordinal);
        Assert.Contains("- 已完整发布", text.Body, StringComparison.Ordinal);
        Assert.Contains("- 撤下 1/1", text.Body, StringComparison.Ordinal);
        Assert.Contains("- 已提交 CDN 刷新", text.Body, StringComparison.Ordinal);
        Assert.Contains("已提交刷新的地址1条", text.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("https://cdn.duhongbo.com/", text.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("/placeholders/", text.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("失败：", text.Body, StringComparison.Ordinal);
        Assert.Contains("日志已写入本机 SiteMediaStudio/run.log。", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishResult_WithTimings_ListsStepAndTotal()
    {
        var text = PublishExecutor.RenderResult(new BatchRunResult
        {
            Ok = true,
            CompletedStepList = ["已发静态包"],
            Elapsed = TimeSpan.FromSeconds(12.4),
            StepTimingList =
            [
                new CommandStepTiming { Title = "构建静态包", Elapsed = TimeSpan.FromSeconds(10) },
                new CommandStepTiming { Title = "收尾", Elapsed = TimeSpan.FromSeconds(0.3) }
            ]
        });
        Assert.Contains("各步耗时：", text.Body, StringComparison.Ordinal);
        Assert.Contains("构建静态包  10 秒", text.Body, StringComparison.Ordinal);
        Assert.Contains("合计 12 秒", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishResult_ListsResourcesAfterTimings()
    {
        var text = PublishExecutor.RenderResult(new BatchRunResult
        {
            Ok = true,
            CompletedStepList = ["已完整发布"],
            Elapsed = TimeSpan.FromSeconds(12),
            StepTimingList =
            [
                new CommandStepTiming { Title = "上传到服务器", Elapsed = TimeSpan.FromSeconds(6) }
            ],
            ResourceList =
            [
                CommandResourceResult.Of("01.webp", true),
                CommandResourceResult.Of("08.mp4", false)
            ]
        });
        var timingAt = text.Body.IndexOf("各步耗时：", StringComparison.Ordinal);
        var resourceAt = text.Body.IndexOf("资源：", StringComparison.Ordinal);
        Assert.True(resourceAt > timingAt);
        Assert.Contains("[成功] 01.webp", text.Body, StringComparison.Ordinal);
        Assert.Contains("[失败] 08.mp4", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishResult_PartialFail_ListsDoneAndFailed()
    {
        var text = PublishExecutor.RenderResult(new BatchRunResult
        {
            Ok = false,
            FailedStep = "CDN 刷新",
            FailureText = "UnauthorizedOperation.CdnHostUnauthorized",
            CompletedStepList = ["已完整发布", "撤下 1/1"]
        });
        Assert.Equal("发布失败，队列已保留", text.Title);
        Assert.Contains("成功：", text.Body, StringComparison.Ordinal);
        Assert.Contains("- 已完整发布", text.Body, StringComparison.Ordinal);
        Assert.Contains("失败：", text.Body, StringComparison.Ordinal);
        Assert.Contains("- CDN 刷新", text.Body, StringComparison.Ordinal);
        Assert.Contains("UnauthorizedOperation.CdnHostUnauthorized", text.Body, StringComparison.Ordinal);
        Assert.Contains("队列已保留", text.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("已成功发布上线", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PurgeArgs_ObjectAndHttpsUrl_SkipRelative()
    {
        var args = SitemediaClient.BuildPurgeArgs(
            new[] { "demo-render/a/01.png" },
            new[] { "https://cdn.example/a/01.png", "/placeholders/a/01.png" },
            dryRun: true);
        Assert.Equal("purge", args[0]);
        Assert.Contains("--dry-run", args);
        Assert.Contains("--object", args);
        Assert.Contains("demo-render/a/01.png", args);
        Assert.Contains("https://cdn.example/a/01.png", args);
        Assert.DoesNotContain("/placeholders/a/01.png", args);
        Assert.False(SitemediaClient.ContainsPrune(args));
        var batches = SitemediaClient.BuildPurgeBatches(
            new[] { "demo-render/a/01.png" },
            new[] { "https://cdn.example/a/01.png", "/placeholders/a/01.png" },
            dryRun: true);
        Assert.Single(batches);
        Assert.Equal(args, batches[0]);
    }

    [Fact]
    public void PurgeBatches_LongQueue_StaysUnderCommandBudget()
    {
        var objectList = Enumerable.Range(1, 406)
            .Select(i => $"landscape-cds/shimao-beijing-yidu/{i:000}.webp")
            .ToList();
        var urlList = objectList
            .Select(key => "https://cdn.duhongbo.com/" + key)
            .Concat(objectList.Select(key => "/placeholders/" + key))
            .ToList();
        var unbatched = new List<string> { "purge" };
        foreach (var key in objectList)
        {
            unbatched.Add("--object");
            unbatched.Add(key);
        }

        foreach (var url in urlList.Where(item => item.StartsWith("https://", StringComparison.Ordinal)))
        {
            unbatched.Add("--url");
            unbatched.Add(url);
        }

        Assert.True(SitemediaClient.EstimateCommandChars(unbatched) > SitemediaClient.PurgeCommandCharBudget);

        var batches = SitemediaClient.BuildPurgeBatches(objectList, urlList, dryRun: false);
        Assert.True(batches.Count >= 2);
        Assert.Equal(objectList.Count * 2, batches.Sum(batch =>
            batch.Count(item => item is "--object" or "--url")));
        foreach (var batch in batches)
        {
            Assert.Equal("purge", batch[0]);
            Assert.True(SitemediaClient.EstimateCommandChars(batch) <= SitemediaClient.PurgeCommandCharBudget);
            Assert.True(batch.Count(item => item is "--object" or "--url") <= SitemediaClient.PurgeUrlBatchLimit);
        }
    }

    [Fact]
    public void CanPublish_RequiresDeployEnv()
    {
        var profile = new WorkspaceProfile { ProfilePath = @"D:\ws\profile.json" };
        var pending = new PendingPublish
        {
            ProfilePath = profile.ProfilePath,
            Deploy = "spa"
        };
        Assert.False(PendingPublishStore.CanPublish(profile, pending));
        Assert.False(PendingPublishStore.CanPublish(profile, null));
    }

    [Fact]
    public void StatusLine_NoQueue_OtherWorkspace_HideOnly_NoEnv()
    {
        var profile = new WorkspaceProfile { ProfilePath = @"D:\ws\profile.json" };
        Assert.Equal(
            "无待发布队列。本工作区无 .env.deploy，发布会提示缺少凭证。",
            PendingPublishStore.StatusLine(profile, null));

        var other = new PendingPublish
        {
            ProfilePath = @"D:\other\profile.json",
            Deploy = "spa"
        };
        var otherLine = PendingPublishStore.StatusLine(profile, other);
        Assert.Contains("其它工作区", otherLine, StringComparison.Ordinal);
        Assert.Contains("无 .env.deploy", otherLine, StringComparison.Ordinal);
        Assert.DoesNotContain("可点", otherLine, StringComparison.Ordinal);

        var hideOnly = new PendingPublish
        {
            ProfilePath = profile.ProfilePath,
            Deploy = "spa"
        };
        var hideLine = PendingPublishStore.StatusLine(profile, hideOnly);
        Assert.Contains("待发布 spa", hideLine, StringComparison.Ordinal);
        Assert.Contains("仅隐藏", hideLine, StringComparison.Ordinal);
        Assert.Contains("无 .env.deploy", hideLine, StringComparison.Ordinal);
        Assert.DoesNotContain("可点", hideLine, StringComparison.Ordinal);

        var ingest = new PendingPublish
        {
            ProfilePath = profile.ProfilePath,
            Deploy = "full",
            IngestObjectList = { "a/01.png" },
            WithdrawObjectList = { "a/02.png" }
        };
        var ingestLine = PendingPublishStore.StatusLine(profile, ingest);
        Assert.Contains("待发布 full", ingestLine, StringComparison.Ordinal);
        Assert.Contains("入库 1", ingestLine, StringComparison.Ordinal);
        Assert.Contains("待撤 1", ingestLine, StringComparison.Ordinal);
        Assert.Contains("无 .env.deploy", ingestLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveForPublish_NoOrOtherQueue_ReturnsManualSpa()
    {
        var profile = new WorkspaceProfile
        {
            ProfilePath = @"D:\ws\profile.json",
            ResolvedRoot = @"D:\ws"
        };
        var spa = PendingPublishStore.ResolveForPublish(profile, null);
        Assert.Equal("spa", spa.Deploy);
        Assert.Equal(profile.ProfilePath, spa.ProfilePath);
        Assert.Empty(spa.IngestObjectList);
        Assert.Empty(spa.WithdrawObjectList);

        var other = new PendingPublish
        {
            ProfilePath = @"D:\other\profile.json",
            Deploy = "full",
            IngestObjectList = { "a/01.png" }
        };
        var resolved = PendingPublishStore.ResolveForPublish(profile, other);
        Assert.Equal("spa", resolved.Deploy);
        Assert.Equal(profile.ProfilePath, resolved.ProfilePath);
        Assert.Empty(resolved.IngestObjectList);

        var same = new PendingPublish
        {
            ProfilePath = profile.ProfilePath,
            Deploy = "full",
            IngestObjectList = { "a/09.png" }
        };
        var kept = PendingPublishStore.ResolveForPublish(profile, same);
        Assert.Equal("full", kept.Deploy);
        Assert.Contains("a/09.png", kept.IngestObjectList);
    }

    [Fact]
    public void ClearIfCurrent_OnlyDeletesSameWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-clear-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pending-publish.json");
        try
        {
            var current = new WorkspaceProfile { ProfilePath = @"D:\ws\profile.json" };
            File.WriteAllText(
                file,
                System.Text.Json.JsonSerializer.Serialize(
                    new PendingPublish { ProfilePath = @"D:\other\profile.json", Deploy = "spa" },
                    JsonUtil.Options),
                JsonUtil.Utf8NoBom);
            PendingPublishStore.ClearIfCurrent(current, file);
            Assert.True(File.Exists(file));

            File.WriteAllText(
                file,
                System.Text.Json.JsonSerializer.Serialize(
                    new PendingPublish { ProfilePath = current.ProfilePath, Deploy = "spa" },
                    JsonUtil.Options),
                JsonUtil.Utf8NoBom);
            PendingPublishStore.ClearIfCurrent(current, file);
            Assert.False(File.Exists(file));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void Purge_DryRun_PrintsUrl_WithoutTaskId()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 purge 预演。");
        var script = ToolPaths.FindSitemediaScript();
        Assert.NotNull(script);
        var run = NodeHost.Run(
            script!,
            new[] { "purge", "--dry-run", "--object", "demo-render/demo-park/01.png" },
            Path.GetDirectoryName(script!)!);
        Assert.True(run.Ok, run.StdErr + run.StdOut);
        Assert.Contains("待刷新 CDN", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("预演结束", run.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskId", run.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("prune-assets", run.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNode()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "-v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
