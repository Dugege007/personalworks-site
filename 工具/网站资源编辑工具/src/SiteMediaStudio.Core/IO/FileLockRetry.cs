namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 短暂的文件占用重试。Windows 上杀毒软件或索引可能在写入后立刻锁住文件，下一次就能通过。
/// </summary>
public static class FileLockRetry
{
    /// <summary>
    /// 同一动作最多尝试的次数，含第一次。五次等待合计 2 秒。
    /// </summary>
    public const int MaxAttempts = 6;

    /// <summary>
    /// 全部失败等待加在一起的上限。权限确实不足时不要把调用方卡住太久。
    /// </summary>
    public static readonly TimeSpan MaxTotalWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 第一次失败后的等待。之后每次倍增，直到用完 <see cref="MaxTotalWait"/>。
    /// </summary>
    public const int FirstWaitMilliseconds = 100;

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
    /// 执行动作。遇到短暂占用时按 100 毫秒起倍增等待，全部等待合计不超过 2 秒，然后原样抛出。
    /// 等待留在调用方的线程上：调用方要等这次写入成功或失败后才能继续，改到别的线程再等回来，窗口卡住的时间一样。
    /// </summary>
    public static void Invoke(Action action)
    {
        Invoke(action, DefaultDelay);
    }

    /// <summary>
    /// 第 failedAttempt 次失败后要等待多久。failedAttempt 从 1 起。
    /// 依次为 100、200、400、800、500 毫秒，合计 2 秒；用完上限后不再等待。
    /// </summary>
    public static TimeSpan DefaultDelay(int failedAttempt)
    {
        if (failedAttempt < 1)
        {
            return TimeSpan.Zero;
        }

        var spent = TimeSpan.Zero;
        for (var index = 1; index < failedAttempt; index++)
        {
            spent += RawDelay(index);
        }

        var remaining = MaxTotalWait - spent;
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var raw = RawDelay(failedAttempt);
        return raw < remaining ? raw : remaining;
    }

    private static TimeSpan RawDelay(int failedAttempt)
    {
        var shift = Math.Min(failedAttempt - 1, 16);
        return TimeSpan.FromMilliseconds(FirstWaitMilliseconds * (1L << shift));
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
