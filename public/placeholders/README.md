# 本地占位媒体

目录名即冻结表 `key`，与 COS 前缀、作品 `channel`、路由细目段一致。

入库来源：`作品中转站/中文（key）/` 下相对路径 → 本目录对应 `{key}/…`（**复制，并登记台账**）。中转站文件保留，由人工补充。是否使用、是否复用以台账与内容层 `src` 为准。下架删本目录文件与 COS 该键，不移回中转站、不删中转站文件；再用时中转站仍有该路径则直接入库，缺失则人工放回，见 `Docs/工具开发/站点媒体生命周期.md`。上线后同一 `{key}` 前缀传到 COS。本流程不读仓库外原片库。

| 目录 | 中文 |
|------|------|
| `home-page/` | 首页（二维码、公安备案徽章） |
| `profile/` | 形象 |
| `profile-resume/` | 简历 |
| `profile-skills/` | 技能 |
| `digital-twin/` | 数字孪生 |
| `line-sim/` | 产线仿真 |
| `landscape-rendering/` | 景观效果图 |
| `landscape-cds/` | 景观施工图 |
| `landscape-photo/` | 风光摄影 |
| `humanist-photo/` | 人文摄影 |
| `portrait-photo/` | 人像摄影 |
| `game-photo/` | 游戏摄影 |
| `{key}/stock/` | 该栏目常驻占位，三张 `01.webp`～`03.webp`；正式入库不覆盖 |
| `notes/` | 心得 |
| `game-dev/` | 游戏开发 |

游戏 WebGL 模板：`game-dev/_id/webgl/`。媒体文件本身不进版本库。

显影首页头图、切片与精选封面目前可用网图占位。素材站名单见 `Docs/页面模块/00 设计大纲/00A-视觉与动效系统.md` 3.8。每张从网上取的图须把**该照片页** URL 写入 `Docs/参考资料/来路登记.md`，禁止只写站首页。正式入库后按「中文（key）」复制覆盖 `{key}/{id}/` 同名 `webp`，不要覆盖 `{key}/stock/`。缺图时页面回退到 stock。
