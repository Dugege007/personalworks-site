using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机窗口偏好，不进 Git。
/// </summary>
public sealed class AppSettings
{
    public string? LastProfilePath { get; set; }
    public string ExecutionMode { get; set; } = "direct";
    public int ThumbnailPx { get; set; } = 240;
    public string? LastChannel { get; set; }
    public string? LastWorkId { get; set; }
    public bool LastUnregisteredGroup { get; set; }

    /// <summary>
    /// 各工作区项目栏收起的节点键；未列入的节点默认展开。
    /// </summary>
    public Dictionary<string, List<string>>? CollapsedNavByProfile { get; set; }

    /// <summary>
    /// 投放箱变更时是否自动重扫。缺省视为开启。
    /// </summary>
    public bool? WatchStageChanges { get; set; }

    public bool WatchStageEnabled => WatchStageChanges != false;

    /// <summary>
    /// Typora 可执行文件。空则查 PATH，再查本机安装目录。
    /// </summary>
    public string? TyporaExe { get; set; }

    /// <summary>
    /// 外部播放器可执行文件。空则探测 PotPlayer，再退系统关联。
    /// </summary>
    public string? ExternalPlayerPath { get; set; }

    /// <summary>
    /// 本机 FFmpeg。空则查 PATH。
    /// </summary>
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// MediaCoder 命令行（<c>mc.exe</c>）。图形界面不算。
    /// </summary>
    public string? MediaCoderPath { get; set; }

    /// <summary>
    /// 视频上页音频码率，128～192。
    /// </summary>
    public int VideoAudioBitrateKbps { get; set; } = 128;

    /// <summary>
    /// 查看序。缺省或空数组为原序。数组顺序即优先级。
    /// </summary>
    public List<GridSortPreference>? GridSort { get; set; }
}

/// <summary>
/// 读写 %AppData%/SiteMediaStudio/settings.json。
/// </summary>
public static class AppSettingsStore
{
    /// <summary>
    /// 本机设置文件路径。
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    /// <summary>
    /// 读取设置；缺失时返回默认值。
    /// </summary>
    public static AppSettings Load()
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonUtil.Options)
                ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// 写回本机设置。
    /// </summary>
    public static void Save(AppSettings settings)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonUtil.Options));
    }
}
