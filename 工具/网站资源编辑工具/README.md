# 网站资源编辑工具

本机 WPF 工作台。默认打开 `fixtures/sample-site/` 模拟站（色块图，不取素材站），避免在开发时改真实投放箱。

## 运行

需要 .NET 8 SDK 与 Windows。

```powershell
cd "工具\网站资源编辑工具"
dotnet run --project "src\SiteMediaStudio\SiteMediaStudio.csproj"
```

指定工作区：

```powershell
dotnet run --project "src\SiteMediaStudio\SiteMediaStudio.csproj" -- --profile ".\profiles\personalworks.json"
```

对话与脚本走同一执行器（不发布、不 `withdraw --apply`）：

```powershell
dotnet run --project "src\SiteMediaStudio.Cli\SiteMediaStudio.Cli.csproj" -- preview --profile ".\profiles\personalworks.json" --intent <intent.json>
dotnet run --project "src\SiteMediaStudio.Cli\SiteMediaStudio.Cli.csproj" -- apply --profile ".\profiles\personalworks.json" --intent <intent.json>
```

或「工作区 → 打开配置…」选择其它站点的 `profile.json`。

## 测试

```powershell
dotnet test "tests\SiteMediaStudio.Tests\SiteMediaStudio.Tests.csproj"
```

`dotnet test` 只生成测试库。窗口须另编：

```powershell
dotnet build "src\SiteMediaStudio\SiteMediaStudio.csproj"
```

或 `dotnet run --project "src\SiteMediaStudio\SiteMediaStudio.csproj"`。编前须退出已开的 `SiteMediaStudio.exe`，否则无法覆盖。

## 当前能力

### 工作区

- `profile.json` 指定投放箱、正式位、编目和栏目。
- PersonalWorks 同时读 `works.ts`、`site.ts`、`initialWorkProjects.ts`、`initialGameProjects.ts`。形象照、真实作品和游戏项目共用站点网格。
- 换一套网站：复制 `fixtures/sample-site/profile.json`，改 `stageRoot`、`placeholdersRoot`、`siteCatalog` 与 `channels`。栏目文件夹名为 key 时用 `stageFolderPattern: Key`；中文（key）时用 `ZhKey`。
- 启动时按上次栏目选中项目树。投放箱有变更时可自动刷新，可在设置里关掉。

### 网格与检视

- 投放箱、站点、对照三页。当前页高亮。切栏目时显示加载提示。
- 网格预览、对照配对、批量标记、导出提示词。投放箱和站点网格按视口物化；已解码的缩略图滚出后仍留着。
- 左右键按行换格。选单空白处可框选。右键有「全选」和「重新选择」，重新选择最多回退 5 次非空选区。
- 空格或双击：图像开窗口内灯箱，视频走 PotPlayer。检视栏拖帧时，优先用 FFmpeg 抽到指定秒并设封面。
- 标记角标带色块边框，卡片变灰时角标不跟着灰。已发布、已隐藏、未脱敏的卡片整张透明度为 0.5。台账状态在界面上用中文。
- 文件大小：1024 KB 及以下写整数 KB，再大写 MB 或 GB，保留两位小数，末尾的 0 不写。

### 登记与认领

- 栏目下还没编进站点的文件夹列在「未登记」。已经点名的容器夹不进组。
- 检视栏可以把文件夹登记成空壳。编目可以是 JSON，也可以是 `works.ts` 里的 `registeredWorks`。登记窗用短标签和问号说明；栏目置灰。内容池可填开始日和地点。
- 已经入编的项目，在左栏右键「编辑项目」。改名后可以确认，再把台账重挂到新路径。
- 项目树选中时只滚动标题行。
- `.site-ready` 里的成片不占格子。已经上页的夹按台账路径认领；原片还可以按台账里的 `sourceStageRel` 认领成片槽位。同题的日期夹按日期对齐。共用一个父夹时，相邻批次各自管自己的资源。
- 细则见 `Docs/工具开发/网站资源编辑工具/投放箱发现与路径漂移.md`。

### 标记与门禁

