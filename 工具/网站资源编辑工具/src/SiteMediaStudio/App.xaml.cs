using System.Windows;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 应用程序入口。
/// </summary>
public partial class App : Application
{
    public static string? ProfileArg { get; private set; }

    static App()
    {
        MenuPlacement.ApplyAppWide();
    }

    /// <summary>
    /// 解析 --profile 参数。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        for (var i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--profile" && i + 1 < e.Args.Length)
            {
                ProfileArg = e.Args[i + 1];
                break;
            }
        }

        base.OnStartup(e);
    }
}
