using System.Diagnostics;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 外部程序的三级查找和版本探测。可执行脚本只在 Linux 上验证启动和超时。
/// </summary>
public sealed class ExternalToolLocatorTests
{
    [Fact]
    public void ResolvePath_PrefersConfiguredPathThenCommonLocationThenPath()
    {
        using var tree = new TempTree();
        var configured = tree.Touch("configured", "pngquant");
        var common = tree.Touch("common", "pngquant.exe");
        var pathDir = tree.MakeDirectory("on-path");
        var onPath = tree.Touch("on-path", "pngquant");
        var names = new[] { "pngquant.exe", "pngquant" };

        Assert.Equal(configured, ExternalToolLocator.ResolvePath(configured, new[] { common }, names, pathDir));
        Assert.Equal(
            common,
            ExternalToolLocator.ResolvePath(tree.Missing("configured"), new[] { common }, names, pathDir));
        Assert.Equal(common, ExternalToolLocator.ResolvePath(null, new[] { common }, names, pathDir));
        Assert.Equal(common, ExternalToolLocator.ResolvePath("   ", new[] { common }, names, pathDir));
        Assert.Equal(
            onPath,
            ExternalToolLocator.ResolvePath(null, new[] { tree.Missing("common") }, names, pathDir));
    }

    [Fact]
    public void ResolvePath_SearchesEachFileNameAcrossTheWholePathBeforeTheNextName()
    {
        using var tree = new TempTree();
        var firstDir = tree.MakeDirectory("first");
        var secondDir = tree.MakeDirectory("second");
        tree.Touch("first", "pngquant");
        var exe = tree.Touch("second", "pngquant.exe");
        var pathEnv = firstDir + Path.PathSeparator + secondDir;

        var resolved = ExternalToolLocator.ResolvePath(
            null,
            Array.Empty<string>(),
            new[] { "pngquant.exe", "pngquant" },
            pathEnv);

        Assert.Equal(exe, resolved);
    }

    [Fact]
    public void Locate_WhenNothingExists_ReturnsNotFoundWithoutThrowing()
    {
        using var tree = new TempTree();
        var status = ExternalToolLocator.Locate(new ExternalToolRequest
        {
            ConfiguredPath = tree.Missing("configured"),
            CommonPathList = new[] { tree.Missing("common") },
            FileNameList = new[] { "pngquant.exe", "pngquant" },
            PathEnvironment = tree.MakeDirectory("empty")
        });

        Assert.False(status.Found);
        Assert.Null(status.FilePath);
        Assert.Null(status.Version);
        Assert.False(status.VersionTimedOut);
        Assert.Equal(ExternalToolStatus.NotFoundText, status.DisplayText);
    }

    [Fact]
    public void Locate_ReadsVersionFromTheFirstExistingLevel()
    {
        if (!CanRunUnixScripts())
        {
            return;
        }

        using var tree = new TempTree();
        var configured = tree.Script("configured", "pngquant", "from-configured");
        var common = tree.Script("common", "pngquant", "from-common");
        var pathDir = tree.MakeDirectory("on-path");
        tree.Script("on-path", "pngquant", "from-path");
        var names = new[] { "pngquant" };

        var fromConfigured = ExternalToolLocator.Locate(Request(configured, new[] { common }, names, pathDir));
        var fromCommon = ExternalToolLocator.Locate(Request(tree.Missing("nope"), new[] { common }, names, pathDir));
        var fromPath = ExternalToolLocator.Locate(Request(null, new[] { tree.Missing("nope") }, names, pathDir));

        Assert.Equal("from-configured", fromConfigured.Version);
        Assert.Equal(configured, fromConfigured.FilePath);
        Assert.Equal("from-configured", fromConfigured.DisplayText);
        Assert.Equal("from-common", fromCommon.Version);
        Assert.Equal(common, fromCommon.FilePath);
        Assert.Equal("from-path", fromPath.Version);
    }

    [Fact]
    public void Locate_ReadsVersionFromStandardError()
    {
        if (!CanRunUnixScripts())
        {
            return;
        }

        using var tree = new TempTree();
        var script = tree.WriteExecutable(
            "bin",
            "oxipng",
            "#!/bin/sh\nprintf '%s\\n' 'oxipng 9.9.9' >&2\n");

        var status = ExternalToolLocator.Locate(Request(script, Array.Empty<string>(), new[] { "oxipng" }, tree.MakeDirectory("empty")));

        Assert.True(status.Found);
        Assert.Equal("oxipng 9.9.9", status.Version);
        Assert.Equal("oxipng 9.9.9", status.DisplayText);
    }

