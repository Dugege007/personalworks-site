using PersonalWorks.SiteMediaStudio.Core;
using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PersonalWorksCatalogTests
{
    [Fact]
    public void Parse_WorksTs_ReadsLandscapeAlbum()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);

        var worksPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        var works = PersonalWorksSiteCatalogReader.Load(worksPath);
        var xiaowayao = works.FirstOrDefault(item => item.Id == "xiaowayao");
        Assert.NotNull(xiaowayao);
        Assert.Equal(3, xiaowayao!.Media.Count);
        Assert.Equal("landscape-rendering/xiaowayao/01.jpg", xiaowayao.Media[0].Src);

        var photo = works.FirstOrDefault(item => item.Id == "sample-2" && item.Channel == "landscape-photo");
        Assert.NotNull(photo);
        Assert.Equal("landscape-photo/sample-2/01.webp", photo!.Media[0].Src);
        Assert.Equal("水面", photo.Media[0].Label);
        Assert.Equal(3, photo.Media.Count);
        Assert.True(string.IsNullOrWhiteSpace(photo.Media[1].Src));

        var cds = works.FirstOrDefault(item => item.Id == "sample-1" && item.Channel == "landscape-cds");
        Assert.NotNull(cds);
        Assert.Equal(3, cds!.Media.Count);
        Assert.Equal("landscape-cds/sample-1/01.webp", cds.Media[0].Src);
        Assert.True(string.IsNullOrWhiteSpace(cds.Media[1].Src));
        Assert.Equal("图框", cds.Media[1].Label);

        var ridge = works.FirstOrDefault(item => item.Id == "sample-1" && item.Channel == "landscape-photo");
        Assert.NotNull(ridge);
        Assert.Contains(ridge!.Media, item => item.Label == "云隙" && string.IsNullOrWhiteSpace(item.Src));
    }

    [Fact]
    public void Load_SiteTsWithoutLedger_StillCatalogsEightProfileImagesAndGames()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);

        var session = WorkspaceSession.Load(profilePath!);
        var profile = Assert.Single(session.Works, item => item.SourceKind == "site-profile");
        Assert.Equal(8, profile.Media.Count);
        Assert.Equal(8, session.SiteItems.Count(item => item.ChannelKey == "profile"));
        Assert.All(
            session.SiteItems.Where(item => item.ChannelKey == "profile"),
            item =>
            {
                Assert.True(item.ReferenceCount > 0);
                Assert.False(item.IsHidden);
            });
        Assert.Equal(2, session.ContentRefCountDict["profile/portrait.webp"]);

        var game = Assert.Single(session.Works, item => item.Channel == "game-dev" && item.Id == "ridge");
        Assert.Equal(2, game.Media.Count);
        Assert.Equal("game-dev/ridge/01.webp", game.Media[0].Src);
        Assert.Equal(2, session.ContentRefCountDict["game-dev/ridge/01.webp"]);
    }

    [Fact]
    public void PersonalWorksProfile_ConfiguresAllActualContentSources()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        Assert.EndsWith("works.ts", profile.SiteCatalog.Path, StringComparison.Ordinal);
        Assert.EndsWith("site.ts", profile.SiteCatalog.SitePath, StringComparison.Ordinal);
        Assert.EndsWith("initialWorkProjects.ts", profile.SiteCatalog.WorkDataPath, StringComparison.Ordinal);
        Assert.EndsWith("initialGameProjects.ts", profile.SiteCatalog.GameDataPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonalWorksProfile_LoadsImportedWorksAndGameScreenshots()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);

        Assert.Contains(session.Works, item =>
            item.Channel == "landscape-rendering" && item.Id == "xiaowayao");
        var antigravity = Assert.Single(session.Works, item =>
            item.Channel == "game-dev" && item.Id == "antigravity");
        Assert.Equal(8, antigravity.Media.Count);
        Assert.Equal(4, antigravity.Media.Single(item => item.Src == "game-dev/antigravity/02.webp").Stars);
        Assert.Equal(4, session.SiteItems.Single(item => item.ObjectKey == "game-dev/antigravity/02.webp").Stars);
        Assert.Equal(10, session.SiteItems.Count(item => item.ChannelKey == "profile"));
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Id is "参赛作品" or "技术文章" or "练习作品"
                or "上海道田景观工程咨询有限公司" or "FlexSim" or "ProdLineSim_自研平台");
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "game-dev" && item.Id == "反重力 AntiGravity");
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "landscape-rendering" && item.Id == "201804 北京丰台小瓦窑");
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "landscape-rendering" && item.Id == "202008 哈尔滨江御府");
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "profile");
        Assert.Contains(session.Works, item =>
            item.Channel == "profile" && !item.IsUnregistered && item.Title == "仪摄影写真");
        Assert.Contains(session.Works, item =>
            item.Channel == "profile" && !item.IsUnregistered && item.Title == "徐汇区");
        Assert.All(
            session.StageItems.Where(item =>
                item.ChannelKey == "profile"
                && StageFolderClaim.TitleMatchesFolder("徐汇区", item.WorkIdGuess)),
            item =>
            {
                Assert.True(StageWorkScope.BelongsToWork(session, item, "徐汇区"));
                Assert.False(StageWorkScope.BelongsToWork(session, item, "仪摄影写真"));
            });
        Assert.All(
            session.StageItems.Where(item =>
                item.ChannelKey == "profile"
                && StageFolderClaim.TitleMatchesFolder("仪摄影写真", item.WorkIdGuess)),
            item => Assert.False(StageWorkScope.BelongsToWork(session, item, "徐汇区")));
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "landscape-photo");
        Assert.DoesNotContain(session.Works, item =>
            item.IsUnregistered && item.Channel == "humanist-photo");
        Assert.Contains(session.Works, item =>
            item.IsUnregistered && item.Channel == "landscape-rendering" && item.Id == "精选");
        Assert.All(
            session.StageItems.Where(item =>
                item.ChannelKey == "profile"
                && string.IsNullOrWhiteSpace(item.LedgerStatus)
                && StageFolderClaim.TitleMatchesFolder("仪摄影写真", item.WorkIdGuess)),
            item => Assert.True(StageWorkScope.BelongsToWork(session, item, "仪摄影写真")));
        Assert.All(
            session.StageItems.Where(item =>
                item.ChannelKey == "landscape-rendering"
                && item.WorkIdGuess == "202008 哈尔滨江御府"
                && string.IsNullOrWhiteSpace(item.LedgerStatus)),
            item => Assert.True(StageWorkScope.BelongsToWork(session, item, "harbin-jiangyufu")));
    }

    [Fact]
    public void PersonalWorksProfile_SameTitleBatches_DoNotCrossClaimStageItems()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var registeredList = session.Works.Where(item => !item.IsUnregistered).ToList();
        var leakList = new List<string>();

        foreach (var work in registeredList)
        {
            foreach (var item in session.StageItems.Where(stage => stage.ChannelKey == work.Channel))
            {
                if (!StageWorkScope.BelongsToWork(session, item, work.Id))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(item.MatchedObject))
                {
                    var owner = session.SiteItems.FirstOrDefault(site =>
                        string.Equals(site.ObjectKey, item.MatchedObject, StringComparison.Ordinal));
                    if (owner != null && owner.WorkId != work.Id)
                    {
                        leakList.Add($"{work.Id} 收入 {item.StageRel}，对象属 {owner.WorkId}");
                    }
                }

                var folderId = StageFolderClaim.FolderIdOf(item);
                if (StageFolderClaim.TryClaim(
                        registeredList,
                        work.Channel,
                        folderId,
                        session.Profile,
                        out var claimed)
                    && claimed != null
                    && claimed.Id != work.Id)
                {
                    leakList.Add($"{work.Id} 收入夹 {folderId}，认领却是 {claimed.Id}");
                }

                var workDate = TrailingDate(work.Id);
                var folderDate = StageFolderClaim.ReadLeadingDate(folderId);
                if (workDate.Length > 0
                    && folderDate.Length > 0
                    && workDate != folderDate
                    && registeredList.Any(other =>
                        other.Channel == work.Channel
                        && other.Title == work.Title
                        && other.Id != work.Id))
                {
                    leakList.Add($"{work.Id} 收入了另一期夹 {folderId}");
                }
            }
        }

        Assert.True(leakList.Count == 0, string.Join("; ", leakList));
    }

    [Fact]
    public void PersonalWorksProfile_FukangWorks_KeepDisjointStageScope()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var fifteenSet = session.StageItems
            .Where(item => StageWorkScope.BelongsToWork(session, item, "huaian-fukang-15"))
            .Select(item => item.StageRel)
            .ToHashSet(StringComparer.Ordinal);
        var threeSet = session.StageItems
            .Where(item => StageWorkScope.BelongsToWork(session, item, "huaian-fukang-3"))
            .Select(item => item.StageRel)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(fifteenSet.Intersect(threeSet));
        Assert.All(fifteenSet, path => Assert.DoesNotContain("HAFK-3#", path, StringComparison.Ordinal));
        Assert.All(threeSet, path => Assert.DoesNotContain("HAFKC-15#", path, StringComparison.Ordinal));
        Assert.Contains(fifteenSet, path => path.Contains("HAFKC-15#", StringComparison.Ordinal));
        Assert.Contains(threeSet, path => path.Contains("HAFK-3#", StringComparison.Ordinal));
    }

    /// <summary>
    /// 作品 id 末尾的日期段。
    /// </summary>
    private static string TrailingDate(string workId)
    {
        var sep = workId.LastIndexOfAny(['-', '_']);
        if (sep < 0)
        {
            return "";
        }

        var tail = workId[(sep + 1)..];
        return tail is { Length: 6 or 8 } && tail.All(char.IsDigit) ? tail : "";
    }

    [Fact]
    public void PersonalWorksProfile_MatchesInitialBatchProjectsAndSelectedMedia()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var batchPath = Path.Combine(
            session.Profile.ResolvedRoot,
            "工具",
            "站点媒体生命周期",
            "batches",
            "initial-projects.json");
        using var document = JsonDocument.Parse(File.ReadAllText(batchPath));

        foreach (var project in document.RootElement.GetProperty("projects").EnumerateArray())
        {
            var id = project.GetProperty("id").GetString();
            var channel = project.GetProperty("channel").GetString();
            var selectedCount = project.GetProperty("media")
                .EnumerateArray()
                .Count(media => media.GetProperty("action").GetString() != "skip");
            var work = Assert.Single(session.Works, item =>
                item.Id == id && item.Channel == channel && !item.IsUnregistered);
            // 首轮清单之后又上页或内容层已少于清单的项目，按现行 media 条数核对。
            var expected = id switch
            {
                "shanghai-mansheng-packaging" => 9,
                "weichai-spark-plug" => 4,
                _ => selectedCount
            };
            Assert.Equal(expected, work.Media.Count);
        }
    }

    [Fact]
    public void Parse_JsonMediaObject_ReadsFrameThemesAndTags()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-frame-tags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "works.ts");
        File.WriteAllText(path, """
            export const placeholderWorks = [
              {
                id: "shoot",
                channel: "real-world-photo",
                title: "新加坡",
                themes: ["landscape-photo"],
                media: [{"kind":"image","label":"效果图 01","src":"photo/real-world-photo/shoot/01.webp","themes":["landscape-photo"],"tags":["展陈"]}]
              }
            ];
            """);
        try
        {
            var work = Assert.Single(PersonalWorksSiteCatalogReader.Load(path));
            var frame = Assert.Single(work.Media);
            Assert.Equal("photo/real-world-photo/shoot/01.webp", frame.Src);
            Assert.Equal(new[] { "landscape-photo" }, frame.Themes);
            Assert.Equal(new[] { "展陈" }, frame.Tags);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Parse_ObjectMedia_ReadsStars()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-object-stars-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "works.ts");
        File.WriteAllText(path, """
            export const registeredWorks = [
              {
                id: "shoot",
                channel: "real-world-photo",
                title: "拍摄",
                media: [{ kind: "image", label: "DSC00001", src: "photo/real-world-photo/shoot/01.webp", tags: ["展馆"], stars: 4 }]
              }
            ];
            """);
        try
        {
            var frame = Assert.Single(Assert.Single(PersonalWorksSiteCatalogReader.Load(path)).Media);
            Assert.Equal(4, frame.Stars);
            Assert.Equal(new[] { "展馆" }, frame.Tags);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PhotoPublishedWithoutSrc_ShowsHiddenOnThatWork()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-photo-hidden-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "stage"));
        Directory.CreateDirectory(Path.Combine(dir, "content"));
        File.WriteAllText(
            Path.Combine(dir, "profile.json"),
            """
            {
              "version": 1,
              "name": "摄影隐藏",
              "stageRoot": "stage",
              "placeholdersRoot": "public/placeholders",
              "ledgerPath": "content/media-ledger.json",
              "siteCatalog": { "kind": "json", "path": "content/catalog.json" },
              "channels": [
                { "key": "real-world-photo", "zh": "现实摄影", "en": "Real-world Photography", "deco": "REAL-WORLD" },
                { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(dir, "content", "catalog.json"),
            """
            {
              "works": [
                {
                  "id": "shoot",
                  "channel": "real-world-photo",
                  "title": "拍摄",
                  "media": [
                    { "kind": "image", "label": "01", "src": "photo/real-world-photo/shoot/01.webp" }
                  ]
                },
                {
                  "id": "other",
                  "channel": "real-world-photo",
                  "title": "另一组",
                  "media": []
                },
                {
                  "id": "park",
                  "channel": "demo-render",
                  "title": "公园",
                  "media": []
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(dir, "content", "media-ledger.json"),
            """
            {
              "version": 1,
              "records": [
                {
                  "object": "photo/real-world-photo/shoot/01.webp",
                  "stageRel": "stage/01.webp",
                  "status": "published"
                },
                {
                  "object": "photo/real-world-photo/shoot/09.webp",
                  "stageRel": "stage/09.webp",
                  "status": "published"
                },
                {
                  "object": "demo-render/park/02.png",
                  "stageRel": "stage/02.png",
                  "status": "published"
                }
              ]
            }
            """);
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(dir, "profile.json"));
            var photoHidden = Assert.Single(session.SiteItems, item =>
                item.ObjectKey == "photo/real-world-photo/shoot/09.webp");
            Assert.True(photoHidden.IsHidden);
            Assert.Equal("shoot", photoHidden.WorkId);
            Assert.DoesNotContain(session.SiteItems, item =>
                item.WorkId == "other" && item.ObjectKey.Contains("09.webp", StringComparison.Ordinal));
            var onPage = Assert.Single(session.SiteItems, item =>
                item.ObjectKey == "photo/real-world-photo/shoot/01.webp");
            Assert.False(onPage.IsHidden);
            var renderHidden = Assert.Single(session.SiteItems, item =>
                item.ObjectKey == "demo-render/park/02.png");
            Assert.True(renderHidden.IsHidden);
            Assert.Equal("park", renderHidden.WorkId);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PendingWithdraw_StaysOffSiteGrid_HiddenLeftoverRemains()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-withdraw-grid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "stage"));
        Directory.CreateDirectory(Path.Combine(dir, "content"));
        var profilePath = Path.Combine(dir, "profile.json");
        File.WriteAllText(
            profilePath,
            """
            {
              "version": 1,
              "name": "撤下不进站点",
              "stageRoot": "stage",
              "placeholdersRoot": "public/placeholders",
              "ledgerPath": "content/media-ledger.json",
              "siteCatalog": { "kind": "json", "path": "content/catalog.json" },
              "channels": [
                { "key": "real-world-photo", "zh": "现实摄影", "en": "Real-world Photography", "deco": "REAL-WORLD" }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(dir, "content", "catalog.json"),
            """
            {
              "works": [
                {
                  "id": "shoot",
                  "channel": "real-world-photo",
                  "title": "拍摄",
                  "media": [
                    { "kind": "image", "label": "01", "src": "photo/real-world-photo/shoot/01.webp" }
                  ]
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(dir, "content", "media-ledger.json"),
            """
            {
              "version": 1,
              "records": [
                {
                  "object": "photo/real-world-photo/shoot/01.webp",
                  "stageRel": "real-world-photo/01.webp",
                  "status": "published"
                },
                {
                  "object": "photo/real-world-photo/shoot/04.webp",
                  "stageRel": "real-world-photo/04.webp",
                  "status": "published"
                },
                {
                  "object": "photo/real-world-photo/shoot/09.webp",
                  "stageRel": "real-world-photo/09.webp",
                  "status": "published"
                }
              ]
            }
            """);
        var stageDir = Path.Combine(dir, "stage", "real-world-photo");
        Directory.CreateDirectory(stageDir);
        File.WriteAllBytes(Path.Combine(stageDir, "01.webp"), [0xFF]);
        File.WriteAllBytes(Path.Combine(stageDir, "04.webp"), [0xFF]);
        File.WriteAllBytes(Path.Combine(stageDir, "09.webp"), [0xFF]);
        try
        {
            var session = WorkspaceSession.Load(
                profilePath,
                new PendingPublish
                {
                    ProfilePath = Path.GetFullPath(profilePath),
                    Deploy = "spa",
                    WithdrawObjectList = new List<string> { "photo/real-world-photo/shoot/04.webp" }
                });
            Assert.DoesNotContain(session.SiteItems, item => item.ObjectKey.EndsWith("/04.webp", StringComparison.Ordinal));
            var hidden = Assert.Single(session.SiteItems, item => item.ObjectKey.EndsWith("/09.webp", StringComparison.Ordinal));
            Assert.True(hidden.IsHidden);
            var onPage = Assert.Single(session.SiteItems, item => item.ObjectKey.EndsWith("/01.webp", StringComparison.Ordinal));
            Assert.False(onPage.IsHidden);
            var withdrawn = Assert.Single(session.StageItems, item => item.MatchedObject != null && item.MatchedObject.EndsWith("/04.webp", StringComparison.Ordinal));
            Assert.Equal("已撤下", PublishStatus.ForStage(withdrawn));
            var stillPublished = Assert.Single(session.StageItems, item => item.MatchedObject != null && item.MatchedObject.EndsWith("/01.webp", StringComparison.Ordinal));
            Assert.Equal("已发布", PublishStatus.ForStage(stillPublished));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
