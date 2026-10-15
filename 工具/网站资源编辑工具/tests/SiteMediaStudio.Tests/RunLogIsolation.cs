using System.Runtime.CompilerServices;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 测试一加载就把运行日志从本机设置文件夹挪开。每条测试第一次写日志时得到自己的临时文件。
/// </summary>
internal static class RunLogIsolation
{
    [ModuleInitializer]
    internal static void IsolateFromMachineSettings()
    {
        RunLog.SetTestPathFactory(CreateTempLog);
    }

    /// <summary>
    /// 生成一个只属于当前测试的日志路径。
    /// </summary>
    private static string CreateTempLog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-runlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "run.log");
    }
}
