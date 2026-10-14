namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 短暂的文件占用重试。Windows 上杀毒软件或索引可能在写入后立刻锁住文件，下一次就能通过。
/// </summary>
public static class FileLockRetry
{
    /// <summary>
    /// 同一动作最多尝试的次数，含第一次。
    /// </summary>
    public const int MaxAttempts = 8;

    /// <summary>
    /// 异常是否像文件正被别的程序占用。权限不足也算在内，因为 Windows 经常用它表示共享冲突。
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return true;
        }

        if (exception is not IOException)
        {
            return false;
        }

        // Win32：5 拒绝访问，32 共享冲突，33 锁定冲突，1175 替换已完成但旧文件删不掉。
        var code = exception.HResult & 0xFFFF;
        return code is 5 or 32 or 33 or 1175;
    }

    /// <summary>
    /// 执行动作。遇到短暂占用时按 100 毫秒起倍增等待，最多约 2 秒，然后原样抛出。
    /// </summary>
    public static void Invoke(Action action)
    {
        Invoke(action, attempt => TimeSpan.FromMilliseconds(Math.Min(2000, 100 * (1 << (attempt - 1)))));
    }

    /// <summary>
    /// 执行动作。delay 的参数是已经失败的次数，从 1 起。
    /// </summary>
    public static void Invoke(Action action, Func<int, TimeSpan> delay)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (delay == null)
        {
            throw new ArgumentNullException(nameof(delay));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception exception) when (IsTransient(exception) && attempt < MaxAttempts)
            {
                var wait = delay(attempt);
                if (wait > TimeSpan.Zero)
                {
                    Thread.Sleep(wait);
                }
            }
        }
    }
}
