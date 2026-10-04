using System.Text.Json.Serialization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一份可编辑网站工作区的路径与栏目映射。
/// </summary>
public sealed class WorkspaceProfile
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";

    /// <summary>
    /// 相对本配置文件的工作区根；空则使用配置文件所在目录。
    /// </summary>
    public string? Root { get; set; }

    public string StageRoot { get; set; } = "stage";

    /// <summary>
    /// Key：栏目目录名为 key；ZhKey：中文（key）。
    /// </summary>
    public string StageFolderPattern { get; set; } = "Key";

    public string PlaceholdersRoot { get; set; } = "public/placeholders";
    public string? LedgerPath { get; set; }
    public SiteCatalogConfig SiteCatalog { get; set; } = new();
    public List<ChannelProfile> Channels { get; set; } = new();
    public CliConfig? Cli { get; set; }

    [JsonIgnore]
    public string ResolvedRoot { get; set; } = "";

    [JsonIgnore]
    public string ProfilePath { get; set; } = "";
}

/// <summary>
/// 站点编目来源。
/// </summary>
public sealed class SiteCatalogConfig
{
    /// <summary>
    /// json 或 personalworks-ts。
    /// </summary>
    public string Kind { get; set; } = "json";
    public string Path { get; set; } = "content/catalog.json";

    /// <summary>
    /// PersonalWorks 的站点级内容源；为空时只读取 works.ts。
    /// </summary>
    public string? SitePath { get; set; }

    /// <summary>
    /// works.ts 导入的显式作品数据源；为空时作品直接位于 works.ts。
    /// </summary>
    public string? WorkDataPath { get; set; }

    /// <summary>
    /// site.ts 导入的游戏项目数据源；为空时游戏直接位于 site.ts。
    /// </summary>
    public string? GameDataPath { get; set; }
}

/// <summary>
/// 左侧目录中的一个栏目。
/// </summary>
public sealed class ChannelProfile
{
    public string Key { get; set; } = "";
    public string Zh { get; set; } = "";
    public string En { get; set; } = "";
    public string Deco { get; set; } = "";
    public ChannelCapabilities Capabilities { get; set; } = new();
    public string ObjectKeyPattern { get; set; } = "work-numbered";

    /// <summary>
    /// 栏目下不作为作品的公司 / 类别夹；其下一层才是作品夹。未列入的第一层仍按作品夹识别。
    /// </summary>
    public List<string> StageContainerFolders { get; set; } = new();

    /// <summary>
    /// 为真时，栏目根下整段四位数字夹是年份容器，不是作品。不必把年份写入容器名单。
    /// </summary>
    public bool YearContainers { get; set; }

    /// <summary>
    /// 栏目夹在投放箱根下的父目录，如 <c>摄影（photo）</c>。空则栏目夹直接位于投放箱根。
    /// </summary>
    public string? StageParent { get; set; }
}

/// <summary>
/// 单个栏目的工具能力；默认保持既有配置兼容。
/// </summary>
public sealed class ChannelCapabilities
{
    public bool Catalog { get; set; } = true;
    public bool Ingest { get; set; } = true;
    public bool Hide { get; set; } = true;
    public bool Withdraw { get; set; } = true;
}

/// <summary>
/// 后续机械编排用的外部命令位置。
/// </summary>
public sealed class CliConfig
{
    public string? Sitemedia { get; set; }
    public string? DeployCwd { get; set; }
}
