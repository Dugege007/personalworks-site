using System.Windows;
using System.Windows.Threading;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 模态结果关掉后把主窗拉回前面。进度窗自动关时主窗可能已落到其它程序后面，
/// 随后弹出的结果窗会把主窗一起带上；关掉结果窗时系统按弹出前的叠放还原，主窗又会掉到后面。
/// </summary>
internal static class WindowActivation
{
    /// <summary>
    /// 立刻试一次，并在当前消息处理完后再试，避免被系统还原叠放盖掉。
    /// </summary>
    public static void BringToFront(Window? window)
    {
        if (window == null)
        {
            return;
        }

        Raise(window);
        window.Dispatcher.BeginInvoke(() => Raise(window), DispatcherPriority.ApplicationIdle);
    }

    private static void Raise(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Focus();
    }
}
