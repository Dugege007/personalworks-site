namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 查找本机 Typora，供双击正文时启动。
/// </summary>
public static class NoteTypora
{
    /// <summary>
    /// 先用配置路径，再查 PATH，再查用户目录与 Program Files 下的 Typora。找不到则空。
    /// </summary>
    public static string? Resolve(string? configured)
    {
        if (IsFile(configured))
        {
            return configured;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "Typora.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var root in InstallRoots())
        {
            var installed = Path.Combine(root, "Typora", "Typora.exe");
            if (File.Exists(installed))
            {
                return installed;
            }
        }

        return null;
    }

    /// <summary>
    /// 常见安装根：用户目录、64 位 Program Files、32 位 Program Files。
    /// </summary>
    private static IEnumerable<string> InstallRoots()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return programFiles;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86)
            && !string.Equals(programFilesX86, programFiles, StringComparison.OrdinalIgnoreCase))
        {
            yield return programFilesX86;
        }
    }

    private static bool IsFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
