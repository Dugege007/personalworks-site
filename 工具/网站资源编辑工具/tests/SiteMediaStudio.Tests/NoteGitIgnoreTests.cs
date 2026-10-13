using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 核对作品中转站的忽略规则：心得笔记文件夹里的配图进入 Git，其它栏目和附属目录仍然忽略。
/// </summary>
public sealed class NoteGitIgnoreTests
{
    /// <summary>
    /// 没有 Git 时跳过依赖命令行的测试。
    /// </summary>
    public sealed class RequiresGitFactAttribute : FactAttribute
    {
        public RequiresGitFactAttribute()
        {
            if (!GitCommand.IsAvailable)
            {
                Skip = "环境没有 Git，跳过心得配图忽略规则测试。";
            }
        }
    }

    [Fact]
    public void GitIgnore_ImageAllowsPrecedeAuxiliaryExcludes_AndKeepsTextRules()
    {
        var lines = File.ReadAllText(FindStageGitIgnore()).Replace("\r\n", "\n").Split('\n');
        var textRules = new[]
        {
            "!*notes*/**/*.md",
            "!*notes*/**/*.json",
            "!*notes*/*.txt"
        };
        var imageRules = new[]
        {
            "!*notes*/*/**/*.[jJ][pP][gG]",
            "!*notes*/*/**/*.[jJ][pP][eE][gG]",
            "!*notes*/*/**/*.[pP][nN][gG]",
            "!*notes*/*/**/*.[wW][eE][bB][pP]",
            "!*notes*/*/**/*.[gG][iI][fF]"
        };
        var excludeRules = new[]
        {
            "*notes*/**/.site-ready/**",
            "*notes*/**/.originals/**"
        };

        var lastAllow = imageRules.Max(rule => LineIndex(lines, rule));
        var firstExclude = excludeRules.Min(rule => LineIndex(lines, rule));
        Assert.True(firstExclude > lastAllow, "网页成品和原图备份的排除规则必须写在配图放行规则之后。");
        foreach (var rule in textRules)
        {
            Assert.True(LineIndex(lines, rule) < lastAllow, "文字文件的放行规则应保留在配图规则之前：" + rule);
        }
    }

    [RequiresGitFact]
    public void CheckIgnore_BothIgnoreCaseSettings_MatchNoteImageRules()
    {
        var source = FindStageGitIgnore();
        AssertCheckIgnore(source, ignoreCase: false);
        AssertCheckIgnore(source, ignoreCase: true);
    }

    /// <summary>
    /// 在临时仓库中复制真实的忽略文件，并用 git check-ignore 判断每条路径。
    /// </summary>
    private static void AssertCheckIgnore(string sourceGitIgnore, bool ignoreCase)
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-gitignore-" + Guid.NewGuid().ToString("N"));
        try
        {
            var stage = Path.Combine(root, "作品中转站");
            Directory.CreateDirectory(stage);
            File.Copy(sourceGitIgnore, Path.Combine(stage, ".gitignore"));
            GitCommand.Run(root, "init", "-q");
            GitCommand.Run(root, "config", "core.ignorecase", ignoreCase ? "true" : "false");

            var expectations = Expectations();
            var output = GitCommand.Run(
                root,
                new[]
                {
                    "-c", "core.quotepath=off",
                    "-c", "core.ignorecase=" + (ignoreCase ? "true" : "false"),
                    "check-ignore", "-v", "-n", "--no-index", "--"
                }.Concat(expectations.Select(item => item.Path)).ToArray());
            var patterns = ParsePatterns(output);
            foreach (var item in expectations)
            {
                Assert.True(patterns.ContainsKey(item.Path), "core.ignorecase=" + ignoreCase + " 时没有返回：" + item.Path + "\n" + output);
                AssertPattern(ignoreCase, item, patterns[item.Path]);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// 按路径种类核对最后一条命中的规则。以感叹号开头的规则表示放行。
    /// </summary>
    private static void AssertPattern(bool ignoreCase, PathExpect item, string pattern)
    {
        var prefix = "core.ignorecase=" + ignoreCase + "，路径 " + item.Path + "，规则 " + pattern + "。";
        switch (item.Kind)
        {
            case ExpectKind.NoteImage:
                Assert.StartsWith("!", pattern, StringComparison.Ordinal);
                Assert.Contains(item.PatternFragment, pattern, StringComparison.Ordinal);
                break;
            case ExpectKind.IgnoredByStar:
                Assert.Equal("*", pattern);
                break;
            case ExpectKind.Originals:
                Assert.Contains(".originals/", pattern, StringComparison.Ordinal);
                Assert.False(pattern.StartsWith('!'), prefix + "原图备份不应被放行。");
                break;
            case ExpectKind.SiteReady:
                Assert.Contains(".site-ready/", pattern, StringComparison.Ordinal);
                Assert.False(pattern.StartsWith('!'), prefix + "网页成品不应被放行。");
                break;
            case ExpectKind.NoteText:
                Assert.Equal(item.PatternFragment, pattern);
                break;
            default:
                throw new InvalidOperationException("未知的路径种类。");
        }
    }

    /// <summary>
    /// 把 check-ignore 的详细输出解析成「路径 → 最后命中的规则」。
    /// </summary>
    private static Dictionary<string, string> ParsePatterns(string output)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var tab = line.IndexOf('\t');
            Assert.True(tab > 0, "无法解析 check-ignore 输出：" + line);
            var rule = line[..tab];
            var path = line[(tab + 1)..];
            var match = Regex.Match(rule, @"^(?<source>.+):(?<line>\d+):(?<pattern>.*)$");
            Assert.True(match.Success, "无法解析规则来源：" + rule);
            map[path] = match.Groups["pattern"].Value;
        }

        return map;
    }

