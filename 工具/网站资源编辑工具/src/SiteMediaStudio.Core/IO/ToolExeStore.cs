namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把 Typora 与 PotPlayer 的可执行文件路径记入本机 settings.json。
/// 配置缺失或文件已不在时，先查已知位置，再全盘搜索并回写。
/// </summary>
public static class ToolExeStore
{
    private static readonly string[] PotPlayerNameList =
    {
        "PotPlayerMini64.exe",
        "PotPlayer.exe",
        "PotPlayerMini.exe"
    };

    /// <summary>
    /// 用已知安装位置补上仍空着或已失效的路径，并在有变化时写回设置。不扫全盘。
    /// </summary>
    public static void FillKnown(AppSettings settings)
    {
        var changed = false;
        changed |= Remember(settings, settings.TyporaExe, NoteTypora.Resolve(settings.TyporaExe), value => settings.TyporaExe = value);
        changed |= Remember(
            settings,
            settings.ExternalPlayerPath,
            ExternalPlayer.ResolvePlayerPath(settings.ExternalPlayerPath),
            value => settings.ExternalPlayerPath = value);
        if (changed)
        {
            AppSettingsStore.Save(settings);
        }
    }

    /// <summary>
    /// 解析 Typora。已知位置没有时，searchDisk 为真才扫固定磁盘。找到新路径则写回设置。
    /// </summary>
    public static string? ResolveTypora(AppSettings settings, bool searchDisk)
    {
        var known = NoteTypora.Resolve(settings.TyporaExe);
        if (known != null)
        {
            RememberAndSave(settings, settings.TyporaExe, known, value => settings.TyporaExe = value);
            return known;
        }

        if (!searchDisk)
        {
            return null;
        }

        var found = ExeDiskSearch.FindFirst(new[] { "Typora.exe" });
        RememberAndSave(settings, settings.TyporaExe, found, value => settings.TyporaExe = value);
        return found;
    }

    /// <summary>
    /// 解析 PotPlayer。已知位置没有时，searchDisk 为真才扫固定磁盘。找到新路径则写回设置。
    /// </summary>
    public static string? ResolvePotPlayer(AppSettings settings, bool searchDisk)
    {
        var known = ExternalPlayer.ResolvePlayerPath(settings.ExternalPlayerPath);
        if (known != null)
        {
            RememberAndSave(settings, settings.ExternalPlayerPath, known, value => settings.ExternalPlayerPath = value);
            return known;
        }

        if (!searchDisk)
        {
            return null;
        }

        var found = ExeDiskSearch.FindFirst(PotPlayerNameList);
        RememberAndSave(settings, settings.ExternalPlayerPath, found, value => settings.ExternalPlayerPath = value);
        return found;
    }

    /// <summary>
    /// 路径有变化时写入字段。返回是否改过。
    /// </summary>
    private static bool Remember(AppSettings settings, string? current, string? found, Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(found) || string.Equals(current, found, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        assign(found);
        return true;
    }

    /// <summary>
    /// 路径有变化时写入字段并保存设置。
    /// </summary>
    private static void RememberAndSave(AppSettings settings, string? current, string? found, Action<string> assign)
    {
        if (Remember(settings, current, found, assign))
        {
            AppSettingsStore.Save(settings);
        }
    }
}
