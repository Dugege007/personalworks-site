using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 读取栏目 / 细目已发布导语。
/// </summary>
public static class ChannelCopyReader
{
    private static readonly Regex LexiconKeyRegex = new(
        @"(\w+)\s*:\s*\{[^}]*?key\s*:\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// JSON 编目根级 <c>channels</c>。
    /// </summary>
    public static IReadOnlyDictionary<string, string> LoadFromJsonCatalog(string catalogFullPath)
    {
        var leadDict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(catalogFullPath))
        {
            return leadDict;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(catalogFullPath));
        if (!doc.RootElement.TryGetProperty("channels", out var channelEl)
            || channelEl.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return leadDict;
        }

        foreach (var item in channelEl.EnumerateArray())
        {
            var key = item.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? keyEl.GetString()
                : null;
            var lead = item.TryGetProperty("lead", out var leadEl) && leadEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? leadEl.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(key))
            {
                leadDict[key] = lead ?? "";
            }
        }

        return leadDict;
    }

    /// <summary>
    /// 从 <c>lexicon.ts</c> 与 <c>site.ts</c> 抽出细目 / 无细目栏目的 <c>lead</c>。
    /// </summary>
    public static IReadOnlyDictionary<string, string> LoadFromSiteTs(string? lexiconFullPath, string? siteFullPath)
    {
        var leadDict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(siteFullPath) || !File.Exists(siteFullPath))
        {
            return leadDict;
        }

        var nameDict = LoadLexiconKeys(lexiconFullPath);
        var siteText = File.ReadAllText(siteFullPath);
        foreach (var pair in nameDict)
        {
            var pattern = $@"id\s*:\s*lexicon\.{Regex.Escape(pair.Key)}\.key[\s\S]{{0,1200}}?lead\s*:\s*""([^""]*)""";
            var match = Regex.Match(siteText, pattern);
            if (match.Success)
            {
                leadDict[pair.Value] = UnescapeJsString(match.Groups[1].Value);
            }
        }

        return leadDict;
    }

    /// <summary>
    /// 读取冻结表短名到 <c>key</c>。
    /// </summary>
    private static Dictionary<string, string> LoadLexiconKeys(string? lexiconFullPath)
    {
        var nameDict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(lexiconFullPath) || !File.Exists(lexiconFullPath))
        {
            return nameDict;
        }

        foreach (Match match in LexiconKeyRegex.Matches(File.ReadAllText(lexiconFullPath)))
        {
            nameDict[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return nameDict;
    }

    /// <summary>
    /// 把 <c>site.ts</c> 字符串字面量里的转义还原成正文。写入用 JSON 转义换行，读回必须同样解开，否则每次执行都会把已写过的导语再当成改动。
    /// </summary>
    private static string UnescapeJsString(string raw)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string>("\"" + raw + "\"") ?? raw;
        }
        catch (System.Text.Json.JsonException)
        {
            return raw;
        }
    }
}
