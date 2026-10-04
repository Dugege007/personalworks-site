using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 主台脱敏筛的一项。
/// </summary>
public sealed class SenseFilterChoice
{
    public SenseFilterChoice(SenseFilterKind kind, string label)
    {
        Kind = kind;
        Label = label;
    }

    public SenseFilterKind Kind { get; }

    public string Label { get; }
}