    /// <summary>
    /// 查找仓库里的作品中转站忽略文件。
    /// </summary>
    private static string FindStageGitIgnore()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "作品中转站", ".gitignore");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("找不到作品中转站/.gitignore。");
    }

    /// <summary>
    /// 返回规则在文件中的行号。找不到时测试失败。
    /// </summary>
    private static int LineIndex(string[] lines, string rule)
    {
        var index = Array.FindIndex(lines, line => line == rule);
        Assert.True(index >= 0, "缺少规则：" + rule);
        return index;
    }

    /// <summary>
    /// 本次要核对的路径。配图只应放行笔记文件夹及其子文件夹。
    /// </summary>
    private static IReadOnlyList<PathExpect> Expectations()
    {
        const string note = "作品中转站/心得（notes）";
        const string folder = note + "/20261005_021341";
        return new PathExpect[]
        {
            new(folder + "/a.png", ExpectKind.NoteImage, "[pP][nN][gG]"),
            new(folder + "/a.PNG", ExpectKind.NoteImage, "[pP][nN][gG]"),
            new(folder + "/a.Png", ExpectKind.NoteImage, "[pP][nN][gG]"),
            new(folder + "/a.jpg", ExpectKind.NoteImage, "[jJ][pP][gG]"),
            new(folder + "/a.JPG", ExpectKind.NoteImage, "[jJ][pP][gG]"),
            new(folder + "/a.jpeg", ExpectKind.NoteImage, "[jJ][pP][eE][gG]"),
            new(folder + "/a.JPEG", ExpectKind.NoteImage, "[jJ][pP][eE][gG]"),
            new(folder + "/a.JpEg", ExpectKind.NoteImage, "[jJ][pP][eE][gG]"),
            new(folder + "/a.webp", ExpectKind.NoteImage, "[wW][eE][bB][pP]"),
            new(folder + "/a.WEBP", ExpectKind.NoteImage, "[wW][eE][bB][pP]"),
            new(folder + "/c.gif", ExpectKind.NoteImage, "[gG][iI][fF]"),
            new(folder + "/c.GIF", ExpectKind.NoteImage, "[gG][iI][fF]"),
            new(folder + "/图/b.jpg", ExpectKind.NoteImage, "[jJ][pP][gG]"),
            new(folder + "/图/b.JPG", ExpectKind.NoteImage, "[jJ][pP][gG]"),
            new(note + "/x.png", ExpectKind.IgnoredByStar, ""),
            new(note + "/x.JPG", ExpectKind.IgnoredByStar, ""),
            new(folder + "/.originals/a.png", ExpectKind.Originals, ""),
            new(folder + "/.originals/图/b.jpg", ExpectKind.Originals, ""),
            new(folder + "/.originals/note.md", ExpectKind.Originals, ""),
            new(folder + "/.originals/keep.json", ExpectKind.Originals, ""),
            new(folder + "/.site-ready/a.webp", ExpectKind.SiteReady, ""),
            new(folder + "/.site-ready/sub/a.txt", ExpectKind.SiteReady, ""),
            new(folder + "/正文.md", ExpectKind.NoteText, "!*notes*/**/*.md"),
            new(folder + "/心得.json", ExpectKind.NoteText, "!*notes*/**/*.json"),
            new(note + "/说明.txt", ExpectKind.NoteText, "!*notes*/*.txt"),
            new(folder + "/说明.txt", ExpectKind.IgnoredByStar, ""),
            new("作品中转站/摄影（photo）/x/a.jpg", ExpectKind.IgnoredByStar, ""),
            new("作品中转站/摄影（photo）/x/a.JPG", ExpectKind.IgnoredByStar, ""),
            new("作品中转站/景观效果图（landscape-rendering）/x/a.png", ExpectKind.IgnoredByStar, ""),
            new("作品中转站/游戏开发（game-dev）/x/clip.mp4", ExpectKind.IgnoredByStar, "")
        };
    }

    private readonly record struct PathExpect(string Path, ExpectKind Kind, string PatternFragment);

    private enum ExpectKind
    {
        NoteImage,
        IgnoredByStar,
        Originals,
        SiteReady,
        NoteText
    }

    /// <summary>
    /// 调用本机 Git。找不到程序时视为不可用。
    /// </summary>
    private static class GitCommand
    {
        /// <summary>
        /// 本机能否启动 Git。
        /// </summary>
        public static bool IsAvailable { get; } = Probe();

        /// <summary>
        /// 在指定目录执行 Git，并返回标准输出。失败时抛出异常。
        /// </summary>
        public static string Run(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 Git。");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && !arguments.Contains("check-ignore"))
            {
                throw new InvalidOperationException("Git 执行失败：" + string.Join(' ', arguments) + "\n" + stderr);
            }

            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                throw new InvalidOperationException("git check-ignore 失败：" + stderr + stdout);
            }

            return stdout;
        }

        /// <summary>
        /// 探测 git --version 是否成功。
        /// </summary>
        private static bool Probe()
        {
            try
            {
                var start = new ProcessStartInfo("git", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var process = Process.Start(start);
                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
