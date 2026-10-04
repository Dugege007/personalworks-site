using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 调用 content-patch.mjs 做预演或写盘。
/// </summary>
public static class ContentPatchClient
{
    /// <summary>
    /// 只读预演；不写内容层。
    /// </summary>
    public static ContentPatchRunResult DryRun(string profilePath, string intentPath)
    {
        return Invoke(profilePath, intentPath, apply: false);
    }

    /// <summary>
    /// 写盘；失败时脚本负责还原内容层，意图文件由调用方保留。
    /// </summary>
    public static ContentPatchRunResult Apply(string profilePath, string intentPath)
    {
        return Invoke(profilePath, intentPath, apply: true);
    }

    /// <summary>
    /// 发布前整理待撤键：正文已不再引用的，从尺寸表和 Exif 去掉；正文仍引用的则失败，且不改这些键。
    /// </summary>
    public static ContentPatchRunResult TidySatellites(string profilePath, IReadOnlyList<string> objectKeyList)
    {
        var script = FindScript();
        if (script == null)
        {
            throw new FileNotFoundException("找不到 content-patch.mjs。");
        }

        var keysPath = Path.Combine(Path.GetTempPath(), "sms-tidy-keys-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(keysPath, JsonSerializer.Serialize(objectKeyList, JsonUtil.Options), JsonUtil.Utf8NoBom);
        try
        {
            var argumentList = new List<string>
            {
                "--tidy-satellites",
                "--profile",
                profilePath,
                "--keys",
                keysPath,
                "--apply"
            };
            var cwd = Path.GetDirectoryName(script) ?? Directory.GetCurrentDirectory();
            return ParseRun(NodeHost.Run(script, argumentList, cwd));
        }
        finally
        {
            try
            {
                File.Delete(keysPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 定位工具目录内的补丁脚本。
    /// </summary>
    public static string? FindScript(string? startDir = null)
    {
        var root = ToolPaths.FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "scripts", "content-patch.mjs");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 启动补丁脚本并解析 stdout JSON。
    /// </summary>
    private static ContentPatchRunResult Invoke(string profilePath, string intentPath, bool apply)
    {
        var script = FindScript();
        if (script == null)
        {
            throw new FileNotFoundException("找不到 content-patch.mjs。");
        }

        var argumentList = new List<string>
        {
            "--intent",
            intentPath,
            "--profile",
            profilePath
        };
        if (apply)
        {
            argumentList.Add("--apply");
        }
        else
        {
            argumentList.Add("--dry-run");
        }

        var cwd = Path.GetDirectoryName(script) ?? Directory.GetCurrentDirectory();
        return ParseRun(NodeHost.Run(script, argumentList, cwd));
    }

    /// <summary>
    /// 把脚本 stdout 收成一次调用结果。
    /// </summary>
    private static ContentPatchRunResult ParseRun(NodeRunResult run)
    {
        ContentPatchScriptReport? report = null;
        if (!string.IsNullOrWhiteSpace(run.StdOut))
        {
            try
            {
                report = JsonSerializer.Deserialize<ContentPatchScriptReport>(run.StdOut, JsonUtil.Options);
            }
            catch (JsonException)
            {
                report = null;
            }
        }

        return new ContentPatchRunResult
        {
            Ok = run.Ok && (report == null || report.Ok),
            ExitCode = run.ExitCode,
            StdOut = run.StdOut,
            StdErr = run.StdErr,
            Report = report
        };
    }
}

/// <summary>
/// 补丁脚本一次调用的汇总。
/// </summary>
public sealed class ContentPatchRunResult
{
    public bool Ok { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public ContentPatchScriptReport? Report { get; init; }

    /// <summary>
    /// 给窗口看的失败说明。
    /// </summary>
    public string FailureText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Report?.Error))
            {
                return Report.Error;
            }

            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                return StdErr;
            }

            return string.IsNullOrWhiteSpace(StdOut) ? $"content-patch 退出码 {ExitCode}。" : StdOut;
        }
    }
}

/// <summary>
/// content-patch.mjs 打印的 JSON。
/// </summary>
public sealed class ContentPatchScriptReport
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public bool Restored { get; set; }
    public string? CatalogPath { get; set; }
    public List<string> Stripped { get; set; } = new();
    public List<ContentPatchScriptChange> Changes { get; set; } = new();
}

/// <summary>
/// 脚本报告中的单条作品改写。
/// </summary>
public sealed class ContentPatchScriptChange
{
    public string WorkId { get; set; } = "";
    public string KindBefore { get; set; } = "";
    public string KindAfter { get; set; } = "";
    public List<string> Removed { get; set; } = new();
    public List<string> Added { get; set; } = new();
    public string? Before { get; set; }
    public string? After { get; set; }
}
