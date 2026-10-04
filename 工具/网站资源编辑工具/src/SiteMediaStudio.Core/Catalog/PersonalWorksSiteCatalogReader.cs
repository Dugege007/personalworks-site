using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// PersonalWorks 内容层适配器：统一解析 works.ts 与 site.ts 中实际使用的图像。
/// </summary>
public static class PersonalWorksSiteCatalogReader
{
    private static readonly Regex AlbumRegex = new(
        @"album\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(\d+)(?:\s*,\s*""([^""]+)"")?\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex ListedRegex = new(
        @"listed\(\s*\[(.*?)\]\s*\)",
        RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ListedEntryRegex = new(
        @"\[\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\]",
        RegexOptions.Compiled);
    private static readonly Regex CoveredRegex = new(
        @"covered\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*\[([^\]]+)\]\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex CoverRegex = new(
        @"cover\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex ImagesRegex = new(
        @"images\(\s*([^)]*)\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex SrcRegex = new(
        @"[""']?src[""']?\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex IdRegex = new(
        @"[""']?id[""']?\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex ChannelRegex = new(
        @"[""']?channel[""']?\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex TitleRegex = new(
        @"[""']?title[""']?\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex StageFolderRegex = new(
        @"[""']?stageFolder[""']?\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex StringRegex = new(
        @"""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// 解析 works.ts 图像条目；covered / images 的无 src 槽一并收入。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Load(string worksFullPath)
    {
        return Load(worksFullPath, null);
    }

    /// <summary>
    /// 按配置合并 works.ts 作品、形象照与游戏项目图库。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Load(string worksFullPath, string? siteFullPath)
    {
        return Load(worksFullPath, siteFullPath, null, null);
    }

    /// <summary>
    /// 按配置合并入口文件及其显式数据源。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Load(
        string worksFullPath,
        string? siteFullPath,
        string? workDataFullPath,
        string? gameDataFullPath)
    {
        return Load(worksFullPath, siteFullPath, workDataFullPath, gameDataFullPath, null, null);
    }

    /// <summary>
    /// 合并入口文件与数据源；形象照按台账场次夹拆成作品。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Load(
        string worksFullPath,
        string? siteFullPath,
        string? workDataFullPath,
        string? gameDataFullPath,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        WorkspaceProfile? profile)
    {
        if (!File.Exists(worksFullPath))
        {
            throw new FileNotFoundException("找不到内容层 works.ts。", worksFullPath);
        }

        var workList = ParseWorks(File.ReadAllText(worksFullPath));
        if (!string.IsNullOrWhiteSpace(workDataFullPath))
        {
            if (!File.Exists(workDataFullPath))
            {
                throw new FileNotFoundException("找不到 works.ts 导入的作品数据源。", workDataFullPath);
            }

            workList.AddRange(ParseWorks(File.ReadAllText(workDataFullPath)));
        }
        if (string.IsNullOrWhiteSpace(siteFullPath))
        {
            return workList;
        }

        if (!File.Exists(siteFullPath))
        {
            throw new FileNotFoundException("找不到内容层 site.ts。", siteFullPath);
        }

        var siteText = File.ReadAllText(siteFullPath);
        workList.AddRange(ParseProfile(siteText, ledgerDict, profile));
        var gameText = string.IsNullOrWhiteSpace(gameDataFullPath)
            ? siteText
            : File.Exists(gameDataFullPath)
                ? File.ReadAllText(gameDataFullPath)
                : throw new FileNotFoundException("找不到 site.ts 导入的游戏数据源。", gameDataFullPath);
        workList.AddRange(ParseGames(gameText));
        return workList;
    }

    /// <summary>
    /// 解析 works.ts 的作品数组。
    /// </summary>
    private static List<WorkCatalogItem> ParseWorks(string text)
    {
        var markerList = new[]
        {
            "export const placeholderWorks",
            "export const initialWorkProjects",
            "const lineSimulationWorks",
            "const registeredWorks",
            "export const registeredWorks"
        };
        var start = markerList
            .Select(marker => text.IndexOf(marker, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (start < 0)
        {
            return new List<WorkCatalogItem>();
        }

        var slice = text[start..];
        var idMatches = IdRegex.Matches(slice);
        var workList = new List<WorkCatalogItem>();
        for (var i = 0; i < idMatches.Count; i++)
        {
            var from = idMatches[i].Index;
            var to = i + 1 < idMatches.Count ? idMatches[i + 1].Index : slice.Length;
            var block = slice[from..to];
            var id = idMatches[i].Groups[1].Value;
            var channel = ChannelRegex.Match(block).Groups[1].Value;
            var title = TitleRegex.Match(block).Groups[1].Value;
            var summary = ParsePropertyString(block, "summary") ?? "";
            var year = ParsePropertyString(block, "year") ?? "";
            var startedOn = ParsePropertyString(block, "startedOn");
            var place = ParsePropertyString(block, "place");
            var head = HeadBeforeMedia(block);
            var stageFolder = StageFolderRegex.Match(block).Groups[1].Value;
            var mediaList = ParseMedia(block);
            if (string.IsNullOrWhiteSpace(channel))
            {
                channel = mediaList
                    .Select(item => item.Src)
                    .FirstOrDefault(src => !string.IsNullOrWhiteSpace(src) && src.Contains('/'))
                    ?.Split('/')[0] ?? "";
            }
            if (string.IsNullOrWhiteSpace(channel))
            {
                continue;
            }
            var work = new WorkCatalogItem
            {
                Id = id,
                Channel = channel,
                Title = string.IsNullOrWhiteSpace(title) ? id : title,
                Summary = summary,
                SourceKind = "works",
                StageFolder = string.IsNullOrWhiteSpace(stageFolder) ? null : JsonUtil.ToRel(stageFolder),
                Year = year,
                StartedOn = string.IsNullOrWhiteSpace(startedOn) ? null : startedOn,
                Place = string.IsNullOrWhiteSpace(place) ? null : place,
                Themes = ParsePropertyStringArray(head, "themes"),
                Tags = ParsePropertyStringArray(head, "tags"),
                Media = mediaList
            };
            CopyParkedStars(work, ParseNamedStarMap(block, "hiddenStars"));
            workList.Add(work);
        }

        return workList;
    }

    /// <summary>
    /// 解析 profile.portraitSrcs 与 portraitSrc，并按台账场次夹拆成作品。
    /// </summary>
    private static IReadOnlyList<WorkCatalogItem> ParseProfile(
        string text,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        WorkspaceProfile? profile)
    {
        var block = ExtractAssignedBlock(text, "export const profile", '{', '}');
        if (block == null)
        {
            return Array.Empty<WorkCatalogItem>();
        }

        var orderedSrcList = ParsePropertyStringArray(block, "portraitSrcs");
        var primarySrc = ParsePropertyString(block, "portraitSrc");
        var refCountDict = CountRefs(orderedSrcList, primarySrc);
        if (!string.IsNullOrWhiteSpace(primarySrc) && !orderedSrcList.Contains(primarySrc, StringComparer.Ordinal))
        {
            orderedSrcList.Insert(0, primarySrc);
        }

        var starDict = ParseNamedStarMap(block, "portraitStars");
        var mediaList = orderedSrcList
            .Distinct(StringComparer.Ordinal)
            .Select(src =>
            {
                var rel = JsonUtil.ToRel(src);
                return new WorkMediaItem
                {
                    Kind = "image",
                    Label = Path.GetFileNameWithoutExtension(src),
                    Src = rel,
                    Stars = starDict.TryGetValue(rel, out var stars) ? stars : null,
                    ReferenceCount = refCountDict.GetValueOrDefault(src),
                    IsPrimary = string.Equals(src, primarySrc, StringComparison.Ordinal)
                };
            })
            .ToList();
        var splitList = mediaList.Count == 0
            ? Array.Empty<WorkCatalogItem>()
            : SplitProfileByStageFolder(mediaList, ledgerDict, profile);
        var profileList = MergeProfileSessions(text, splitList);
        foreach (var work in profileList)
        {
            CopyParkedStars(work, starDict);
        }

        return profileList;
    }

    /// <summary>
    /// 把已确认的场次空壳并入编目，已有台账拆分的场次不重复。
    /// </summary>
    private static IReadOnlyList<WorkCatalogItem> MergeProfileSessions(
        string text,
        IReadOnlyList<WorkCatalogItem> existingList)
    {
        var array = ExtractAssignedBlock(text, "export const registeredProfileSessions", '[', ']')
            ?? ExtractAssignedBlock(text, "const registeredProfileSessions", '[', ']');
        if (array == null)
        {
            return existingList;
        }

        var knownSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in existingList)
        {
            knownSet.Add(work.Id);
            if (!string.IsNullOrWhiteSpace(work.StageFolder))
            {
                knownSet.Add(JsonUtil.ToRel(work.StageFolder));
            }
        }

        var resultList = existingList.ToList();
        foreach (var block in SplitTopLevelObjects(array))
        {
            var id = ParsePropertyString(block, "id");
            var title = ParsePropertyString(block, "title");
            var stageFolder = ParsePropertyString(block, "stageFolder");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var folderRel = string.IsNullOrWhiteSpace(stageFolder) ? "" : JsonUtil.ToRel(stageFolder);
            if (knownSet.Contains(id) || (!string.IsNullOrWhiteSpace(folderRel) && knownSet.Contains(folderRel)))
            {
                continue;
            }

            resultList.Add(CreateProfileWork(
                id,
                string.IsNullOrWhiteSpace(title) ? id : title,
                string.IsNullOrWhiteSpace(folderRel) ? null : folderRel,
                Array.Empty<WorkMediaItem>()));
            knownSet.Add(id);
            if (!string.IsNullOrWhiteSpace(folderRel))
            {
                knownSet.Add(folderRel);
            }
        }

        return resultList;
    }

    /// <summary>
    /// 有台账场次夹时一夹一条作品，题名为去掉日期后的夹名；否则仍为单条形象照。
    /// </summary>
    private static IReadOnlyList<WorkCatalogItem> SplitProfileByStageFolder(
        IReadOnlyList<WorkMediaItem> mediaList,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        WorkspaceProfile? profile)
    {
        if (ledgerDict == null || ledgerDict.Count == 0)
        {
            return new[] { CreateProfileWork("portrait", "形象照", null, mediaList) };
        }

        var channel = StageWorkFolder.FindChannel(profile, "profile");
        var groupDict = new SortedDictionary<string, (string FolderGuess, List<WorkMediaItem> MediaList)>(
            StringComparer.Ordinal);
        var leftoverList = new List<WorkMediaItem>();
        foreach (var media in mediaList)
        {
            if (string.IsNullOrWhiteSpace(media.Src)
                || !ledgerDict.TryGetValue(media.Src, out var record)
                || !StageWorkScope.TryReadFolderFromStageRel(
                    record.StageRel,
                    channel,
                    out var folderId,
                    out var folderGuess))
            {
                leftoverList.Add(media);
                continue;
            }

            if (!groupDict.TryGetValue(folderId, out var group))
            {
                group = (folderGuess, new List<WorkMediaItem>());
                groupDict[folderId] = group;
            }

            group.MediaList.Add(media);
        }

        if (groupDict.Count == 0)
        {
            return new[] { CreateProfileWork("portrait", "形象照", null, mediaList) };
        }

        var workList = groupDict
            .Select(pair =>
            {
                var title = WorkRegisterRules.SeedTitle(pair.Key);
                var stageFolder = string.IsNullOrWhiteSpace(pair.Value.FolderGuess)
                    ? WorkRegisterRules.StageFolder("profile", pair.Key)
                    : JsonUtil.ToRel("profile/" + pair.Value.FolderGuess);
                return CreateProfileWork(title, title, stageFolder, pair.Value.MediaList);
            })
            .ToList();
        if (leftoverList.Count > 0 && workList.Count == 1)
        {
            workList[0] = CreateProfileWork(
                workList[0].Id,
                workList[0].Title,
                workList[0].StageFolder,
                workList[0].Media.Concat(leftoverList).ToList());
        }
        else if (leftoverList.Count > 0)
        {
            workList.Add(CreateProfileWork("portrait", "形象照", null, leftoverList));
        }

        return workList;
    }

    /// <summary>
    /// 组装一条形象照场次作品。
    /// </summary>
    private static WorkCatalogItem CreateProfileWork(
        string id,
        string title,
        string? stageFolder,
        IReadOnlyList<WorkMediaItem> mediaList)
    {
        return new WorkCatalogItem
        {
            Id = id,
            Channel = "profile",
            Title = title,
            SourceKind = "site-profile",
            SupportsAppend = true,
            StageFolder = string.IsNullOrWhiteSpace(stageFolder) ? null : JsonUtil.ToRel(stageFolder),
            Media = mediaList
        };
    }

    /// <summary>
    /// 解析 placeholderGames 中每个项目的 coverSrc 与可选图库数组。
    /// </summary>
    private static IReadOnlyList<WorkCatalogItem> ParseGames(string text)
    {
        var gameList = new List<WorkCatalogItem>();
        var seenSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in new[]
                 {
                     "export const placeholderGames",
                     "export const initialGameProjects",
                     "export const registeredGames",
                     "const registeredGames"
                 })
        {
            var array = ExtractAssignedBlock(text, marker, '[', ']');
            if (array == null)
            {
                continue;
            }

            foreach (var block in SplitTopLevelObjects(array))
            {
                ParseGameBlock(block, gameList, seenSet);
            }
        }

        return gameList;
    }

    /// <summary>
    /// 解析一条游戏项目；空截图仍作为可上页空壳保留。
    /// </summary>
    private static void ParseGameBlock(
        string block,
        List<WorkCatalogItem> gameList,
        HashSet<string> seenSet)
    {
        var id = ParsePropertyString(block, "id");
        if (string.IsNullOrWhiteSpace(id) || !seenSet.Add(id))
        {
            return;
        }

        var title = ParsePropertyString(block, "title");
        var lead = ParsePropertyString(block, "lead") ?? "";
        var stageFolder = ParsePropertyString(block, "stageFolder");
        var parkedStarDict = ParseNamedStarMap(block, "hiddenStars");
        var shots = TryReadNamedArray(block, "screenshots");
        if (shots is { Count: > 0 })
        {
            AddGame(gameList, id, title, lead, stageFolder, WithGameRefs(block, shots), parkedStarDict);
            return;
        }

        var arrayProperty = FirstExistingArrayProperty(block, "gallerySrcs", "coverSrcs", "imageSrcs");
        var gallerySrcList = arrayProperty == null
            ? new List<string>()
            : ParsePropertyStringArray(block, arrayProperty);
        if (gallerySrcList.Count == 0)
        {
            gallerySrcList = ListedEntryRegex.Matches(block)
                .Select(match => JsonUtil.ToRel(match.Groups[1].Value))
                .Where(src => src.StartsWith("game-dev/", StringComparison.Ordinal))
                .ToList();
        }

        var coverSrc = ParsePropertyString(block, "coverSrc");
        var refCountDict = CountRefs(gallerySrcList, coverSrc);
        if (!string.IsNullOrWhiteSpace(coverSrc) && !gallerySrcList.Contains(coverSrc, StringComparer.Ordinal))
        {
            gallerySrcList.Insert(0, coverSrc);
        }

        var mediaList = gallerySrcList
            .Distinct(StringComparer.Ordinal)
            .Select(src => new WorkMediaItem
            {
                Kind = "image",
                Label = Path.GetFileNameWithoutExtension(src),
                Src = JsonUtil.ToRel(src),
                ReferenceCount = refCountDict.GetValueOrDefault(src),
                IsPrimary = string.Equals(src, coverSrc, StringComparison.Ordinal)
            })
            .ToList();
        AddGame(gameList, id, title, lead, stageFolder, mediaList, parkedStarDict);
    }

    /// <summary>
    /// 截图上的引用次数与封面标记。星级、图名从原条目留下。
    /// </summary>
    private static List<WorkMediaItem> WithGameRefs(string block, List<WorkMediaItem> shots)
    {
        var coverSrc = ParsePropertyString(block, "coverSrc");
        var srcList = shots
            .Select(item => item.Src)
            .Where(src => !string.IsNullOrWhiteSpace(src))
            .Select(src => src!)
            .ToList();
        var refCountDict = CountRefs(srcList, coverSrc);
        return shots.Select(item => new WorkMediaItem
        {
            Kind = item.Kind,
            Label = item.Label,
            Src = item.Src,
            Poster = item.Poster,
            DisplayName = item.DisplayName,
            Description = item.Description,
            Stars = item.Stars,
            Themes = item.Themes,
            Tags = item.Tags,
            ReferenceCount = string.IsNullOrWhiteSpace(item.Src) ? 1 : refCountDict.GetValueOrDefault(item.Src),
            IsPrimary = string.Equals(item.Src, coverSrc, StringComparison.Ordinal)
        }).ToList();
    }

    /// <summary>
    /// 写入一条游戏编目。
    /// </summary>
    private static void AddGame(
        List<WorkCatalogItem> gameList,
        string id,
        string? title,
        string lead,
        string? stageFolder,
        List<WorkMediaItem> mediaList,
        IReadOnlyDictionary<string, int> parkedStarDict)
    {
        var work = new WorkCatalogItem
        {
            Id = id,
            Channel = "game-dev",
            Title = string.IsNullOrWhiteSpace(title) ? id : title,
            Summary = lead,
            SourceKind = "site-game",
            SupportsAppend = true,
            StageFolder = string.IsNullOrWhiteSpace(stageFolder) ? null : JsonUtil.ToRel(stageFolder),
            Media = mediaList
        };
        CopyParkedStars(work, parkedStarDict);
        gameList.Add(work);
    }

    /// <summary>
    /// 把旁路星级抄到作品上，供已隐藏卡片读取。
    /// </summary>
    private static void CopyParkedStars(WorkCatalogItem work, IReadOnlyDictionary<string, int> starDict)
    {
        foreach (var pair in starDict)
        {
            work.ParkedStarDict[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// 从单条作品文本块抽出带 src 的图像。
    /// </summary>
    private static List<WorkMediaItem> ParseMedia(string block)
    {
        var extracted = ContentPatchPlanner.TryReadMediaList(block);
        if (extracted is { Count: > 0 })
        {
            return extracted.ToList();
        }

        var mixed = TryReadMixedMediaArray(block);
        if (mixed is { Count: > 0 })
        {
            return mixed;
        }

        var mediaList = new List<WorkMediaItem>();
        var album = AlbumRegex.Match(block);
        if (album.Success)
        {
            var channel = album.Groups[1].Value;
            var id = album.Groups[2].Value;
            var count = int.Parse(album.Groups[3].Value);
            var ext = album.Groups[4].Success ? album.Groups[4].Value : "jpg";
            for (var i = 1; i <= count; i++)
            {
                var slot = i.ToString("00");
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = $"效果图 {slot}",
                    Src = $"{channel}/{id}/{slot}.{ext}"
                });
            }

            return mediaList;
        }

        var listed = ListedRegex.Match(block);
        if (listed.Success)
        {
            foreach (Match entry in ListedEntryRegex.Matches(listed.Groups[1].Value))
            {
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = entry.Groups[2].Value,
                    Src = JsonUtil.ToRel(entry.Groups[1].Value)
                });
            }

            return mediaList;
        }

        var covered = CoveredRegex.Match(block);
        if (covered.Success)
        {
            var channel = covered.Groups[1].Value;
            var id = covered.Groups[2].Value;
            var labels = ParseStringArray(covered.Groups[3].Value);
            for (var i = 0; i < labels.Count; i++)
            {
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = labels[i],
                    Src = i == 0 ? $"{channel}/{id}/01.webp" : null
                });
            }

            return mediaList;
        }

        foreach (Match cover in CoverRegex.Matches(block))
        {
            mediaList.Add(new WorkMediaItem
            {
                Kind = "image",
                Label = cover.Groups[3].Value,
                Src = $"{cover.Groups[1].Value}/{cover.Groups[2].Value}/01.webp"
            });
        }

        foreach (Match images in ImagesRegex.Matches(block))
        {
            foreach (var label in ParseStringArray(images.Groups[1].Value))
            {
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = label
                });
            }
        }

        if (mediaList.Count > 0)
        {
            return mediaList;
        }

        foreach (Match src in SrcRegex.Matches(block))
        {
            mediaList.Add(new WorkMediaItem
            {
                Kind = "image",
                Label = Path.GetFileNameWithoutExtension(src.Groups[1].Value),
                Src = JsonUtil.ToRel(src.Groups[1].Value)
            });
        }

        if (mediaList.Count == 0)
        {
            foreach (Match entry in ListedEntryRegex.Matches(block))
            {
                var src = JsonUtil.ToRel(entry.Groups[1].Value);
                if (!src.Contains('/') || string.IsNullOrWhiteSpace(Path.GetExtension(src)))
                {
                    continue;
                }

                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = entry.Groups[2].Value,
                    Src = src
                });
            }
        }

        return mediaList;
    }

    /// <summary>
    /// 解析作品数据源 <c>media</c>：二元组与对象混排，对象可读 <c>displayName</c> / <c>description</c>。
    /// </summary>
    private static List<WorkMediaItem>? TryReadMixedMediaArray(string block)
    {
        return TryReadNamedArray(block, "media");
    }

    /// <summary>
    /// 按属性名读二元组与对象混排的数组。
    /// </summary>
    private static List<WorkMediaItem>? TryReadNamedArray(string block, string property)
    {
        var head = Regex.Match(block, @"[""']?" + Regex.Escape(property) + @"[""']?\s*:\s*\[");
        if (!head.Success)
        {
            return null;
        }

        var open = block.IndexOf('[', head.Index);
        var close = MatchDelimited(block, open, '[', ']');
        if (close < 0)
        {
            return null;
        }

        var list = ParseMixedMediaEntries(block[(open + 1)..close]);
        return list.Count > 0 ? list : null;
    }

    /// <summary>
    /// 按原文顺序读 <c>["src","label"]</c> 与对象字面量。
    /// </summary>
    private static List<WorkMediaItem> ParseMixedMediaEntries(string inner)
    {
        var mediaList = new List<WorkMediaItem>();
        var i = 0;
        while (i < inner.Length)
        {
            var ch = inner[i];
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                i++;
                continue;
            }

            if (ch == '[')
            {
                var close = MatchDelimited(inner, i, '[', ']');
                if (close < 0)
                {
                    break;
                }

                var match = ListedEntryRegex.Match(inner[i..(close + 1)]);
                if (match.Success)
                {
                    var src = JsonUtil.ToRel(match.Groups[1].Value);
                    mediaList.Add(new WorkMediaItem
                    {
                        Kind = IsVideoSrc(src) ? "video" : "image",
                        Label = match.Groups[2].Value,
                        Src = src
                    });
                }

                i = close + 1;
                continue;
            }

            if (ch == '{')
            {
                var close = MatchDelimited(inner, i, '{', '}');
                if (close < 0)
                {
                    break;
                }

                mediaList.Add(ParseMediaObjectLiteral(inner[(i + 1)..close]));
                i = close + 1;
                continue;
            }

            i++;
        }

        return mediaList;
    }

    /// <summary>
    /// 从对象字面量读 kind / label / src、帧上的类型与自由标签，以及可选资源文案。
    /// </summary>
    private static WorkMediaItem ParseMediaObjectLiteral(string body)
    {
        var src = ParsePropertyString(body, "src");
        var kind = ParseCopyField(body, "kind") ?? "";
        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = IsVideoSrc(src) ? "video" : "image";
        }

        return new WorkMediaItem
        {
            Kind = kind,
            Label = ParseCopyField(body, "label") ?? "",
            Src = src,
            Poster = ParsePropertyString(body, "poster"),
            DisplayName = ParseCopyField(body, "displayName"),
            Description = ParseCopyField(body, "description"),
            Stars = ParseStars(body),
            Themes = ParsePropertyStringArray(body, "themes"),
            Tags = ParsePropertyStringArray(body, "tags")
        };
    }

    /// <summary>
    /// 读取旁路星级表。形象照为 portraitStars，已隐藏资源为 hiddenStars。
    /// </summary>
    private static Dictionary<string, int> ParseNamedStarMap(string block, string property)
    {
        var starDict = new Dictionary<string, int>(StringComparer.Ordinal);
        var head = Regex.Match(block, Regex.Escape(property) + @"\s*:\s*\{");
        if (!head.Success)
        {
            return starDict;
        }

        var open = block.IndexOf('{', head.Index);
        var close = MatchDelimited(block, open, '{', '}');
        if (close < 0)
        {
            return starDict;
        }

        foreach (Match match in Regex.Matches(block[(open + 1)..close], @"""([^""]+)""\s*:\s*([0-9]+)"))
        {
            if (!int.TryParse(match.Groups[2].Value, out var stars) || stars is < 1 or > 5)
            {
                continue;
            }

            starDict[JsonUtil.ToRel(match.Groups[1].Value)] = stars;
        }

        return starDict;
    }

    /// <summary>
    /// 读 1～5 星。缺字段或 0 视为未写。
    /// </summary>
    private static int? ParseStars(string block)
    {
        var match = Regex.Match(block, @"[""']?stars[""']?\s*:\s*([0-9]+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var stars) || stars is < 1 or > 5)
        {
            return null;
        }

        return stars;
    }

    /// <summary>
    /// 读对象字符串字段原文，不按路径规范化。
    /// </summary>
    private static string? ParseCopyField(string block, string property)
    {
        var match = Regex.Match(block, $@"[""']?{Regex.Escape(property)}[""']?\s*:\s*""([^""]*)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 对象键或源路径是否为正式位视频。
    /// </summary>
    private static bool IsVideoSrc(string? src)
    {
        return !string.IsNullOrWhiteSpace(src)
            && src.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析 TypeScript 字符串数组字面量。
    /// </summary>
    private static List<string> ParseStringArray(string body)
    {
        var labelList = new List<string>();
        foreach (Match match in Regex.Matches(body, "\"([^\"]+)\""))
        {
            labelList.Add(match.Groups[1].Value);
        }

        return labelList;
    }

    /// <summary>
    /// 统计数组与主图字段对同一对象的实际引用次数。
    /// </summary>
    private static Dictionary<string, int> CountRefs(IEnumerable<string> srcList, string? primarySrc)
    {
        var refCountDict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var src in srcList.Append(primarySrc ?? "").Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            var normalized = JsonUtil.ToRel(src);
            refCountDict[normalized] = refCountDict.GetValueOrDefault(normalized) + 1;
        }

        return refCountDict;
    }

    /// <summary>
    /// 作品字段只读 media 之前，避免把单张上的类型当成作品级。
    /// </summary>
    private static string HeadBeforeMedia(string block)
    {
        var mediaAt = block.IndexOf("media:", StringComparison.Ordinal);
        return mediaAt < 0 ? block : block[..mediaAt];
    }

    /// <summary>
    /// 读取对象中的字符串属性。
    /// </summary>
    private static string? ParsePropertyString(string block, string property)
    {
        var match = Regex.Match(block, $@"[""']?{Regex.Escape(property)}[""']?\s*:\s*""([^""]+)""");
        return match.Success ? JsonUtil.ToRel(match.Groups[1].Value) : null;
    }

    /// <summary>
    /// 读取对象中的字符串数组属性。
    /// </summary>
    private static List<string> ParsePropertyStringArray(string block, string property)
    {
        var head = Regex.Match(block, $@"[""']?{Regex.Escape(property)}[""']?\s*:\s*\[");
        if (!head.Success)
        {
            return new List<string>();
        }

        var open = block.IndexOf('[', head.Index);
        var close = MatchDelimited(block, open, '[', ']');
        if (close < 0)
        {
            return new List<string>();
        }

        return StringRegex.Matches(block[(open + 1)..close])
            .Select(match => JsonUtil.ToRel(match.Groups[1].Value))
            .ToList();
    }

    /// <summary>
    /// 返回首个存在的字符串数组属性。
    /// </summary>
    private static string? FirstExistingArrayProperty(string block, params string[] propertyList)
    {
        return propertyList.FirstOrDefault(property =>
            Regex.IsMatch(block, $@"[""']?{Regex.Escape(property)}[""']?\s*:\s*\["));
    }

    /// <summary>
    /// 从变量赋值后抽出配对的对象或数组。数组跳过类型里的方括号。
    /// </summary>
    private static string? ExtractAssignedBlock(string text, string marker, char openChar, char closeChar)
    {
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var from = markerIndex + marker.Length;
        var open = openChar == '['
            ? FindAssignedArrayOpen(text, from)
            : text.IndexOf(openChar, from);
        var close = MatchDelimited(text, open, openChar, closeChar);
        return open >= 0 && close > open ? text[open..(close + 1)] : null;
    }

    /// <summary>
    /// 取值数组的 <c>[</c>：跳过 <c>WorkRecord[]</c>、<c>Array&lt;[string, string]&gt;</c> 等类型括号。
    /// </summary>
    private static int FindAssignedArrayOpen(string text, int from)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var seenEquals = false;
        var inString = false;
        var quote = '\0';
        for (var i = from; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (ch == '\\')
                {
                    i++;
                    continue;
                }

                if (ch == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (ch is '"' or '\'' or '`')
            {
                inString = true;
                quote = ch;
                continue;
            }

            if (ch == '<')
            {
                angle++;
                continue;
            }

            if (ch == '>' && angle > 0)
            {
                angle--;
                continue;
            }

            if (angle > 0)
            {
                continue;
            }

            if (ch == '{')
            {
                brace++;
                continue;
            }

            if (ch == '}' && brace > 0)
            {
                brace--;
                continue;
            }

            if (ch == '(')
            {
                paren++;
                continue;
            }

            if (ch == ')' && paren > 0)
            {
                paren--;
                continue;
            }

            if (!seenEquals)
            {
                if (brace == 0 && paren == 0 && ch == '=')
                {
                    seenEquals = true;
                }

                continue;
            }

            if (ch == '[')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 把顶层数组切成对象块。
    /// </summary>
    private static IReadOnlyList<string> SplitTopLevelObjects(string array)
    {
        var blockList = new List<string>();
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] != '{')
            {
                continue;
            }

            var close = MatchDelimited(array, i, '{', '}');
            if (close < 0)
            {
                break;
            }

            blockList.Add(array[i..(close + 1)]);
            i = close;
        }

        return blockList;
    }

    /// <summary>
    /// 匹配括号终点，并跳过字符串内字符。
    /// </summary>
    private static int MatchDelimited(string text, int openIndex, char openChar, char closeChar)
    {
        if (openIndex < 0)
        {
            return -1;
        }

        var depth = 0;
        var quote = '\0';
        var escaped = false;
        for (var i = openIndex; i < text.Length; i++)
        {
            var value = text[i];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (value == '\\')
                {
                    escaped = true;
                }
                else if (value == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (value is '"' or '\'')
            {
                quote = value;
            }
            else if (value == openChar)
            {
                depth++;
            }
            else if (value == closeChar && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
