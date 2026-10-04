using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CatalogNoticeTests
{
    [Theory]
    [InlineData("sheet.desense.jpg", true)]
    [InlineData("sheet.DESENSE.JPG", true)]
    [InlineData("sheet.webp", true)]
    [InlineData("sheet.jpg", false)]
    [InlineData("sheet.png", false)]
    [InlineData("sheet.desense.png", false)]
    public void PublicReady_OnlyDesenseJpgOrWebp(string name, bool expected)
    {
        Assert.Equal(expected, CatalogNotice.IsPublicReadyConstructionFile(name));
    }

    [Theory]
    [InlineData("landscape-cds", "sheet.jpg", true)]
    [InlineData("landscape-cds", "sheet.desense.jpg", false)]
    [InlineData("landscape-cds", "sheet.webp", false)]
    [InlineData("landscape-rendering", "sheet.jpg", false)]
    public void IsUnsigned_OnlyDesenseChannelsWithoutReadyFile(string channel, string name, bool expected)
    {
        Assert.Equal(expected, CatalogNotice.IsUnsigned(channel, name));
    }

    [Fact]
    public void NeedsChromeDim_UnsignedUsesSameRuleAsPublishedOrHidden()
    {
        Assert.True(CatalogNotice.NeedsChromeDim(isSite: false, isHidden: false, isPublished: false, isUnsigned: true));
        Assert.True(CatalogNotice.NeedsChromeDim(isSite: false, isHidden: false, isPublished: true, isUnsigned: false));
        Assert.True(CatalogNotice.NeedsChromeDim(isSite: true, isHidden: true, isPublished: false, isUnsigned: false));
        Assert.False(CatalogNotice.NeedsChromeDim(isSite: false, isHidden: false, isPublished: false, isUnsigned: false));
        Assert.False(CatalogNotice.NeedsChromeDim(isSite: true, isHidden: false, isPublished: true, isUnsigned: false));
    }

    [Fact]
    public void Ingest_UnsignedConstruction_IsRejected()
    {
        var temp = CreateTempImage(".jpg");
        var item = new StageItem
        {
            StageRel = "landscape-cds/sample-1/01.jpg",
            FullPath = temp,
            ChannelKey = "landscape-cds",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(MediaIntent.StageIngest, item, IngestContext());
        Assert.False(decision.Allowed);
        Assert.Contains("未脱敏", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_DesenseConstruction_IsAllowed()
    {
        var temp = CreateTempImage(".desense.jpg");
        var item = new StageItem
        {
            StageRel = "landscape-cds/sample-1/01.desense.jpg",
            FullPath = temp,
            ChannelKey = "landscape-cds",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(MediaIntent.StageIngest, item, IngestContext());
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Ingest_OtherChannelJpg_IsAllowed()
    {
        var temp = CreateTempImage(".jpg");
        var item = new StageItem
        {
            StageRel = "landscape-rendering/xiaowayao/01.jpg",
            FullPath = temp,
            ChannelKey = "landscape-rendering",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(MediaIntent.StageIngest, item, IngestContext());
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Recycle_UnsignedConstruction_IsAllowed()
    {
        var temp = CreateTempImage(".jpg");
        var item = new StageItem
        {
            StageRel = "landscape-cds/sample-1/01.jpg",
            FullPath = temp,
            ChannelKey = "landscape-cds",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageRecycle,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>()
            });
        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData("landscape-cds", "sheet.jpg", SenseFilterKind.All, true)]
    [InlineData("landscape-cds", "sheet.jpg", SenseFilterKind.Unsigned, true)]
    [InlineData("landscape-cds", "sheet.jpg", SenseFilterKind.Signed, false)]
    [InlineData("landscape-cds", "sheet.desense.jpg", SenseFilterKind.Unsigned, false)]
    [InlineData("landscape-cds", "sheet.desense.jpg", SenseFilterKind.Signed, true)]
    [InlineData("landscape-cds", "clip.webp", SenseFilterKind.Signed, true)]
    [InlineData("landscape-cds", "clip.mp4", SenseFilterKind.Unsigned, true)]
    [InlineData("landscape-cds", "clip.mp4", SenseFilterKind.Signed, false)]
    [InlineData("landscape-rendering", "sheet.jpg", SenseFilterKind.All, true)]
    [InlineData("landscape-rendering", "sheet.jpg", SenseFilterKind.Unsigned, false)]
    [InlineData("landscape-rendering", "sheet.jpg", SenseFilterKind.Signed, false)]
    public void SenseFilter_MatchesChannelAndFile(string channel, string name, SenseFilterKind filter, bool expected)
    {
        Assert.Equal(expected, CatalogNotice.MatchesSense(channel, name, filter));
    }

    [Fact]
    public void SenseFilter_CompareRowKeepsIfEitherSideMatches()
    {
        var unsigned = new StageItem
        {
            StageRel = "landscape-cds/sample-1/01.jpg",
            FullPath = "01.jpg",
            ChannelKey = "landscape-cds",
            IsStock = false
        };
        var signed = new SiteItem
        {
            WorkId = "sample-1",
            ChannelKey = "landscape-cds",
            WorkTitle = "总图选页",
            Label = "01",
            ObjectKey = "landscape-cds/sample-1/01.desense.jpg",
            Index = 0,
            Total = 1,
            IsStock = false
        };
        var row = new CompareRow
        {
            Kind = CompareRowKind.Paired,
            Stage = unsigned,
            Site = signed
        };
        Assert.True(CatalogNotice.MatchesSense(row, SenseFilterKind.Unsigned));
        Assert.True(CatalogNotice.MatchesSense(row, SenseFilterKind.Signed));
        Assert.False(CatalogNotice.MatchesSense(
            new CompareRow { Kind = CompareRowKind.StageOnly, Stage = unsigned },
            SenseFilterKind.Signed));
    }

    [Fact]
    public void Bar_CountsUnsignedStageAndEmptySlots()
    {
        var temp = CreateTempImage(".jpg");
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile { Name = "测" },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "sample-1",
                    Channel = "landscape-cds",
                    Title = "总图选页",
                    Media =
                    [
                        new WorkMediaItem { Label = "总图选页", Src = "landscape-cds/sample-1/01.webp" },
                        new WorkMediaItem { Label = "图框" },
                        new WorkMediaItem { Label = "索引" }
                    ]
                }
            ],
            StageItems =
            [
                new StageItem
                {
                    StageRel = "landscape-cds/sample-1/01.jpg",
                    FullPath = temp,
                    ChannelKey = "landscape-cds",
                    WorkIdGuess = "sample-1",
                    IsStock = false
                }
            ]
        };

        var bar = CatalogNotice.BuildBar(session, "landscape-cds", "sample-1");
        Assert.Contains("未脱敏 1 张", bar, StringComparison.Ordinal);
        Assert.Contains("占位槽 2 个", bar, StringComparison.Ordinal);
        Assert.Equal("", CatalogNotice.BuildBar(session, "landscape-rendering", null));
        var composed = CatalogNotice.ComposeBar(session, "landscape-cds", "sample-1", 3);
        Assert.Contains("未脱敏 3 张", composed, StringComparison.Ordinal);
        Assert.Contains("占位槽 2 个", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_LargeStageFile_HasNoSizeWarning()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var source = session.StageItems.First(item => !item.IsStock && !IntentGate.IsPublished(item.LedgerStatus));
        var oversized = new StageItem
        {
            StageRel = source.StageRel,
            FullPath = source.FullPath,
            ChannelKey = source.ChannelKey,
            WorkIdGuess = source.WorkIdGuess,
            Length = 20L * 1024 * 1024,
            LastWriteUtc = source.LastWriteUtc,
            MatchedObject = source.MatchedObject,
            LedgerStatus = source.LedgerStatus,
            IsStock = source.IsStock
        };
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Prompt,
            new[] { (MediaIntent.StageRecycle, oversized, (SiteItem?)null) });
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], oversized, (SiteItem?)null) },
            null);
        Assert.False(report.HasHardError);
        Assert.False(report.Lines[0].Decision.IsWarn);
        Assert.DoesNotContain("15MB", report.Lines[0].Decision.Message ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_HideWithoutStageRel_IsWarn()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var source = session.SiteItems.First();
        var orphan = new SiteItem
        {
            WorkId = source.WorkId,
            ChannelKey = source.ChannelKey,
            WorkTitle = source.WorkTitle,
            Label = source.Label,
            ObjectKey = source.ObjectKey,
            Index = source.Index,
            Total = source.Total,
            StageRel = null,
            FullPath = source.FullPath,
            LedgerStatus = source.LedgerStatus,
            ReferenceCount = source.ReferenceCount,
            IsStock = source.IsStock
        };
        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Prompt,
            new[] { (MediaIntent.SiteHide, (StageItem?)null, orphan) });
        var report = PreviewReporter.Build(
            session,
            document,
            new[] { (document.Items[0], (StageItem?)null, orphan) },
            orphan.WorkId);
        Assert.False(report.HasHardError);
        Assert.True(report.Lines[0].Decision.IsWarn);
        Assert.Contains("中转站缺失", report.Lines[0].Decision.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 写入最小图像供存在性检查。
    /// </summary>
    private static string CreateTempImage(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-" + Guid.NewGuid().ToString("N") + suffix);
        File.WriteAllBytes(path, MinimalPng);
        return path;
    }

    /// <summary>
    /// 已指定写入作品的上页上下文。
    /// </summary>
    private static IntentContext IngestContext()
    {
        return new IntentContext
        {
            LedgerDict = new Dictionary<string, LedgerRecord>(),
            ContentRefCountDict = new Dictionary<string, int>(),
            TargetWorkId = "sample-1"
        };
    }

    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };
}
