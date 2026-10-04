using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本工具共用的 JSON 选项。
/// </summary>
public static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>
    /// 生成驼峰、允许尾逗号的序列化选项。
    /// </summary>
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return options;
    }

    /// <summary>
    /// 把相对路径统一成正斜杠。
    /// </summary>
    public static string ToRel(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    /// <summary>
    /// 无 BOM 的 UTF-8，供意图文件交给 Node 解析。
    /// </summary>
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
}
