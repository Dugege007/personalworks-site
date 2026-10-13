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
    /// 压缩心得配图时是否去掉全球定位系统位置。缺省视为开启。
    /// </summary>
    public bool? StripGps { get; set; }

    /// <summary>
    /// 缺省或设置里没有这一项时，压缩会去掉全球定位系统位置。
    /// </summary>
    public bool StripGpsEnabled => StripGps != false;

    /// <summary>
    /// 压缩心得配图时是否保留拍摄参数、拍摄时间和方向信息。缺省视为开启。全球定位系统位置由 <see cref="StripGps"/> 单独控制。
    /// </summary>
    public bool? KeepCaptureParams { get; set; }

    /// <summary>
    /// 缺省或设置里没有这一项时，压缩会保留拍摄参数。
    /// </summary>
    public bool KeepCaptureParamsEnabled => KeepCaptureParams != false;

    /// <summary>
    /// pngquant 可执行文件的路径。空表示本机尚未指定。
    /// </summary>
    public string? PngquantPath { get; set; }

    /// <summary>
    /// oxipng 可执行文件的路径。空表示本机尚未指定。
    /// </summary>
    public string? OxipngPath { get; set; }

    /// <summary>
    /// gifsicle 可执行文件的路径。空表示本机尚未指定。
    /// </summary>
    public string? GifsiclePath { get; set; }

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
    /// 读取本机设置。文件不存在时返回默认值。文件存在但无法解析时抛出异常。
    /// </summary>
    public static AppSettings Load()
    {
        return Load(FilePath);
    }

    /// <summary>
    /// 读取指定设置文件。文件不存在时返回默认值。内容无法解析时抛出带路径的异常。
    /// </summary>
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonUtil.Options);
            if (settings == null)
            {
                throw new AppSettingsException(path, "文件内容不是一份设置对象。");
            }

            return settings;
        }
        catch (JsonException ex)
        {
            throw new AppSettingsException(path, "文件内容不是合法的 JSON。", ex);
        }
    }

    /// <summary>
    /// 写回本机设置。原文件无法解析时拒绝写入。先写同目录临时文件，再替换原文件。
    /// </summary>
    public static void Save(AppSettings settings)
    {
        Save(settings, FilePath);
    }

    /// <summary>
    /// 把设置写到指定路径。原文件无法解析时拒绝写入，避免用新内容覆盖损坏的设置。
    /// </summary>
    public static void Save(AppSettings settings, string path)
    {
        if (File.Exists(path))
        {
            Load(path);
        }

        AtomicTextFile.Write(path, JsonSerializer.Serialize(settings, JsonUtil.Options));
    }
}

/// <summary>
/// 本机设置文件存在，但内容无法解析。此时禁止写入，以免损坏的设置被覆盖。
/// </summary>
public sealed class AppSettingsException : InvalidOperationException
{
    /// <summary>
    /// 界面展示的固定说明。完整异常信息在此之后附上文件路径和原因。
    /// </summary>
    public const string DisplayText = "本机设置损坏，请先修复，工具不会用默认值覆盖它";

    /// <summary>
    /// 无法解析的设置文件路径。
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// 用文件路径和原因说明本机设置已损坏。
    /// </summary>
    public AppSettingsException(string configPath, string reason, Exception? inner = null)
        : base(DisplayText + "：" + configPath + "。" + reason, inner)
    {
        ConfigPath = configPath;
    }
}
