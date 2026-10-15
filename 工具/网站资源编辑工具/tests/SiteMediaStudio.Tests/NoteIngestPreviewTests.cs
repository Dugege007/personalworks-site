using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 界面「预演」走 IntentDocumentBuilder、PreviewReporter 与 PromptRenderer，不走 NotePublisher。
/// </summary>
public sealed class NoteIngestPreviewTests
{
    [Fact]
    public void Preview_RejectsGifInFolder_AndListsPngNotBodyWebp()
    {
        using var fixture = new NotePreviewFixture("29990101_000002");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile("anim.gif");

        var report = fixture.PreviewMarkedBody();
        var text = PromptRenderer.RenderPreview(report);
        var bullets = Bullets(text);

        Assert.True(report.HasHardError);
        Assert.Contains("[拒绝]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[允许]", text, StringComparison.Ordinal);
        Assert.Contains(NoteRules.GifPublishRejectedMessage, text, StringComparison.Ordinal);
        var rejected = bullets.Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();
        Assert.Single(rejected);
        Assert.Contains("anim.gif", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ok.png", rejected[0], StringComparison.Ordinal);
        Assert.Contains("这个文件导致整篇被拒绝", text, StringComparison.Ordinal);
        Assert.Contains("ok.png 本身没问题，因整篇被拒绝而不会上页。", text, StringComparison.Ordinal);
        Assert.DoesNotContain(bullets, line => line.Contains("正文", StringComparison.Ordinal));
        Assert.DoesNotContain("正文.webp", text, StringComparison.Ordinal);
        Assert.Contains(report.Document.Items, item => item.StageRel != null && item.StageRel.EndsWith("正文.md", StringComparison.Ordinal));
        Assert.All(report.Document.Items, item => Assert.DoesNotContain("正文.webp", item.Object ?? "", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_AllowsPublishablePng_AndDoesNotTreatBodyAsImage()
    {
        using var fixture = new NotePreviewFixture("29990101_000001");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");

        var report = fixture.PreviewMarkedBody();
        var text = PromptRenderer.RenderPreview(report);
        var bullets = Bullets(text);

        Assert.False(report.HasHardError, text);
        Assert.Contains("[允许]", text, StringComparison.Ordinal);
        Assert.Contains(bullets, line => line.Contains("ok.png", StringComparison.Ordinal));
        Assert.DoesNotContain(bullets, line => line.Contains("正文", StringComparison.Ordinal));
        Assert.DoesNotContain("正文.webp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("「一篇」", text, StringComparison.Ordinal);
        Assert.Contains(report.Lines, line => line.Item.StageRel != null && line.Item.StageRel.EndsWith("ok.png", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_RejectsNestedGif_NamesGifAndKeepsOtherImagesInnocent()
    {
        using var fixture = new NotePreviewFixture("29990101_000005");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile("图/b.jpg");
        fixture.WriteFile("图/anim.gif");

        var report = fixture.PreviewMarkedBody();
        var text = PromptRenderer.RenderPreview(report);
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.True(report.HasHardError);
        Assert.Single(rejected);
        Assert.EndsWith("图/anim.gif", rejected[0].TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(" anim.gif", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ok.png", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("b.jpg", rejected[0], StringComparison.Ordinal);
        Assert.EndsWith("图/anim.gif", report.Lines[0].Item.StageRel, StringComparison.Ordinal);
        Assert.Equal(
            "GIF 配图暂不支持上页：图/anim.gif。这个文件导致整篇被拒绝。ok.png、图/b.jpg 本身没问题，因整篇被拒绝而不会上页。",
            report.Lines[0].Decision.Message);
    }

    [Fact]
    public void Preview_RejectsGifInSubfolder_ShowsRelativePathOnTheRejectedLine()
    {
        using var fixture = new NotePreviewFixture("29990101_000008");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile("sub/deep.gif");

        var report = fixture.PreviewMarkedBody();
        var text = PromptRenderer.RenderPreview(report);
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.True(report.HasHardError);
        Assert.Single(rejected);
        Assert.EndsWith("sub/deep.gif", rejected[0].TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(" deep.gif", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ok.png", rejected[0], StringComparison.Ordinal);
        Assert.Contains(NoteRules.GifPublishRejectedMessage, text, StringComparison.Ordinal);
        Assert.Contains("sub/deep.gif", report.Lines[0].Decision.Message, StringComparison.Ordinal);
        Assert.EndsWith("sub/deep.gif", report.Lines[0].Item.StageRel, StringComparison.Ordinal);
        Assert.Contains("ok.png 本身没问题", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectsNestedVideo_ShowsRelativePathNotOnlyFileName()
    {
        using var fixture = new NotePreviewFixture("29990101_000009");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile("sub/clip.mp4");

        var report = fixture.PreviewMarkedBody();
        var rejected = Bullets(PromptRenderer.RenderPreview(report))
            .Where(line => line.Contains("[拒绝]", StringComparison.Ordinal))
            .ToList();

        Assert.Single(rejected);
        Assert.EndsWith("sub/clip.mp4", rejected[0].TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(" clip.mp4", rejected[0], StringComparison.Ordinal);
        Assert.Contains(NoteRules.BlockedInFolderPrefix + "sub/clip.mp4", report.Lines[0].Decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectsGifWithoutOtherImages_DoesNotCallItInnocent()
    {
        using var fixture = new NotePreviewFixture("29990101_000006");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("anim.gif");

        var text = PromptRenderer.RenderPreview(fixture.PreviewMarkedBody());
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.Single(rejected);
        Assert.Contains("anim.gif", rejected[0], StringComparison.Ordinal);
        Assert.Contains("这个文件导致整篇被拒绝", text, StringComparison.Ordinal);
        Assert.DoesNotContain("本身没问题", text, StringComparison.Ordinal);
        Assert.DoesNotContain("正文.webp", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectsBlockedVideo_NamesVideoNotPng()
    {
        using var fixture = new NotePreviewFixture("29990101_000007");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile("clip.mp4");

        var text = PromptRenderer.RenderPreview(fixture.PreviewMarkedBody());
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.Single(rejected);
        Assert.Contains("clip.mp4", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ok.png", rejected[0], StringComparison.Ordinal);
        Assert.Contains("夹内有不能上页的文件：clip.mp4", text, StringComparison.Ordinal);
        Assert.Contains("ok.png 本身没问题，因整篇被拒绝而不会上页。", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_IgnoresGifInsideOriginals()
    {
        using var fixture = new NotePreviewFixture("29990101_000003");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");
        fixture.WriteFile(Path.Combine(".originals", "anim.gif"));

        var text = PromptRenderer.RenderPreview(fixture.PreviewMarkedBody());

        Assert.Contains("[允许]", text, StringComparison.Ordinal);
        Assert.DoesNotContain(NoteRules.GifPublishRejectedMessage, text, StringComparison.Ordinal);
        Assert.Contains("ok.png", text, StringComparison.Ordinal);
        Assert.DoesNotContain("正文.webp", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectsImageOnlyMark_WithoutInventingBodyWebp()
    {
        using var fixture = new NotePreviewFixture("29990101_000004");
        fixture.WriteBody("# 一篇\n\n文字\n");
        fixture.WriteFile("ok.png");

        var stage = fixture.Stage("ok.png");
        var text = PromptRenderer.RenderPreview(fixture.Preview(stage));

        Assert.Contains("[拒绝]", text, StringComparison.Ordinal);
        Assert.Contains("须标记正文.md", text, StringComparison.Ordinal);
        Assert.Contains("ok.png", text, StringComparison.Ordinal);
        Assert.Contains("「一篇」", text, StringComparison.Ordinal);
        Assert.DoesNotContain("正文.webp", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectionLines_IncludeEachNoteTitle()
    {
        using var fixture = new NotePreviewFixture("29990101_000010");
        var morning = fixture.StageOf(fixture.WriteNote("29990101_000010", "清晨散步", "anim.gif"));
        var chart = fixture.StageOf(fixture.WriteNote("29990101_000011", "控制图", "anim.gif"));

        var text = PromptRenderer.RenderPreview(fixture.PreviewStages(new[] { morning, chart }));
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, rejected.Count);
        Assert.Contains(rejected, line => line.Contains("「清晨散步」", StringComparison.Ordinal) && line.Contains("anim.gif", StringComparison.Ordinal));
        Assert.Contains(rejected, line => line.Contains("「控制图」", StringComparison.Ordinal) && line.Contains("anim.gif", StringComparison.Ordinal));
        Assert.DoesNotContain(rejected, line =>
            line.Contains("「清晨散步」", StringComparison.Ordinal)
            && line.Contains("「控制图」", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_RejectionLine_PrefersCatalogTitleOverHeading()
    {
        using var fixture = new NotePreviewFixture("29990101_000012");
        var folder = fixture.WriteNote("29990101_000012", "正文标题", "anim.gif");
        var stage = fixture.StageOf(folder);
        var works = new[]
        {
            new WorkCatalogItem
            {
                Id = "29990101_000012",
                Channel = NoteRules.ChannelKey,
                Title = "目录里的清晨",
                SourceKind = "note"
            }
        };

        var text = PromptRenderer.RenderPreview(fixture.PreviewStages(new[] { stage }, works));
        var rejected = Bullets(text).Where(line => line.Contains("[拒绝]", StringComparison.Ordinal)).ToList();

        Assert.Single(rejected);
        Assert.Contains("「目录里的清晨」", rejected[0], StringComparison.Ordinal);
        Assert.Contains("anim.gif", rejected[0], StringComparison.Ordinal);
        Assert.DoesNotContain("正文标题", rejected[0], StringComparison.Ordinal);
    }

    private static List<string> Bullets(string text)
    {
        return text.Split('\n').Where(line => line.StartsWith("- [", StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// 临时心得夹与最小工作区。预演调用顺序与主窗口 BuildPreview 相同。
    /// </summary>
    private sealed class NotePreviewFixture : IDisposable
    {
        private readonly string mRoot;
        private readonly WorkspaceSession mSession;
        private readonly string mFolderRel;

        public NotePreviewFixture(string folderName)
        {
            mRoot = Path.Combine(Path.GetTempPath(), "sms-note-preview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(mRoot);
            var channel = new ChannelProfile
            {
                Key = NoteRules.ChannelKey,
                Zh = "心得",
                Capabilities = new ChannelCapabilities { Catalog = true, Ingest = true }
            };
            var profile = new WorkspaceProfile
            {
                Name = "预演",
                ProfilePath = Path.Combine(mRoot, "profile.json"),
                ResolvedRoot = mRoot,
                StageRoot = "stage",
                StageFolderPattern = "ZhKey",
                SiteCatalog = new SiteCatalogConfig
                {
                    Kind = "personalworks-ts",
                    Path = "missing-works.ts"
                },
                Channels = new List<ChannelProfile> { channel }
            };
            mFolderRel = WorkspaceProfileLoader.ChannelStageRelative(profile, channel) + "/" + folderName;
            Folder = Path.Combine(
                WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot),
                mFolderRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Folder);
            mSession = new WorkspaceSession { Profile = profile };
        }

        private string Folder { get; }

        public void WriteBody(string markdown)
        {
            File.WriteAllText(Path.Combine(Folder, NoteRules.BodyFileName), markdown);
            NoteConfigStore.Save(Folder, new NoteConfig
            {
                CreatedAt = "2999-01-01 00:00:02",
                Slug = "sample",
                Title = "一篇"
            });
        }

        public void WriteFile(string relative)
        {
            var path = Path.Combine(Folder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[] { 0 });
        }

        public StageItem Stage(string fileName)
        {
            var rel = JsonUtil.ToRel(mFolderRel + "/" + fileName);
            return new StageItem
            {
                ChannelKey = NoteRules.ChannelKey,
                StageRel = rel,
                FullPath = Path.Combine(Folder, fileName.Replace('/', Path.DirectorySeparatorChar)),
                WorkIdGuess = Path.GetFileName(mFolderRel)
            };
        }

        /// <summary>
        /// 在同一栏目下再写一篇笔记，返回这篇的文件夹。标题来自正文一级标题。
        /// </summary>
        public string WriteNote(string folderName, string heading, params string[] relativeFiles)
        {
            var channel = mSession.Profile.Channels[0];
            var rel = WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel) + "/" + folderName;
            var full = Path.Combine(
                WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot),
                rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(full);
            File.WriteAllText(Path.Combine(full, NoteRules.BodyFileName), "# " + heading + "\n\n文字\n");
            NoteConfigStore.Save(full, new NoteConfig
            {
                CreatedAt = "2999-01-01 00:00:02",
                Slug = NoteConfigStore.DefaultSlug(folderName),
                Title = heading
            });
            foreach (var relative in relativeFiles)
            {
                var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, new byte[] { 0 });
            }

            return full;
        }

        /// <summary>
        /// 用笔记文件夹里的正文做成预演用的投放条目。
        /// </summary>
        public StageItem StageOf(string folderFull)
        {
            var channel = mSession.Profile.Channels[0];
            var channelFull = Path.Combine(
                WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot),
                WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel).Replace('/', Path.DirectorySeparatorChar));
            var rel = WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel)
                + "/"
                + Path.GetRelativePath(channelFull, folderFull).Replace('\\', '/');
            return new StageItem
            {
                ChannelKey = NoteRules.ChannelKey,
                StageRel = JsonUtil.ToRel(rel + "/" + NoteRules.BodyFileName),
                FullPath = Path.Combine(folderFull, NoteRules.BodyFileName),
                WorkIdGuess = Path.GetFileName(folderFull)
            };
        }

        /// <summary>
        /// 一次预演多篇笔记的正文标记。works 用来提供项目栏标题。
        /// </summary>
        public PreviewReport PreviewStages(IReadOnlyList<StageItem> stages, IReadOnlyList<WorkCatalogItem>? works = null)
        {
            var session = new WorkspaceSession
            {
                Profile = mSession.Profile,
                StageItems = stages,
                Works = works ?? Array.Empty<WorkCatalogItem>()
            };
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                stages.Select(stage => new MarkedMedia(MediaIntent.StageIngest, stage, null, stage.WorkIdGuess)),
                stages[0].WorkIdGuess);
            return PreviewReporter.BuildFromDocument(session, document);
        }

        public PreviewReport PreviewMarkedBody()
        {
            return Preview(Stage(NoteRules.BodyFileName));
        }

        public PreviewReport Preview(StageItem stage)
        {
            var session = new WorkspaceSession
            {
                Profile = mSession.Profile,
                StageItems = new[] { stage }
            };
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { new MarkedMedia(MediaIntent.StageIngest, stage, null, stage.WorkIdGuess) },
                stage.WorkIdGuess);
            return PreviewReporter.BuildFromDocument(session, document);
        }

        public void Dispose()
        {
            if (Directory.Exists(mRoot))
            {
                Directory.Delete(mRoot, recursive: true);
            }
        }
    }
}
