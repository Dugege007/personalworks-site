using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 从 JSON 编目读取作品列表。
/// </summary>
public static class JsonSiteCatalogReader
{
    /// <summary>
    /// 读取通用编目文件。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Load(string catalogFullPath)
    {
        if (!File.Exists(catalogFullPath))
        {
            throw new FileNotFoundException("找不到站点编目。", catalogFullPath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(catalogFullPath));
        if (!doc.RootElement.TryGetProperty("works", out var works) || works.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkCatalogItem>();
        }

        var workList = new List<WorkCatalogItem>();
        foreach (var workEl in works.EnumerateArray())
        {
            var id = ReadString(workEl, "id");
            var channel = ReadString(workEl, "channel");
            var title = ReadString(workEl, "title");
            var summary = ReadString(workEl, "summary") ?? "";
            var year = ReadString(workEl, "year") ?? "";
            var startedOn = ReadString(workEl, "startedOn");
            var place = ReadString(workEl, "place");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(channel))
            {
                continue;
            }

            var mediaList = new List<WorkMediaItem>();
            if (workEl.TryGetProperty("media", out var mediaEl) && mediaEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var media in mediaEl.EnumerateArray())
                {
                    var src = ReadString(media, "src");
                    var label = ReadString(media, "label") ?? "";
                    if (string.IsNullOrWhiteSpace(src) && string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    mediaList.Add(new WorkMediaItem
                    {
                        Kind = ReadString(media, "kind") ?? "image",
                        Label = label,
                        DisplayName = ReadString(media, "displayName"),
                        Description = ReadString(media, "description"),
                        Stars = ReadStars(media),
                        Src = string.IsNullOrWhiteSpace(src) ? null : JsonUtil.ToRel(src),
                        Themes = ReadStringList(media, "themes"),
                        Tags = ReadStringList(media, "tags")
                    });
                }
            }

            var stageFolder = ReadString(workEl, "stageFolder");
            var work = new WorkCatalogItem
            {
                Id = id,
                Channel = channel,
                Title = title ?? id,
                Summary = summary,
                StageFolder = string.IsNullOrWhiteSpace(stageFolder) ? null : JsonUtil.ToRel(stageFolder),
                Year = year,
                StartedOn = string.IsNullOrWhiteSpace(startedOn) ? null : startedOn,
                Place = string.IsNullOrWhiteSpace(place) ? null : place,
                Themes = ReadStringList(workEl, "themes"),
                Tags = ReadStringList(workEl, "tags"),
                Media = mediaList
            };
            if (workEl.TryGetProperty("hiddenStars", out var hiddenStars) && hiddenStars.ValueKind == JsonValueKind.Object)
            {
                foreach (var pair in hiddenStars.EnumerateObject())
                {
                    if (pair.Value.TryGetInt32(out var stars) && stars is >= 1 and <= 5)
                    {
                        work.ParkedStarDict[JsonUtil.ToRel(pair.Name)] = stars;
                    }
                }
            }

            workList.Add(work);
        }

        return workList;
    }

    /// <summary>
    /// 读取字符串数组；没有该字段则空。
    /// </summary>
    private static IReadOnlyList<string> ReadStringList(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => item.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 读取 1～5 星。缺字段、0 或越界视为未写。
    /// </summary>
    private static int? ReadStars(JsonElement element)
    {
        if (!element.TryGetProperty("stars", out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var stars) && stars is >= 1 and <= 5 ? stars : null;
    }

    /// <summary>
    /// 读取可选字符串属性。
    /// </summary>
    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