    [Fact]
    public void Locate_WhenProgramHangs_ReturnsBeforeTheTimeoutWindowExpires()
    {
        if (!CanRunUnixScripts())
        {
            return;
        }

        using var tree = new TempTree();
        var script = tree.WriteExecutable("bin", "gifsicle", "#!/bin/sh\nsleep 30\n");
        var watch = Stopwatch.StartNew();

        var status = ExternalToolLocator.Locate(new ExternalToolRequest
        {
            ConfiguredPath = script,
            CommonPathList = Array.Empty<string>(),
            FileNameList = new[] { "gifsicle" },
            PathEnvironment = tree.MakeDirectory("empty"),
            VersionTimeoutMs = 400
        });

        watch.Stop();
        Assert.True(status.Found);
        Assert.True(status.VersionTimedOut);
        Assert.Null(status.Version);
        Assert.Equal(ExternalToolStatus.VersionUnreadText, status.DisplayText);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "版本探测超时后仍等待了 " + watch.Elapsed);
    }

    [Fact]
    public void LocatePngquant_UsesTheConfiguredExecutable()
    {
        if (!CanRunUnixScripts())
        {
            return;
        }

        using var tree = new TempTree();
        var script = tree.Script("bin", "pngquant", "pngquant 3.1.4");

        var status = ExternalToolLocator.LocatePngquant(script, tree.MakeDirectory("empty"), versionTimeoutMs: 2_000);

        Assert.True(status.Found);
        Assert.Equal(script, status.FilePath);
        Assert.Equal("pngquant 3.1.4", status.DisplayText);
    }

    [Fact]
    public void Locate_WhenFileCannotStart_ReportsUnreadableInsteadOfThrowing()
    {
        using var tree = new TempTree();
        var file = tree.Touch("bin", "pngquant");
        File.WriteAllText(file, "this is not a program");

        var status = ExternalToolLocator.Locate(Request(file, Array.Empty<string>(), new[] { "pngquant" }, tree.MakeDirectory("empty")));

        Assert.True(status.Found);
        Assert.Equal(file, status.FilePath);
        Assert.Equal(ExternalToolStatus.VersionUnreadText, status.DisplayText);
        Assert.False(status.VersionTimedOut);
    }

    /// <summary>
    /// 造一个会打印版本的可执行脚本。调用前须先确认当前系统能运行 shell 脚本。
    /// </summary>
    private static ExternalToolRequest Request(
        string? configuredPath,
        IReadOnlyList<string> commonPathList,
        IReadOnlyList<string> fileNameList,
        string pathEnvironment)
    {
        return new ExternalToolRequest
        {
            ConfiguredPath = configuredPath,
            CommonPathList = commonPathList,
            FileNameList = fileNameList,
            PathEnvironment = pathEnvironment,
            VersionTimeoutMs = 2_000
        };
    }

    /// <summary>
    /// 版本探测依赖可执行的 shell 脚本。Windows 上这些用例直接返回，不造脚本。
    /// </summary>
    private static bool CanRunUnixScripts()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    }

    /// <summary>
    /// 临时目录。测试结束时整棵删掉。
    /// </summary>
    private sealed class TempTree : IDisposable
    {
        private readonly string mRoot = Path.Combine(Path.GetTempPath(), "sms-ext-tool-" + Guid.NewGuid().ToString("N"));

        public TempTree()
        {
            Directory.CreateDirectory(mRoot);
        }

        /// <summary>
        /// 建一个子目录并返回它的路径。
        /// </summary>
        public string MakeDirectory(string name)
        {
            var path = Path.Combine(mRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 写一个空文件，用来验证只看文件是否存在的查找顺序。
        /// </summary>
        public string Touch(string directoryName, string fileName)
        {
            var path = Path.Combine(MakeDirectory(directoryName), fileName);
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }

        /// <summary>
        /// 返回一个肯定不存在的路径。
        /// </summary>
        public string Missing(string name)
        {
            return Path.Combine(mRoot, name, "missing-" + Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// 写一个打印固定版本的可执行脚本。
        /// </summary>
        public string Script(string directoryName, string fileName, string version)
        {
            var body = "#!/bin/sh\nprintf '%s\\n' '" + version.Replace("'", "'\\''") + "'\n";
            return WriteExecutable(directoryName, fileName, body);
        }

        /// <summary>
        /// 写一个可执行脚本。
        /// </summary>
        public string WriteExecutable(string directoryName, string fileName, string script)
        {
            var path = Path.Combine(MakeDirectory(directoryName), fileName);
            File.WriteAllText(path, script.Replace("\r\n", "\n"));
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(mRoot))
                {
                    Directory.Delete(mRoot, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
