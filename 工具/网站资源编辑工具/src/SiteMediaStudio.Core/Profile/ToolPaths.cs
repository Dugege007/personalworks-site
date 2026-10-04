namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 定位本工具仓库内的解决方案根与默认模拟站。
/// </summary>
public static class ToolPaths
{
    /// <summary>
    /// 自当前基目录向上查找含解决方案的工具根。
    /// </summary>
    public static string? FindToolRoot(string? startDir = null)
    {
        var dir = new DirectoryInfo(startDir ?? AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var sln = Path.Combine(dir.FullName, "SiteMediaStudio.sln");
            if (File.Exists(sln))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// 模拟站配置文件的绝对路径。
    /// </summary>
    public static string? FindFixtureProfile(string? startDir = null)
    {
        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "fixtures", "sample-site", "profile.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// PersonalWorks 预置配置的绝对路径。
    /// </summary>
    public static string? FindPersonalWorksProfile(string? startDir = null)
    {
        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "profiles", "personalworks.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 内容补丁隔离样例的配置路径。
    /// </summary>
    public static string? FindPatchCasesProfile(string? startDir = null)
    {
        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "fixtures", "patch-cases", "profile.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// works.ts 登记隔离夹具的配置路径。
    /// </summary>
    public static string? FindRegisterTsProfile(string? startDir = null)
    {
        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "fixtures", "register-ts", "profile.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 既有 sitemedia 闸门脚本；优先配置里的相对路径，否则取工具旁的生命周期目录。
    /// </summary>
    public static string? FindSitemediaScript(WorkspaceProfile? profile = null, string? startDir = null)
    {
        if (profile != null && !string.IsNullOrWhiteSpace(profile.Cli?.Sitemedia))
        {
            var configured = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.Cli.Sitemedia);
            if (File.Exists(configured))
            {
                return configured;
            }
        }

        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var sibling = Path.GetFullPath(Path.Combine(root, "..", "站点媒体生命周期", "sitemedia.mjs"));
        return File.Exists(sibling) ? sibling : null;
    }

    /// <summary>
    /// 既有压图脚本，与 sitemedia 同目录。
    /// </summary>
    public static string? FindPrepareScript(string? startDir = null)
    {
        var root = FindToolRoot(startDir);
        if (root == null)
        {
            return null;
        }

        var sibling = Path.GetFullPath(Path.Combine(root, "..", "站点媒体生命周期", "prepare-initial-batch.py"));
        return File.Exists(sibling) ? sibling : null;
    }
}
