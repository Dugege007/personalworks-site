namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 灯箱缩放下拉的一项：适应窗口或绝对百分比。
/// </summary>
public sealed class LightboxZoomChoice
{
    public LightboxZoomChoice(string label, bool isFit, double scale)
    {
        Label = label;
        IsFit = isFit;
        Scale = scale;
    }

    public string Label { get; }

    public bool IsFit { get; }

    public double Scale { get; }
}
