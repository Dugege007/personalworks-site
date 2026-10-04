using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ContentPatchTests
{
    [Fact]
    public void TryReadMediaList_KeepsQuotedKindFrameAndItsTags()
    {
        var block = """
            media: [{"kind":"image","label":"DSC06877","src":"photo/real-world-photo/20250531 上海 龙美术馆/03.webp","tags":["111"]}, { kind: "image", label: "DSC06881", src: "photo/real-world-photo/20250531 上海 龙美术馆/04.webp" }],
            """;
        var list = ContentPatchPlanner.TryReadMediaList(block);
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Equal("photo/real-world-photo/20250531 上海 龙美术馆/03.webp", list[0].Src);
        Assert.Equal(new[] { "111" }, list[0].Tags);
        Assert.Equal("photo/real-world-photo/20250531 上海 龙美术馆/04.webp", list[1].Src);
        Assert.Empty(list[1].Tags);
    }

    [Fact]
    public void Planner_JsonHide_RelabelsRemainingAndKeepsObjectKey()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "demo-park",
            Channel = "demo-render",
            Object = "demo-render/demo-park/01.png"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("demo-park", changes[0].WorkId);
        Assert.Single(changes[0].AfterMediaList);
        Assert.Equal("demo-render/demo-park/02.png", changes[0].AfterMediaList[0].Src);
        Assert.Equal("效果图 01", changes[0].AfterMediaList[0].Label);
        Assert.DoesNotContain("demo-yard", changes.Select(item => item.WorkId));
    }

    [Fact]
    public void Planner_WorksTsAlbum_ConvertsToListedWithGapKeys()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "xiaowayao",
            Object = "landscape-rendering/xiaowayao/02.jpg"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("album", changes[0].KindBefore);
        Assert.Equal("listed", changes[0].KindAfter);
        Assert.Equal(2, changes[0].AfterMediaList.Count);
        Assert.Equal("landscape-rendering/xiaowayao/01.jpg", changes[0].AfterMediaList[0].Src);
        Assert.Equal("landscape-rendering/xiaowayao/03.jpg", changes[0].AfterMediaList[1].Src);
        Assert.Equal("效果图 01", changes[0].AfterMediaList[0].Label);
        Assert.Equal("效果图 02", changes[0].AfterMediaList[1].Label);
    }

    [Fact]
    public void Planner_WorksTsCovered_KeepsUnnumberedSlots()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-2",
            Channel = "landscape-photo",
            Object = "landscape-photo/sample-2/01.webp"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("landscape-photo", changes[0].Channel);
        Assert.Equal("covered", changes[0].KindBefore);
        Assert.Equal("images", changes[0].KindAfter);
        Assert.Equal(2, changes[0].AfterMediaList.Count);
        Assert.Equal("倒影", changes[0].AfterMediaList[0].Label);
        Assert.Equal("岸", changes[0].AfterMediaList[1].Label);
        Assert.Null(changes[0].AfterMediaList[0].Src);
        Assert.Contains("images(\"倒影\", \"岸\")", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain(changes, item => item.Channel == "digital-twin");
    }

    [Fact]
    public void Planner_WorksTsCoverArrayWithVideo_KeepsVideo()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-1",
            Channel = "digital-twin",
            Object = "digital-twin/sample-1/01.webp"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("digital-twin", changes[0].Channel);
        Assert.Equal("cover-array", changes[0].KindBefore);
        Assert.Equal("cover-array", changes[0].KindAfter);
        Assert.Equal(2, changes[0].AfterMediaList.Count);
        Assert.Equal("分层", changes[0].AfterMediaList[0].Label);
        Assert.Null(changes[0].AfterMediaList[0].Src);
        Assert.Equal("video", changes[0].AfterMediaList[1].Kind);
        Assert.Equal("漫游切片 · 静音点击播放", changes[0].AfterMediaList[1].Label);
        Assert.Contains("...images(\"分层\")", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("kind: \"video\"", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("digital-twin/sample-1/01.webp", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain(changes, item => item.Channel == "landscape-photo");
    }

    [Fact]
    public void Planner_WorksTsCoverArray_OnlyTouchesPhoto()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-1",
            Channel = "landscape-photo",
            Object = "landscape-photo/sample-1/01.webp"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("landscape-photo", changes[0].Channel);
        Assert.Equal("cover-array", changes[0].KindBefore);
        Assert.Equal("images", changes[0].KindAfter);
        Assert.Equal("云隙", changes[0].AfterMediaList[0].Label);
        Assert.DoesNotContain(changes, item => item.Channel == "digital-twin");
    }

    [Fact]
    public void Planner_PersonalWorks_CoveredSingle_ToEmpty()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);

        var hide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-3",
            Channel = "landscape-photo",
            Object = "landscape-photo/sample-3/01.webp"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { hide });
        Assert.Single(changes);
        Assert.Equal("landscape-photo", changes[0].Channel);
        Assert.Equal("covered", changes[0].KindBefore);
        Assert.Equal("empty", changes[0].KindAfter);
        Assert.Empty(changes[0].AfterMediaList);
        Assert.Contains("covered(", changes[0].BeforeSnippet, StringComparison.Ordinal);
        Assert.Equal("media: []", changes[0].AfterSnippet);
        Assert.DoesNotContain("other", changes[0].KindBefore, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_PersonalWorks_TwinAndLineSimHide_KeepsVideo()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);

        var twinHide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-1",
            Channel = "digital-twin",
            Object = "digital-twin/sample-1/01.webp"
        };
        var lineHide = new IntentItem
        {
            Intent = MediaIntentCodes.SiteHide,
            WorkId = "sample-1",
            Channel = "line-sim",
            Object = "line-sim/sample-1/01.webp"
        };

        var twinChanges = ContentPatchPlanner.Plan(session, new[] { twinHide });
        Assert.Single(twinChanges);
        Assert.Equal("cover-array", twinChanges[0].KindAfter);
        Assert.Contains("...images(\"分层\")", twinChanges[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("漫游切片 · 静音点击播放", twinChanges[0].AfterSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("digital-twin/sample-1/01.webp", twinChanges[0].AfterSnippet, StringComparison.Ordinal);

        var lineChanges = ContentPatchPlanner.Plan(session, new[] { lineHide });
        Assert.Single(lineChanges);
        Assert.Equal("cover-array", lineChanges[0].KindAfter);
        Assert.Contains("...images(\"节拍板\")", lineChanges[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("节拍回放 · 静音点击播放", lineChanges[0].AfterSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("line-sim/sample-1/01.webp", lineChanges[0].AfterSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_WorksTsIngestVideo_KeepsImagesAndWritesPoster()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var ingest = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "sample-2",
            Channel = "digital-twin",
            Object = "digital-twin/sample-2/03.mp4"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { ingest });
        Assert.Single(changes);
        Assert.Equal("digital-twin", changes[0].Channel);
        Assert.Equal("cover-array", changes[0].KindAfter);
        Assert.Equal(3, changes[0].AfterMediaList.Count);
        Assert.Equal("image", changes[0].AfterMediaList[0].Kind);
        Assert.Equal("digital-twin/sample-2/01.webp", changes[0].AfterMediaList[0].Src);
        Assert.Equal("video", changes[0].AfterMediaList[2].Kind);
        Assert.Equal("视频", changes[0].AfterMediaList[2].Label);
        Assert.Equal("digital-twin/sample-2/03.mp4", changes[0].AfterMediaList[2].Src);
        Assert.Equal("digital-twin/sample-2/03.poster.webp", changes[0].AfterMediaList[2].Poster);
        Assert.Contains("kind: \"video\"", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("digital-twin/sample-2/03.mp4", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("03.poster.webp", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("cover(\"digital-twin\", \"sample-2\"", changes[0].AfterSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_WorksTsIngestVideo_FillsEmptyVideoSlot()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var ingest = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "sample-3",
            Channel = "digital-twin",
            Object = "digital-twin/sample-3/02.mp4",
            SourceStageRel = "demo-render/demo-park/立库动画.mp4"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { ingest });
        Assert.Single(changes);
        Assert.Equal(1, changes[0].AfterMediaList.Count(item => item.Kind == "video"));
        var filled = changes[0].AfterMediaList.Last(item => item.Kind == "video");
        Assert.Equal("digital-twin/sample-3/02.mp4", filled.Src);
        Assert.Equal("演示", filled.Label);
        Assert.Contains("总图", changes[0].AfterSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_WorksTsIngestVideo_UsesSourceFileStemAsLabel()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var ingest = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "sample-2",
            Channel = "digital-twin",
            Object = "digital-twin/sample-2/03.mp4",
            SourceStageRel = "数字孪生（digital-twin）/上海曼盛包装/立库动画.mp4",
            StageRel = "数字孪生（digital-twin）/上海曼盛包装/.site-ready/shanghai-mansheng-packaging/09.mp4"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { ingest });
        Assert.Single(changes);
        var added = changes[0].AfterMediaList.Last(item => item.Kind == "video");
        Assert.Equal("立库动画", added.Label);
        Assert.Equal("digital-twin/sample-2/03.mp4", added.Src);
    }

    [Fact]
    public void Planner_DefaultVideoLabel_IgnoresPreparedRel()
    {
        Assert.Equal(
            "立库动画",
            ContentPatchPlanner.DefaultVideoLabel(
                "数字孪生（digital-twin）/上海曼盛包装/立库动画.mp4",
                "数字孪生（digital-twin）/上海曼盛包装/.site-ready/shanghai-mansheng-packaging/09.mp4"));
        Assert.Equal(
            "视频",
            ContentPatchPlanner.DefaultVideoLabel(
                "a/.site-ready/work/09.mp4",
                "a/.site-ready/work/09.mp4"));
    }

    [Fact]
    public void Planner_JsonIngest_AppendsAndRelabels()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var ingest = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "demo-park",
            Channel = "demo-render",
            Object = "demo-render/demo-park/03.png"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { ingest });
        Assert.Single(changes);
        Assert.Equal(3, changes[0].AfterMediaList.Count);
        Assert.Equal("demo-render/demo-park/03.png", changes[0].AfterMediaList[2].Src);
        Assert.Equal("效果图 03", changes[0].AfterMediaList[2].Label);
        Assert.Contains("demo-render/demo-park/03.png", changes[0].AddedObjectList);
    }

    [Fact]
    public void Planner_SiteRestore_AppendsExistingObject()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var restore = new IntentItem
        {
            Intent = MediaIntentCodes.SiteRestore,
            WorkId = "demo-park",
            Channel = "demo-render",
            Object = "demo-render/demo-park/03.png"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { restore });
        Assert.Single(changes);
        Assert.Equal(3, changes[0].AfterMediaList.Count);
        Assert.Equal("demo-render/demo-park/03.png", changes[0].AfterMediaList[2].Src);
        Assert.Contains("demo-render/demo-park/03.png", changes[0].AddedObjectList);
    }

    [Fact]
    public void Preview_RestoreHidden_WritesBackObject()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hidden = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "已隐藏样例",
            ObjectKey = "demo-render/demo-park/03.png",
            IsHidden = true,
            StageRel = "demo-render/demo-park/03.png"
        };
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[] { (MediaIntent.SiteRestore, (StageItem?)null, hidden) });
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], (StageItem?)null, hidden) },
            hidden.WorkId);

        Assert.False(report.HasHardError);
        Assert.True(report.Lines[0].Decision.Allowed);
        Assert.False(IntentGate.IsRedundantRestoreDecision(report.Lines[0].Decision));
        Assert.Equal(MediaIntentCodes.SiteRestore, document.Items[0].Intent);
        Assert.Contains("demo-render/demo-park/03.png", report.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("不新建键", report.Lines[0].Decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_DirectHide_IsAllowed()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var park = session.SiteItems.First(item => item.ObjectKey.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[] { (MediaIntent.SiteHide, (StageItem?)null, park) });
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], (StageItem?)null, park) },
            park.WorkId);
        Assert.False(report.HasHardError);
        Assert.Contains("内容层将改片段", report.PatchPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_AlreadyHiddenHide_DoesNotBlockBatch()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var park = session.SiteItems.First(item => item.ObjectKey.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        var hidden = new SiteItem
        {
            WorkId = park.WorkId,
            ChannelKey = park.ChannelKey,
            WorkTitle = park.WorkTitle,
            Label = "已隐藏样例",
            ObjectKey = "demo-render/demo-park/hidden.png",
            IsHidden = true,
            StageRel = "demo-render/demo-park/hidden.png"
        };
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[]
            {
                (MediaIntent.SiteHide, (StageItem?)null, hidden),
                (MediaIntent.SiteHide, (StageItem?)null, park)
            });
        var report = PreviewReporter.Build(
            session,
            document,
            new[]
            {
                (document.Items[0], (StageItem?)null, hidden),
                (document.Items[1], (StageItem?)null, park)
            },
            park.WorkId);

        Assert.False(report.HasHardError);
        Assert.True(IntentGate.IsRedundantHideDecision(report.Lines[0].Decision));
        Assert.True(report.Lines[1].Decision.Allowed);
        Assert.False(report.Lines[1].Decision.IsWarn);
        Assert.Contains("内容层将改片段", report.PatchPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden.png", report.PatchPreview, StringComparison.Ordinal);
        var preview = PromptRenderer.RenderPreview(report);
        Assert.Contains("[跳过]", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("无法执行", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_OnlyAlreadyHiddenHide_HasNoPatchAndNoHardError()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var hidden = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "已隐藏样例",
            ObjectKey = "demo-render/demo-park/hidden.png",
            IsHidden = true,
            StageRel = "demo-render/demo-park/hidden.png"
        };
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[] { (MediaIntent.SiteHide, (StageItem?)null, hidden) });
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], (StageItem?)null, hidden) },
            hidden.WorkId);

        Assert.False(report.HasHardError);
        Assert.True(IntentGate.IsRedundantHideDecision(report.Lines[0].Decision));
        Assert.True(string.IsNullOrWhiteSpace(report.PatchPreview));
    }

    [Fact]
    public void Planner_AlreadyPublishedIngest_DoesNotAppendGhostObject()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var published = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "demo-park",
            Channel = "demo-render",
            Object = "demo-render/demo-park/09.webp",
            StageRel = "demo-render/demo-park/01.png"
        };
        var unpublished = new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            WorkId = "demo-park",
            Channel = "demo-render",
            Object = "demo-render/demo-park/03.png",
            StageRel = "demo-render/demo-park/03.png"
        };

        var changes = ContentPatchPlanner.Plan(session, new[] { published, unpublished });
        Assert.Single(changes);
        Assert.DoesNotContain("09.webp", changes[0].AfterSnippet, StringComparison.Ordinal);
        Assert.Contains("demo-render/demo-park/03.png", changes[0].AddedObjectList);
        Assert.DoesNotContain("demo-render/demo-park/09.webp", changes[0].AddedObjectList);
    }

    [Fact]
    public void Preview_AlreadyPublishedIngest_DoesNotBlockBatch()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var published = session.StageItems.First(item =>
            item.StageRel == "demo-render/demo-park/01.png");
        var unpublished = session.StageItems.First(item =>
            item.StageRel == "demo-render/demo-park/03.png");
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Prompt,
            new[]
            {
                (MediaIntent.StageIngest, published, (SiteItem?)null),
                (MediaIntent.StageIngest, unpublished, (SiteItem?)null)
            },
            unpublished.WorkIdGuess ?? "demo-park");
        var report = PreviewReporter.Build(
            session,
            document,
            new[]
            {
                (document.Items[0], published, (SiteItem?)null),
                (document.Items[1], unpublished, (SiteItem?)null)
            },
            unpublished.WorkIdGuess ?? "demo-park");

        Assert.False(report.HasHardError);
        Assert.True(IntentGate.IsRedundantIngestDecision(report.Lines[0].Decision));
        Assert.True(report.Lines[1].Decision.Allowed);
        Assert.False(report.Lines[1].Decision.IsWarn);
        Assert.Contains("内容层将改片段", report.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("追加 1 条", report.PatchPreview, StringComparison.Ordinal);
        var preview = PromptRenderer.RenderPreview(report);
        Assert.Contains("[跳过]", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("无法执行", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_OnlyAlreadyPublishedIngest_HasNoPatchAndNoHardError()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var published = session.StageItems.First(item =>
            item.StageRel == "demo-render/demo-park/01.png");
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Prompt,
            new[] { (MediaIntent.StageIngest, published, (SiteItem?)null) },
            published.WorkIdGuess ?? "demo-park");
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], published, (SiteItem?)null) },
            published.WorkIdGuess ?? "demo-park");

        Assert.False(report.HasHardError);
        Assert.True(IntentGate.IsRedundantIngestDecision(report.Lines[0].Decision));
        Assert.True(string.IsNullOrWhiteSpace(report.PatchPreview));
    }

    [Fact]
    public void Preview_WithdrawSharedObject_RejectsWholeKey()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var fifteen = session.SiteItems.First(item =>
            item.WorkId == "huaian-fukang-15"
            && item.ObjectKey == "landscape-rendering/huaian-fukang/01.jpg");
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Prompt,
            new[]
            {
                (MediaIntent.SiteHide, (StageItem?)null, fifteen),
                (MediaIntent.SiteWithdraw, (StageItem?)null, fifteen)
            });
        var resolvedList = document.Items
            .Select(item => (item, (StageItem?)null, fifteen))
            .ToList();
        var report = PreviewReporter.Build(session, document, resolvedList, fifteen.WorkId);
        Assert.True(report.HasHardError);
        Assert.All(report.Lines, line => Assert.False(line.Decision.Allowed));
        Assert.Contains("整键拒绝", report.Lines[0].Decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_AlbumMiddleHide_KeepsGapAndRestoresOnFailure()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var original = File.ReadAllText(worksPath);
            var intentPath = Path.Combine(workDir, "intent.json");
            File.WriteAllText(intentPath, HideXiaowayaoMiddleIntent(workDir), JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            Assert.Contains("listed([", patched, StringComparison.Ordinal);
            Assert.Contains("landscape-rendering/xiaowayao/01.jpg", patched, StringComparison.Ordinal);
            Assert.Contains("landscape-rendering/xiaowayao/03.jpg", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("landscape-rendering/xiaowayao/02.jpg", patched, StringComparison.Ordinal);
            Assert.Contains("[\"landscape-rendering/xiaowayao/03.jpg\", \"效果图 02\"]", patched, StringComparison.Ordinal);
            Assert.Contains("huaian-fukang-15", patched, StringComparison.Ordinal);
            Assert.Contains("huaian-fukang-3", patched, StringComparison.Ordinal);

            File.WriteAllText(worksPath, original, JsonUtil.Utf8NoBom);
            var fail = RunPatch(script!, intentPath, profilePath, "--apply", "--fail-after-write");
            Assert.False(fail.Ok);
            Assert.Equal(original, File.ReadAllText(worksPath));
            Assert.True(File.Exists(intentPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ListedHide_OnlyTouchesTargetWork()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var original = File.ReadAllText(worksPath);
            var intentPath = Path.Combine(workDir, "intent-listed.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "huaian-fukang-15",
                      "channel": "landscape-rendering",
                      "object": "landscape-rendering/huaian-fukang/02.jpg"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            Assert.DoesNotContain("landscape-rendering/huaian-fukang/02.jpg", patched, StringComparison.Ordinal);
            Assert.Contains("[\"landscape-rendering/huaian-fukang/01.jpg\", \"效果图 01\"]", patched, StringComparison.Ordinal);
            Assert.Contains("[\"landscape-rendering/huaian-fukang-15/01.jpg\", \"效果图 02\"]", patched, StringComparison.Ordinal);
            Assert.Contains("landscape-rendering/huaian-fukang/01.jpg", ExtractWorkBlock(patched, "huaian-fukang-3"), StringComparison.Ordinal);

            var threeBlock = ExtractWorkBlock(patched, "huaian-fukang-3");
            var originalThree = ExtractWorkBlock(original, "huaian-fukang-3");
            Assert.Equal(Normalize(originalThree), Normalize(threeBlock));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_HideSharedObject_OnlyChangesCurrentWork()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalThree = ExtractWorkBlock(File.ReadAllText(worksPath), "huaian-fukang-3");
            var intentPath = Path.Combine(workDir, "intent-shared.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "huaian-fukang-15",
                      "object": "landscape-rendering/huaian-fukang/01.jpg"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            Assert.DoesNotContain(
                "landscape-rendering/huaian-fukang/01.jpg",
                ExtractWorkBlock(patched, "huaian-fukang-15"),
                StringComparison.Ordinal);
            Assert.Equal(Normalize(originalThree), Normalize(ExtractWorkBlock(patched, "huaian-fukang-3")));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_WithdrawSharedObject_RefusesAndKeepsFile()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var original = File.ReadAllText(worksPath);
            var intentPath = Path.Combine(workDir, "intent-withdraw.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.withdraw",
                      "workId": "huaian-fukang-15",
                      "object": "landscape-rendering/huaian-fukang/01.jpg"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.False(apply.Ok);
            Assert.Equal(original, File.ReadAllText(worksPath));
            Assert.Contains("仍被未纳入批次的作品引用", apply.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_CoveredHide_WritesImagesAndSkipsSameId()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalTwin = ExtractWorkBlock(File.ReadAllText(worksPath), "sample-1", "digital-twin");
            var intentPath = Path.Combine(workDir, "intent-photo-covered.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "sample-2",
                      "channel": "landscape-photo",
                      "object": "landscape-photo/sample-2/01.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var photoBlock = ExtractWorkBlock(patched, "sample-2", "landscape-photo");
            Assert.Contains("images(\"倒影\", \"岸\")", photoBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("landscape-photo/sample-2/01.webp", photoBlock, StringComparison.Ordinal);
            Assert.Equal(
                Normalize(originalTwin),
                Normalize(ExtractWorkBlock(patched, "sample-1", "digital-twin")));
            Assert.Contains("album(\"landscape-rendering\", \"xiaowayao\", 3)", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_CoverArrayHide_DoesNotTouchDigitalTwin()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalTwin = ExtractWorkBlock(File.ReadAllText(worksPath), "sample-1", "digital-twin");
            var intentPath = Path.Combine(workDir, "intent-photo-cover.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "sample-1",
                      "channel": "landscape-photo",
                      "object": "landscape-photo/sample-1/01.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var photoBlock = ExtractWorkBlock(patched, "sample-1", "landscape-photo");
            Assert.Contains("images(\"云隙\")", photoBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("landscape-photo/sample-1/01.webp", photoBlock, StringComparison.Ordinal);
            Assert.Equal(
                Normalize(originalTwin),
                Normalize(ExtractWorkBlock(patched, "sample-1", "digital-twin")));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_IngestVideo_AppendsToTwinListed()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalPhoto = ExtractWorkBlock(File.ReadAllText(worksPath), "sample-1", "landscape-photo");
            var intentPath = Path.Combine(workDir, "intent-twin-video-ingest.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "workId": "sample-2",
                      "channel": "digital-twin",
                      "object": "digital-twin/sample-2/03.mp4",
                      "sourceStageRel": "数字孪生（digital-twin）/上海曼盛包装/立库动画.mp4",
                      "stageRel": "数字孪生（digital-twin）/上海曼盛包装/.site-ready/shanghai-mansheng-packaging/09.mp4"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var twinBlock = ExtractWorkBlock(patched, "sample-2", "digital-twin");
            Assert.Contains("kind: \"video\"", twinBlock, StringComparison.Ordinal);
            Assert.Contains("label: \"立库动画\"", twinBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("label: \"视频\"", twinBlock, StringComparison.Ordinal);
            Assert.Contains("digital-twin/sample-2/03.mp4", twinBlock, StringComparison.Ordinal);
            Assert.Contains("03.poster.webp", twinBlock, StringComparison.Ordinal);
            Assert.Contains("cover(\"digital-twin\", \"sample-2\"", twinBlock, StringComparison.Ordinal);
            Assert.Equal(
                Normalize(originalPhoto),
                Normalize(ExtractWorkBlock(patched, "sample-1", "landscape-photo")));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_RegisteredIngest_KeepsFrameTags()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            File.WriteAllText(
                worksPath,
                """
                const registeredWorks = [
                  {
                    id: "miaozihu",
                    channel: "real-world-photo",
                    title: "庙子湖",
                    themes: ["portrait-photo"],
                    media: [
                      { kind: "image", label: "DSC04707", src: "photo/real-world-photo/miaozihu/01.webp", themes: ["landscape-photo"], tags: ["海景", "街头"], displayName: "礁石" },
                      {"kind":"image","label":"DSC04725","src":"photo/real-world-photo/miaozihu/02.webp","description":"近岸"},
                      { kind: "image", label: "DSC04728", src: "photo/real-world-photo/miaozihu/03.webp" }
                    ],
                  },
                  {
                    id: "other-shoot",
                    channel: "real-world-photo",
                    title: "另一处",
                    media: [{ kind: "image", label: "DSC00001", src: "photo/real-world-photo/other/01.webp", tags: ["夜景"] }],
                  }
                ];
                """,
                JsonUtil.Utf8NoBom);
            var intentPath = Path.Combine(workDir, "intent-keep-tags.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "workId": "miaozihu",
                      "channel": "real-world-photo",
                      "object": "photo/real-world-photo/miaozihu/04.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var block = ExtractWorkBlock(patched, "miaozihu", "real-world-photo");
            Assert.Contains("photo/real-world-photo/miaozihu/04.webp", block, StringComparison.Ordinal);
            Assert.Contains("themes: [\"landscape-photo\"]", block, StringComparison.Ordinal);
            Assert.Contains("tags: [\"海景\", \"街头\"]", block, StringComparison.Ordinal);
            Assert.Contains("displayName: \"礁石\"", block, StringComparison.Ordinal);
            Assert.Contains("description: \"近岸\"", block, StringComparison.Ordinal);
            Assert.Contains("themes: [\"portrait-photo\"]", block, StringComparison.Ordinal);
            Assert.Contains("photo/real-world-photo/miaozihu/03.webp", block, StringComparison.Ordinal);
            var added = ExtractMediaObject(block, "photo/real-world-photo/miaozihu/04.webp");
            Assert.DoesNotContain("tags:", added, StringComparison.Ordinal);
            Assert.DoesNotContain("themes:", added, StringComparison.Ordinal);
            var other = ExtractWorkBlock(patched, "other-shoot", "real-world-photo");
            Assert.Contains("tags: [\"夜景\"]", other, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_CoverArrayWithVideoHide_KeepsVideo()
    {
        var sourceProfile = ToolPaths.FindPatchCasesProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalPhoto = ExtractWorkBlock(File.ReadAllText(worksPath), "sample-1", "landscape-photo");
            var intentPath = Path.Combine(workDir, "intent-twin-video.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "sample-1",
                      "channel": "digital-twin",
                      "object": "digital-twin/sample-1/01.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var twinBlock = ExtractWorkBlock(patched, "sample-1", "digital-twin");
            Assert.Contains("...images(\"分层\")", twinBlock, StringComparison.Ordinal);
            Assert.Contains("kind: \"video\"", twinBlock, StringComparison.Ordinal);
            Assert.Contains("漫游切片 · 静音点击播放", twinBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("digital-twin/sample-1/01.webp", twinBlock, StringComparison.Ordinal);
            Assert.Equal(
                Normalize(originalPhoto),
                Normalize(ExtractWorkBlock(patched, "sample-1", "landscape-photo")));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ProfilePrimaryHide_SelectsFirstRemainingImage()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var sitePath = Path.Combine(workDir, "site.ts");
            var intentPath = Path.Combine(workDir, "intent-profile.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "portrait",
                      "channel": "profile",
                      "object": "profile/portrait.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(sitePath);
            Assert.Contains("portraitSrc: \"profile/02.webp\"", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("profile/portrait.webp", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ProfileSessionWorkId_IngestsIntoFlatPortraitSrcs()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归形象场次上页。");
        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var sitePath = Path.Combine(workDir, "site.ts");
            var original = File.ReadAllText(sitePath);
            var intentPath = Path.Combine(workDir, "intent-profile-session.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "workId": "session-a",
                      "channel": "profile",
                      "object": "profile/09.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(sitePath);
            Assert.Contains("portraitSrc: \"profile/portrait.webp\"", patched, StringComparison.Ordinal);
            Assert.Contains("profile/09.webp", patched, StringComparison.Ordinal);
            Assert.Contains("profile/02.webp", patched, StringComparison.Ordinal);
            Assert.Equal(1, patched.Split("portraitSrcs:").Length - 1);
            Assert.DoesNotContain("session-a", patched, StringComparison.Ordinal);
            Assert.NotEqual(original, patched);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_GameGallery_AddsAndRemovesWithinTargetProject()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var sitePath = Path.Combine(workDir, "site.ts");
            var intentPath = Path.Combine(workDir, "intent-game.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "ridge",
                      "channel": "game-dev",
                      "object": "game-dev/ridge/01.webp"
                    },
                    {
                      "intent": "stage.ingest",
                      "workId": "ridge",
                      "channel": "game-dev",
                      "object": "game-dev/ridge/03.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(sitePath);
            var ridgeBlock = ExtractSiteObjectBlock(patched, "ridge");
            Assert.Contains("coverSrc: \"game-dev/ridge/02.webp\"", ridgeBlock, StringComparison.Ordinal);
            Assert.Contains("game-dev/ridge/03.webp", ridgeBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("game-dev/ridge/01.webp", ridgeBlock, StringComparison.Ordinal);
            Assert.Contains("game-dev/line/01.webp", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_MultiSourceFailure_RestoresAllFiles()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = CopyPatchCases();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var sitePath = Path.Combine(workDir, "site.ts");
            var worksOriginal = File.ReadAllText(worksPath);
            var siteOriginal = File.ReadAllText(sitePath);
            var intentPath = Path.Combine(workDir, "intent-atomic.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "xiaowayao",
                      "channel": "landscape-rendering",
                      "object": "landscape-rendering/xiaowayao/02.jpg"
                    },
                    {
                      "intent": "site.hide",
                      "workId": "portrait",
                      "channel": "profile",
                      "object": "profile/portrait.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply", "--fail-after-write");

            Assert.False(apply.Ok);
            Assert.Equal(worksOriginal, File.ReadAllText(worksPath));
            Assert.Equal(siteOriginal, File.ReadAllText(sitePath));
            Assert.Contains("已还原内容层", apply.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_WithdrawMissingFrame_StripsOrphanExif()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var keys = WriteFireflySatelliteCase(workDir);
            var worksPath = Path.Combine(workDir, "works.ts");
            var exifPath = Path.Combine(workDir, "photoExif.ts");
            var sizesPath = Path.Combine(workDir, "photoSizes.ts");
            var worksBefore = File.ReadAllText(worksPath);
            var intentPath = Path.Combine(workDir, "intent-orphan-withdraw.json");
            File.WriteAllText(
                intentPath,
                JsonSerializer.Serialize(new
                {
                    version = 1,
                    mode = "direct",
                    items = new[]
                    {
                        new
                        {
                            intent = "site.withdraw",
                            workId = "firefly",
                            channel = "real-world-photo",
                            @object = keys.Orphan
                        }
                    }
                }, JsonUtil.Options),
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            Assert.Equal(worksBefore, File.ReadAllText(worksPath));
            var exif = File.ReadAllText(exifPath);
            var sizes = File.ReadAllText(sizesPath);
            Assert.DoesNotContain(keys.Orphan, exif, StringComparison.Ordinal);
            Assert.DoesNotContain(keys.Orphan, sizes, StringComparison.Ordinal);
            Assert.Contains(keys.Live, exif, StringComparison.Ordinal);
            Assert.Contains(keys.Other, exif, StringComparison.Ordinal);
            Assert.Contains(keys.Other, sizes, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_WorkWithdraw_StripsListedKeysMissingFromMedia()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        try
        {
            var keys = WriteFireflySatelliteCase(workDir);
            var exifPath = Path.Combine(workDir, "photoExif.ts");
            var intentPath = Path.Combine(workDir, "intent-work-withdraw.json");
            File.WriteAllText(
                intentPath,
                JsonSerializer.Serialize(new
                {
                    version = 1,
                    mode = "direct",
                    items = new[]
                    {
                        new
                        {
                            intent = "work.withdraw",
                            workId = "firefly",
                            channel = "real-world-photo",
                            objectList = new[] { keys.Orphan, keys.Live }
                        }
                    }
                }, JsonUtil.Options),
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");

            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            Assert.DoesNotContain("firefly", File.ReadAllText(Path.Combine(workDir, "works.ts")), StringComparison.Ordinal);
            var exif = File.ReadAllText(exifPath);
            Assert.DoesNotContain(keys.Orphan, exif, StringComparison.Ordinal);
            Assert.DoesNotContain(keys.Live, exif, StringComparison.Ordinal);
            Assert.Contains(keys.Other, exif, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_TidySatellites_StripsOrphanAndStopsWhenBodyRemains()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyPatchCases();
        var keysPath = Path.Combine(Path.GetTempPath(), "sms-tidy-keys-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var keys = WriteFireflySatelliteCase(workDir);
            var worksPath = Path.Combine(workDir, "works.ts");
            var worksBefore = File.ReadAllText(worksPath);
            File.WriteAllText(
                keysPath,
                JsonSerializer.Serialize(new[] { keys.Orphan, keys.Live }, JsonUtil.Options),
                JsonUtil.Utf8NoBom);

            var mixed = RunTidy(script!, Path.Combine(workDir, "profile.json"), keysPath, "--apply");

            Assert.False(mixed.Ok);
            Assert.Contains("works.ts", mixed.StdOut, StringComparison.Ordinal);
            Assert.Contains(keys.Live, mixed.StdOut, StringComparison.Ordinal);
            Assert.Equal(worksBefore, File.ReadAllText(worksPath));
            var exif = File.ReadAllText(Path.Combine(workDir, "photoExif.ts"));
            Assert.DoesNotContain(keys.Orphan, exif, StringComparison.Ordinal);
            Assert.Contains(keys.Live, exif, StringComparison.Ordinal);
            Assert.Contains(keys.Other, exif, StringComparison.Ordinal);

            File.WriteAllText(
                keysPath,
                JsonSerializer.Serialize(new[] { keys.Other }, JsonUtil.Options),
                JsonUtil.Utf8NoBom);
            var clean = RunTidy(script!, Path.Combine(workDir, "profile.json"), keysPath, "--apply");
            Assert.True(clean.Ok, clean.StdOut + clean.StdErr);
            Assert.Equal(worksBefore, File.ReadAllText(worksPath));
            var after = File.ReadAllText(Path.Combine(workDir, "photoExif.ts"));
            Assert.DoesNotContain(keys.Other, after, StringComparison.Ordinal);
            Assert.Contains(keys.Live, after, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
            try
            {
                File.Delete(keysPath);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// 隔离样例：正文只留 05，04 只出现在尺寸表和 Exif。
    /// </summary>
    private static (string Orphan, string Live, string Other) WriteFireflySatelliteCase(string workDir)
    {
        const string orphan = "photo/real-world-photo/20260618 上海 萤火虫基地/04.webp";
        const string live = "photo/real-world-photo/20260618 上海 萤火虫基地/05.webp";
        const string other = "photo/real-world-photo/other/01.webp";
        File.WriteAllText(
            Path.Combine(workDir, "works.ts"),
            """
            const registeredWorks = [
              {
                id: "firefly",
                channel: "real-world-photo",
                media: [
                  { kind: "image", label: "DSC02760", src: "photo/real-world-photo/20260618 上海 萤火虫基地/05.webp" },
                ],
              },
            ];
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "photoExif.ts"),
            """
            export const photoExif = {
              "photo/real-world-photo/20260618 上海 萤火虫基地/04.webp": { camera: "old" },
              "photo/real-world-photo/20260618 上海 萤火虫基地/05.webp": { camera: "keep" },
              "photo/real-world-photo/other/01.webp": { camera: "other" },
            };
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "photoSizes.ts"),
            """
            export const photoSizes = {
              "photo/real-world-photo/20260618 上海 萤火虫基地/04.webp": { width: 100, height: 200 },
              "photo/real-world-photo/other/01.webp": { width: 1, height: 2 },
            };
            """,
            JsonUtil.Utf8NoBom);
        return (orphan, live, other);
    }

    /// <summary>
    /// 把隔离样例拷到临时目录，避免改仓库内正本。
    /// </summary>
    private static string CopyPatchCases()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindPatchCasesProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        }

        return dest;
    }

    /// <summary>
    /// 隐藏小瓦窑中间一张的意图。
    /// </summary>
    private static string HideXiaowayaoMiddleIntent(string workspaceRoot)
    {
        return JsonSerializer.Serialize(new
        {
            version = 1,
            workspaceRoot,
            mode = "direct",
            items = new[]
            {
                new
                {
                    intent = "site.hide",
                    workId = "xiaowayao",
                    channel = "landscape-rendering",
                    @object = "landscape-rendering/xiaowayao/02.jpg",
                    labelBefore = "效果图 02"
                }
            },
            options = new { relabel = true, deploy = "none" }
        }, JsonUtil.Options);
    }

    /// <summary>
    /// 调用 content-patch.mjs。
    /// </summary>
    private static NodeRunResult RunPatch(string script, string intentPath, string profilePath, params string[] extraArgs)
    {
        var argumentList = new List<string>
        {
            "--intent",
            intentPath,
            "--profile",
            profilePath
        };
        argumentList.AddRange(extraArgs);
        return NodeHost.Run(script, argumentList, Path.GetDirectoryName(script)!);
    }

    /// <summary>
    /// 调用发布前的尺寸表 / Exif 整理。
    /// </summary>
    private static NodeRunResult RunTidy(string script, string profilePath, string keysPath, params string[] extraArgs)
    {
        var argumentList = new List<string>
        {
            "--tidy-satellites",
            "--profile",
            profilePath,
            "--keys",
            keysPath
        };
        argumentList.AddRange(extraArgs);
        return NodeHost.Run(script, argumentList, Path.GetDirectoryName(script)!);
    }

    /// <summary>
    /// 抽出单个作品块，便于对照未改作品。id 跨栏目重复时须带 channel。
    /// </summary>
    private static string ExtractWorkBlock(string text, string workId, string? channel = null)
    {
        var marker = "id: \"" + workId + "\"";
        var start = 0;
        while (true)
        {
            start = text.IndexOf(marker, start, StringComparison.Ordinal);
            Assert.True(start >= 0, "找不到作品 " + workId + (channel == null ? "" : "/" + channel));
            var next = text.IndexOf("id: \"", start + marker.Length, StringComparison.Ordinal);
            var block = next < 0 ? text[start..] : text[start..next];
            if (channel == null || block.Contains("channel: \"" + channel + "\"", StringComparison.Ordinal))
            {
                return block;
            }

            start += marker.Length;
        }
    }

    /// <summary>
    /// 抽出媒体数组里包含指定 src 的那一个对象。
    /// </summary>
    private static string ExtractMediaObject(string block, string src)
    {
        var marker = "src: \"" + src + "\"";
        var srcIndex = block.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(srcIndex >= 0, "找不到媒体 " + src);
        var start = block.LastIndexOf('{', srcIndex);
        var depth = 0;
        for (var i = start; i < block.Length; i++)
        {
            if (block[i] == '{')
            {
                depth++;
            }
            else if (block[i] == '}' && --depth == 0)
            {
                return block[start..(i + 1)];
            }
        }

        throw new InvalidDataException("媒体对象括号不配对。");
    }

    /// <summary>
    /// 抽出 site.ts 中带指定 id 的对象块。
    /// </summary>
    private static string ExtractSiteObjectBlock(string text, string workId)
    {
        var idMarker = "id: \"" + workId + "\"";
        var idIndex = text.IndexOf(idMarker, StringComparison.Ordinal);
        Assert.True(idIndex >= 0, "找不到站点级项目 " + workId);
        var start = text.LastIndexOf('{', idIndex);
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}' && --depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        throw new InvalidDataException("站点级项目对象括号不配对。");
    }

    /// <summary>
    /// 去掉空白差异后再比作品块。
    /// </summary>
    private static string Normalize(string text)
    {
        return string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd())).Trim();
    }

    /// <summary>
    /// 是否能启动 node。
    /// </summary>
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

    /// <summary>
    /// 尽量删掉临时目录。
    /// </summary>
    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
