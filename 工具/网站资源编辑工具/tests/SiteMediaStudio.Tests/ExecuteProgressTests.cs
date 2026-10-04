using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ExecuteProgressTests
{
    [Fact]
    public void OrderIngest_SortsImageAudioVideoPack_KeepsStableOrder()
    {
        var session = PersonalSession(
            Pack("game-dev/demo/webgl"),
            Video("digital-twin/a.mp4"),
            Image("landscape-photo/02.jpg"),
            Image("landscape-photo/01.jpg"));
        var items = new[]
        {
            Ingest("digital-twin/a.mp4", "digital-twin/demo/08.mp4", "digital-twin"),
            Ingest("game-dev/demo/webgl", "game-dev/demo/webgl/", "game-dev"),
            Ingest("landscape-photo/02.jpg", "landscape-photo/demo/02.webp", "landscape-photo"),
            Ingest("bgm.mp3", "unused/01.mp3", "digital-twin"),
            Ingest("landscape-photo/01.jpg", "landscape-photo/demo/01.webp", "landscape-photo")
        };

        var ordered = ExecuteProgressPlanner.OrderIngestItems(session, items);
        Assert.Equal(
            new[]
            {
                "landscape-photo/02.jpg",
                "landscape-photo/01.jpg",
                "bgm.mp3",
                "digital-twin/a.mp4",
                "game-dev/demo/webgl"
            },
            ordered.Select(item => item.StageRel).ToArray());
    }

    [Fact]
    public void Classify_UsesPackDirectoryAndAudioExtension()
    {
        var session = PersonalSession();
        Assert.Equal(
            ExecuteMediaClass.Pack,
            ExecuteProgressPlanner.Classify(session, Ingest("game-dev/demo/webgl", "game-dev/demo/webgl/", "game-dev")));
        Assert.Equal(
            ExecuteMediaClass.Audio,
            ExecuteProgressPlanner.Classify(session, Ingest("bgm.wav", "unused/01.wav", "digital-twin")));
        Assert.Equal(
            ExecuteMediaClass.Video,
            ExecuteProgressPlanner.Classify(session, Ingest("clip.mov", "digital-twin/demo/08.mp4", "digital-twin")));
        Assert.Equal(
            ExecuteMediaClass.Image,
            ExecuteProgressPlanner.Classify(session, Ingest("shot.jpg", "landscape-photo/demo/01.webp", "landscape-photo")));
    }

    [Fact]
    public void Plan_ImageNeedsPrepare_HasCompressAndIngest()
    {
        var session = PersonalSession(Image("风光摄影（landscape-photo）/原图.jpg"));
        var document = Doc(Ingest(
            "风光摄影（landscape-photo）/原图.jpg",
            "landscape-photo/demo/01.webp",
            "landscape-photo"));
        var keys = ExecuteProgressPlanner.Plan(session, document).Select(step => step.Key).ToList();
        Assert.Equal(ExecuteProgressPlanner.PrepareImageKey, keys[0]);
        Assert.Equal(ExecuteProgressPlanner.IngestKey, keys[1]);
        Assert.Equal(ExecuteProgressPlanner.PatchKey, keys[2]);
        Assert.Equal(ExecuteProgressPlanner.FinishKey, keys[^1]);
        Assert.DoesNotContain(keys, key => key.StartsWith("encode-", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_Webp_SkipsCompress()
    {
        var session = PersonalSession(Image("风光摄影（landscape-photo）/.site-ready/demo/01.webp"));
        var document = Doc(Ingest(
            "风光摄影（landscape-photo）/.site-ready/demo/01.webp",
            "landscape-photo/demo/01.webp",
            "landscape-photo"));
        var keys = ExecuteProgressPlanner.Plan(session, document).Select(step => step.Key).ToList();
        Assert.DoesNotContain(ExecuteProgressPlanner.PrepareImageKey, keys);
        Assert.Contains(ExecuteProgressPlanner.IngestKey, keys);
    }

    [Fact]
    public void Plan_ConstructionNeedsPrepare_HasCompressAndIngest()
    {
        var session = PersonalSession(new StageItem
        {
            StageRel = "景观施工图（landscape-cds）/sample.desense.jpg",
            FullPath = "D:/tmp/sample.desense.jpg",
            ChannelKey = "landscape-cds"
        });
        var document = Doc(Ingest(
            "景观施工图（landscape-cds）/sample.desense.jpg",
            "landscape-cds/demo/01.webp",
            "landscape-cds"));
        var keys = ExecuteProgressPlanner.Plan(session, document).Select(step => step.Key).ToList();
        Assert.Equal(ExecuteProgressPlanner.PrepareImageKey, keys[0]);
        Assert.Equal(ExecuteProgressPlanner.IngestKey, keys[1]);
        Assert.Equal(ExecuteProgressPlanner.PatchKey, keys[2]);
    }

    [Fact]
    public void Plan_VideoNeedsEncode_HasEncodeIngestPoster()
    {
        var session = PersonalSession(Video("数字孪生（digital-twin）/clip.mov"));
        var document = Doc(Ingest(
            "数字孪生（digital-twin）/clip.mov",
            "digital-twin/demo/08.mp4",
            "digital-twin"));
        var steps = ExecuteProgressPlanner.Plan(session, document);
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Equal(ExecuteProgressPlanner.EncodeVideoKey, keys[0]);
        Assert.Equal(ExecuteProgressPlanner.IngestKey, keys[1]);
        Assert.DoesNotContain(ExecuteProgressPlanner.PosterKey, keys);
        Assert.Contains(ExecuteProgressPlanner.PatchKey, keys);
        var encode = steps.First(step => step.Key == ExecuteProgressPlanner.EncodeVideoKey);
        var totalWeight = steps.Sum(step => step.Weight);
        Assert.Equal(ExecuteProgressPlanner.EncodeVideoWeight, encode.Weight);
        Assert.True(encode.Weight * 10 >= totalWeight * 9);
    }

    [Fact]
    public void Plan_RecycleThenMixedIngest_RecycleFirst_ImagesBeforeVideo()
    {
        var session = PersonalSession(
            Image("landscape-photo/a.jpg"),
            Video("digital-twin/b.mp4"));
        var document = Doc(
            Ingest("digital-twin/b.mp4", "digital-twin/demo/08.mp4", "digital-twin"),
            Recycle("landscape-photo/old.jpg"),
            Ingest("landscape-photo/a.jpg", "landscape-photo/demo/01.webp", "landscape-photo"));
        var steps = ExecuteProgressPlanner.Plan(session, document);
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Equal(ExecuteProgressPlanner.RecycleKey, keys[0]);
        Assert.Contains(ExecuteProgressPlanner.PrepareImageKey, keys);
        Assert.Contains(ExecuteProgressPlanner.EncodeVideoKey, keys);
        Assert.True(keys.IndexOf(ExecuteProgressPlanner.PrepareImageKey)
            < keys.IndexOf(ExecuteProgressPlanner.EncodeVideoKey));
        Assert.Single(keys, key => key == ExecuteProgressPlanner.PrepareImageKey);
        Assert.Single(keys, key => key == ExecuteProgressPlanner.EncodeVideoKey);
        Assert.Single(keys, key => key == ExecuteProgressPlanner.IngestKey);
        Assert.Single(keys, key => key == ExecuteProgressPlanner.RecycleKey);
    }

    [Fact]
    public void Plan_TwoRecycles_SharesOneRecycleStep()
    {
        var document = Doc(
            Recycle("landscape-photo/old-a.jpg"),
            Recycle("landscape-photo/old-b.jpg"));
        var steps = ExecuteProgressPlanner.Plan(null, document);
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Single(keys, key => key == ExecuteProgressPlanner.RecycleKey);
        Assert.Equal(2, steps.First(step => step.Key == ExecuteProgressPlanner.RecycleKey).Weight);
    }

    [Fact]
    public void Tracker_ReportsIncreasingPercent()
    {
        var session = PersonalSession(Image("风光摄影（landscape-photo）/原图.jpg"));
        var document = Doc(Ingest(
            "风光摄影（landscape-photo）/原图.jpg",
            "landscape-photo/demo/01.webp",
            "landscape-photo"));
        var progress = new CollectingProgress();
        var tracker = new ExecuteProgressTracker(session, document, progress);
        tracker.Begin(ExecuteProgressPlanner.PrepareImageKey, "正在压图 原图.jpg");
        tracker.Begin(ExecuteProgressPlanner.IngestKey, "正在上页");
        tracker.Finish("本机执行完成。");

        Assert.NotEmpty(progress.SnapshotList);
        Assert.Equal("压图", progress.SnapshotList[0].Title);
        Assert.Equal(100, progress.SnapshotList[^1].Percent);
        Assert.Equal("本机执行完成。", progress.SnapshotList[^1].Detail);
        Assert.True(progress.SnapshotList[0].Percent < progress.SnapshotList[^1].Percent);
    }

    [Fact]
    public void Tracker_SetFraction_FillsCurrentStepWithoutLeavingItsShare()
    {
        var session = PersonalSession(Image("风光摄影（landscape-photo）/原图.jpg"));
        var document = Doc(Ingest(
            "风光摄影（landscape-photo）/原图.jpg",
            "landscape-photo/demo/01.webp",
            "landscape-photo"));
        var progress = new CollectingProgress();
        var tracker = new ExecuteProgressTracker(session, document, progress);
        tracker.Begin(ExecuteProgressPlanner.PrepareImageKey, "正在压图 原图.jpg");
        var afterBegin = progress.SnapshotList[^1];
        tracker.SetFraction(0.5, "质量 80");
        var mid = progress.SnapshotList[^1];
        tracker.Begin(ExecuteProgressPlanner.IngestKey, "正在上页");
        var next = progress.SnapshotList[^1];

        Assert.Equal(0, afterBegin.Percent);
        Assert.True(mid.Percent > afterBegin.Percent);
        Assert.True(mid.Percent < next.Percent);
        Assert.Equal("质量 80", mid.Detail);
        Assert.Equal("压图", mid.Title);
        var plan = ExecuteProgressPlanner.Plan(session, document);
        var prepareWeight = plan.First(step => step.Key == ExecuteProgressPlanner.PrepareImageKey).Weight;
        var totalWeight = plan.Sum(step => step.Weight);
        Assert.Equal(PublishProgressPlanner.ToPercent(0.5 * prepareWeight, totalWeight), mid.Percent);
        Assert.Equal(PublishProgressPlanner.ToPercent(prepareWeight, totalWeight), next.Percent);
    }

    [Fact]
    public void Plan_TwoImagesNeedingPrepare_SharesOnePrepareStep()
    {
        var session = PersonalSession(
            Image("风光摄影（landscape-photo）/甲.jpg"),
            Image("风光摄影（landscape-photo）/乙.jpg"));
        var document = Doc(
            Ingest("风光摄影（landscape-photo）/甲.jpg", "landscape-photo/demo/01.webp", "landscape-photo"),
            Ingest("风光摄影（landscape-photo）/乙.jpg", "landscape-photo/demo/02.webp", "landscape-photo"));
        var steps = ExecuteProgressPlanner.Plan(session, document);
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Single(keys, key => key == ExecuteProgressPlanner.PrepareImageKey);
        Assert.Equal(ExecuteProgressPlanner.PrepareImageWeight * 2, steps.First(step =>
            step.Key == ExecuteProgressPlanner.PrepareImageKey).Weight);
        Assert.Single(keys, key => key == ExecuteProgressPlanner.IngestKey);
        Assert.Equal(2, steps.First(step => step.Key == ExecuteProgressPlanner.IngestKey).Weight);
    }

    [Fact]
    public void Plan_AlreadyPublishedIngest_OmitsIngestSteps()
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
            ExecutionMode.Direct,
            new[]
            {
                (MediaIntent.StageIngest, published, (SiteItem?)null),
                (MediaIntent.StageIngest, unpublished, (SiteItem?)null)
            },
            "demo-park");
        var keys = ExecuteProgressPlanner.Plan(session, document).Select(step => step.Key).ToList();
        Assert.Single(keys, key => key == ExecuteProgressPlanner.IngestKey);
        Assert.Contains(ExecuteProgressPlanner.PatchKey, keys);
    }

    [Fact]
    public void NeedsPrepare_RejectsAudio()
    {
        var session = PersonalSession();
        var audio = new StageItem
        {
            StageRel = "digital-twin/bgm.mp3",
            FullPath = "D:/tmp/bgm.mp3",
            ChannelKey = "digital-twin"
        };
        Assert.True(MediaPathRules.IsAudioFile(audio.StageRel));
        Assert.False(WebpPrepareRules.NeedsPrepare(audio, session.Profile));
    }

    private static WorkspaceSession PersonalSession(params StageItem[] stageList)
    {
        return new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "test",
                SiteCatalog = new SiteCatalogConfig { Kind = "personalworks-ts" }
            },
            StageItems = stageList
        };
    }

    private static IntentDocument Doc(params IntentItem[] itemList)
    {
        return new IntentDocument
        {
            Version = 1,
            Options = new IntentOptions { Deploy = "none" },
            Items = itemList.ToList()
        };
    }

    private static IntentItem Ingest(string stageRel, string objectKey, string channel)
    {
        return new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            StageRel = stageRel,
            Object = objectKey,
            Channel = channel,
            WorkId = "demo"
        };
    }

    private static IntentItem Recycle(string stageRel)
    {
        return new IntentItem
        {
            Intent = MediaIntentCodes.StageRecycle,
            StageRel = stageRel
        };
    }

    private static StageItem Image(string stageRel)
    {
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = "D:/tmp/" + Path.GetFileName(stageRel),
            ChannelKey = "landscape-photo"
        };
    }

    private static StageItem Video(string stageRel)
    {
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = "D:/tmp/" + Path.GetFileName(stageRel),
            ChannelKey = "digital-twin"
        };
    }

    private static StageItem Pack(string stageRel)
    {
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = "D:/tmp/" + stageRel.Replace('/', Path.DirectorySeparatorChar),
            ChannelKey = "game-dev",
            IsPack = true
        };
    }

    /// <summary>
    /// 同步收集进度，避免测试线程没有同步上下文时漏报。
    /// </summary>
    private sealed class CollectingProgress : IProgress<PublishProgress>
    {
        public List<PublishProgress> SnapshotList { get; } = new();

        public void Report(PublishProgress value)
        {
            SnapshotList.Add(value);
        }
    }
}
