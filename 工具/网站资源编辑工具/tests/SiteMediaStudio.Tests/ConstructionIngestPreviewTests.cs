using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ConstructionIngestPreviewTests
{
    [Fact]
    public void Preview_ConstructionStageIngest_DoesNotThrow()
    {
        var session = LoadPersonalWorksOrSkip();
        if (session == null)
        {
            return;
        }

        var stageList = session.StageItems
            .Where(item => item.ChannelKey == CatalogNotice.ConstructionChannelKey)
            .Take(8)
            .ToList();
        Assert.True(stageList.Count > 0, "施工图投放箱应有可预览文件。");

        var target = session.Works.FirstOrDefault(item =>
            item.Channel == CatalogNotice.ConstructionChannelKey);
        if (target == null)
        {
            return;
        }
        var report = BuildIngestPreview(session, stageList, target.Id);
        Assert.NotNull(report);
        Assert.All(report.Lines, line => Assert.False(line.Decision.Allowed));
        var text = PromptRenderer.RenderPreview(report);
        Assert.DoesNotContain("未处理异常", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_ReadyFileIntoCoveredConstruction_RejectsWithoutThrow()
    {
        var session = LoadPersonalWorksOrSkip();
        if (session == null)
        {
            return;
        }
        if (!session.Works.Any(item =>
                item.Channel == "landscape-cds" && item.Id == "sample-1"))
        {
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), "sms-" + Guid.NewGuid().ToString("N") + ".webp");
        File.WriteAllBytes(temp, [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50]);
        var stage = new StageItem
        {
            StageRel = "landscape-cds/sample-1/ready.webp",
            FullPath = temp,
            ChannelKey = "landscape-cds",
            WorkIdGuess = "sample-1",
            IsStock = false
        };
        var report = BuildIngestPreview(session, new[] { stage }, "sample-1");
        Assert.False(report.Lines[0].Decision.Allowed);
        Assert.Contains("占位槽", report.Lines[0].Decision.Message, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(report.PatchPreview));
    }

    [Fact]
    public void Plan_IngestIntoCoveredConstruction_DoesNotThrow()
    {
        var session = LoadPersonalWorksOrSkip();
        if (session == null)
        {
            return;
        }

        var ingest = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "sample-1",
            Channel = "landscape-cds",
            Object = "landscape-cds/sample-1/02.desense.jpg"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { ingest });
        Assert.Empty(changes);
    }

    /// <summary>
    /// 加载真实 PersonalWorks；目录缺失则跳过。
    /// </summary>
    private static WorkspaceSession? LoadPersonalWorksOrSkip()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        if (profilePath == null || !File.Exists(profilePath))
        {
            return null;
        }

        try
        {
            return WorkspaceSession.Load(profilePath);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// 按窗口同一条链路组装施工图上页预演。
    /// </summary>
    private static PreviewReport BuildIngestPreview(
        WorkspaceSession session,
        IReadOnlyList<StageItem> stageList,
        string targetWorkId)
    {
        var markedList = stageList
            .Select(item => (MediaIntent.StageIngest, item, (SiteItem?)null))
            .ToList();
        var document = IntentDocumentBuilder.Build(session, ExecutionMode.Prompt, markedList, targetWorkId);
        var resolvedList = document.Items
            .Select(item =>
            {
                var stage = stageList.FirstOrDefault(candidate => candidate.StageRel == item.StageRel)
                    ?? session.StageItems.FirstOrDefault(candidate => candidate.StageRel == item.StageRel);
                return (item, stage, (SiteItem?)null);
            })
            .ToList();
        return PreviewReporter.Build(session, document, resolvedList, targetWorkId);
    }
}
