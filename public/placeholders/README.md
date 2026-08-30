# 本地占位媒体

目录名即冻结表 `key`，与 COS 前缀、作品 `channel`、路由细目段一致。

入库来源：`作品中转站/中文（key）/{id}/` → 本目录 `{key}/{id}/`（**移动，不复制**）。中转站清空表示该批已入库。原片库另存。是否使用、是否复用以内容层 `src` 为准。上线后同一 `{key}` 前缀传到 COS。

| 目录 | 中文 |
|------|------|
| `home-page/` | 首页 |
| `profile/` | 形象 |
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

显影首页头图、切片与精选封面目前可用网图占位。素材站名单见 `Docs/页面模块/00 设计大纲/00A-视觉与动效系统.md` 3.8（Unsplash / Pexels / Wikimedia Commons / 0 A.D. 官网截图）。正式入库后按「中文（key）」移动覆盖 `{key}/{id}/` 同名 `webp`，不要覆盖 `{key}/stock/`。缺图时页面回退到 stock。