- 执行模式是滑动开关，默认机械模式（按一套固定流程执行）；可选提示词，以供AI帮忙执行。
- 站点上已隐藏的资源可以再标上页，用来恢复。已经是隐藏时再执行隐藏，这一条跳过。
- 投放箱里已经上页的文件，不能再标上页或回收。残留标记在执行时跳过：不占新对象键，也不写内容层。
- 上页前要在检视栏选定「写入作品」。回收、待撤、发布上线的确认框要勾选「已知后果」。
- 预演按意图分开走。站点上的隐藏和撤下，不因为中转站里还有同名文件就改走投放箱闸门。
- 施工图未脱敏，或目标里有无 `src` 的占位槽，主台出警告条。缩放条可筛未脱敏、已脱敏或全部。未脱敏，或目标含占位槽时，禁止上页。预演被拒绝时窗口不崩。不自动脱敏，也不自动补图。
- 带视频的上页、封面帧和 PotPlayer 见 `Docs/工具开发/网站资源编辑工具/视频素材整理.md`。

### 执行

- 未上页的投放箱文件可以移入回收站。
- 机械隐藏调用 `scripts/content-patch.mjs`，按栏目和作品去掉内容层 `src`。`album` 改为 `listed` 并重排标签。摄影的 `cover` / `covered` 抽掉后写成 `images(...)`。数字孪生和产线里带 `video` 的数组保留视频。
- 机械上页、隐藏、待撤：原图先在旁边压成 WebP，视频先压成网页 MP4 和伴生封面，再交给 `sitemedia.mjs` 入库并打内容补丁。本机不执行 `withdraw --apply`，也不 `deploy`。中转站文件不删。禁止 `prune-assets`。
- PersonalWorks 的正式位只收网页 WebP。上页时调用 `prepare-initial-batch.py` 生成 `.site-ready` 副本，原片保留。施工图质量 20，保持源图的像素和比例；其它栏目按长边规格缩小。
- 上页排队顺序是图、音频、视频、游戏包。新上页未填名称时，`label` 用原片文件名，不写「视频」。没有编码器时，视频预演直接拒绝。
- 「执行」确认后打开进度窗。百分比按预估耗时加权。压图、压码、上页、回收各算一整步；压图和压码进行中按真实输出填写当前步。结束后弹出结果框，列出用到的动作、步骤、短耗时和资源成败，只读可复制，并写入本机 `run.log`。某一条上页成功就清掉该条标记；整批成功后再清其余标记。
- 进度细则见 `Docs/工具开发/网站资源编辑工具/执行进度.md`。

### 发布上线

- 真实站的顺序是 `deploy.mjs`、待撤 apply、`sitemedia purge`。COS 瞬时断线会自动重试。同步 COS 整批算一步，进度按耗时加权。刷新按大约 2 万字符分批，结果只写条数。
- 底栏显示队列是否可发。模拟站，或没有 `.env.deploy` 时，按钮不可用。2026-09-18 已在真实站走通。CDN 权限见 `Docs/运维备忘.md`。

### 文案与排序

- 检视栏可写栏目、项目描述，以及作品资源的名称和描述。回车或失焦写入 `%AppData%/SiteMediaStudio/copy-drafts.json`。没有媒体标记时，未保存的草稿也可以预演和执行。底栏只读草稿，不在判断能否执行时写盘。形象页没有描述栏。
- `copy.update` 经 `content-patch.mjs` 写入内容层，并计入静态包发布。不改 `id`、自动 `label` 和 `src`。编目按顺序读混排对象的 `displayName` 和 `description`。草稿里的已发布快照与内容层对齐。细则见 `Docs/工具开发/网站资源编辑工具/文案编辑.md`。
- 没有排序草稿时，站点网格按投放箱原片路径排序，范围与投放箱一致。多选栏目时，这些栏目铺在同一网格。选中已入编作品且草稿不是空的，可以拖动带 `src` 的卡片改顺序。草稿在 `%AppData%/SiteMediaStudio/reorder-drafts.json`，关掉再开仍然生效。
- 执行 `media.reorder` 把内容层数组顺序写入，并计入静态包发布。草稿清掉后，网格回到路径序。发布上线后，访客看到的画册跟这个数组。新上页的图加在末尾，不再按投放箱路径把整表重排。细则见 `Docs/工具开发/网站资源编辑工具/站点资源排序.md`。

### 本机缓存

- 网格缩略图在程序旁边的 `thumbs/`。「工作区 → 设置…」可查看缩略图路径、文案草稿路径和占用，并可清理缩略图。清理不删除文案草稿。

### 模拟站

开发默认打开 `fixtures/sample-site/` 正本。「未登记 / 登记 / 路径重挂」的测试也用这里，须先拷成隔离副本。窗口里的上页和撤下会改编目、台账和正式位。正本被写脏后，按该目录的 README 把 `03.png` 还原为只在投放箱。
