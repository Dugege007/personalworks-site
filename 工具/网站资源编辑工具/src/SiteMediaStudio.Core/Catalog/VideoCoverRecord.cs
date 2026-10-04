namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机视频封面缓存的元数据。
/// </summary>
public sealed class VideoCoverRecord
{
    public string StageRel { get; set; } = "";
    public double PositionSec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string UpdatedAt { get; set; } = "";
}
