namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 闸门判定结果。
/// </summary>
public sealed class GateDecision
{
    public bool Allowed { get; }
    public bool IsHardError { get; }
    public bool IsWarn { get; }
    public string Message { get; }

    private GateDecision(bool allowed, bool isHardError, bool isWarn, string message)
    {
        Allowed = allowed;
        IsHardError = isHardError;
        IsWarn = isWarn;
        Message = message;
    }

    /// <summary>
    /// 允许执行。
    /// </summary>
    public static GateDecision Allow(string message = "")
    {
        return new GateDecision(true, false, false, message);
    }

    /// <summary>
    /// 硬拒绝，批次不可继续该条。
    /// </summary>
    public static GateDecision Reject(string message)
    {
        return new GateDecision(false, true, false, message);
    }

    /// <summary>
    /// 软警告，预演列出但默认仍可导出提示词。
    /// </summary>
    public static GateDecision Warn(string message)
    {
        return new GateDecision(true, false, true, message);
    }
}
