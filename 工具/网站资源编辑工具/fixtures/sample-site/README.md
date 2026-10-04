# 模拟站点

隔离回归用的假网站目录。不含 COS、不含真实作品。路径形态与通用 `profile.json` 一致，便于在不碰 PersonalWorks 投放箱的情况下验证编目、标记、闸门与回收站。

| 路径 | 角色 |
|------|------|
| `stage/` | 投放箱 |
| `public/placeholders/` | 正式位 |
| `content/catalog.json` | 站点编目 |
| `content/media-ledger.json` | 台账 |

色块图由 `write-demo-pngs.py` 生成，不取素材站。

文案夹具（供 `copy.update` 回归，不改 `id` / 自动 `label` / `src`）：

- `demo-render` 栏目 `lead` 为空；`demo-photo` 为「演示风光栏目。」。
- `demo-park` 有项目 `summary`；第一张带可选 `displayName` / `description`，第二张未填（灯箱回退项目描述）。
- `demo-yard` 的 `summary` 为空；`demo-walk` 有「步道项目说明。」。

窗口可直接打开本目录，但上页 / 撤下会写 `content/` 与 `public/placeholders/`。隔离回归由测试拷贝副本。污染正本后：`catalog.json` / `media-ledger.json` 去掉已上页的 `03.png`，删除正式位 `demo-render/demo-park/03.png`，中转站 `03.png` 保留。

约定：

- `stage/demo-render/demo-park/01.png`、`02.png` 已上页，回收应被拒绝；正式位各有同名副本。
- 星级夹具：`demo-park/01.png` 为 4 星，`02.png` 为 1 星；`demo-walk/01.png` 不写字段，读侧按 0 星。
- `stage/demo-render/demo-park/03.png`、`04.png` 仅在投放箱，可回收。
- `stage/demo-render/demo-park/clip.mp4` 短视频夹具，可编目与外放；阶段3 前上页拒绝。
- `stage/demo-render/demo-park/old.png` 台账为 withdrawn，可回收。
- `stage/demo-render/stock/keep.png` 为 stock，不可回收。
- `stage/demo-render/leftover/unused.png` 未配对，可回收。
- `stage/demo-render/new-folder/01.png` 未入编作品夹，项目栏出现在「未登记」下，不可当作目标上页。
- `stage/demo-render/studio-a/nested-new/01.png` 位于已登记容器 `studio-a` 下的未入编作品夹；`studio-a` 本身不进「未登记」。
- `stage/demo-render/demo-yard/01.png`、`stage/demo-photo/demo-walk/01.png` 已上页。
