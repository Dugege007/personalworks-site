using System.Text;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Cli;

/// <summary>
/// 意图文件的预演与执行入口，闸门与写盘走 Core。
/// </summary>
public static class Program
{
    /// <summary>
    /// 解析 preview / apply 并打印结果。
    /// </summary>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (!TryParse(args, out var command, out var profilePath, out var intentPath, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(UsageText());
            return IntentFileRunner.ExitUsage;
        }

        var result = command == "apply"
            ? IntentFileRunner.Apply(profilePath, intentPath)
            : IntentFileRunner.Preview(profilePath, intentPath);
        var writer = result.Ok ? Console.Out : Console.Error;
        writer.WriteLine(result.Text.TrimEnd());
        return result.ExitCode;
    }

    /// <summary>
    /// 读取命令与必填路径。
    /// </summary>
    private static bool TryParse(
        string[] args,
        out string command,
        out string profilePath,
        out string intentPath,
        out string error)
    {
        command = "";
        profilePath = "";
        intentPath = "";
        error = "";
        if (args.Length == 0)
        {
            error = "缺少命令。";
            return false;
        }

        command = args[0].Trim().ToLowerInvariant();
        if (command is not ("preview" or "apply"))
        {
            error = "命令须为 preview 或 apply。";
            return false;
        }

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (token is "--profile" or "--intent")
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = token + " 后需要路径。";
                    return false;
                }

                if (token == "--profile")
                {
                    profilePath = args[i + 1];
                }
                else
                {
                    intentPath = args[i + 1];
                }

                i++;
                continue;
            }

            error = "未知参数：" + token;
            return false;
        }

        if (string.IsNullOrWhiteSpace(profilePath) || string.IsNullOrWhiteSpace(intentPath))
        {
            error = "必须同时提供 --profile 与 --intent。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 用法说明。
    /// </summary>
    private static string UsageText()
    {
        return """
            用法：
              SiteMediaStudio.Cli preview --profile <profile.json> --intent <intent.json>
              SiteMediaStudio.Cli apply --profile <profile.json> --intent <intent.json>
            """;
    }
}
