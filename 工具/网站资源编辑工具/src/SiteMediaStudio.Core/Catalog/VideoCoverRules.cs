namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 视频默认封面帧位置。
/// </summary>
public static class VideoCoverRules
{
    public const double DefaultSeconds = 1.0;

    /// <summary>
    /// 时长 ≥ 1s 取第 1 秒；更短取 10% 处。未知时长按 1 秒。
    /// </summary>
    public static double DefaultPositionSec(double? durationSec)
    {
        if (durationSec is null or <= 0)
        {
            return DefaultSeconds;
        }

        if (durationSec.Value >= DefaultSeconds)
        {
            return DefaultSeconds;
        }

        return Math.Max(0, durationSec.Value * 0.1);
    }
}
