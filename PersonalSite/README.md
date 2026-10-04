# 个人站点

本目录是站点工程（React + Vite + TypeScript）。仓库根在上一级：这里是网页，旁边是维护工具和中转站目录骨架。

访客默认看到显影 DEVELOP，也可换成层境 STRATA。界面语言默认中文，可切英文。

## 本机预览

需要 Node.js 20 或以上。在仓库根执行：

```powershell
cd PersonalSite
npm install
npm run dev
```

浏览器打开终端给出的地址（默认 `http://localhost:5173/`）。

图片和音视频不进 Git。复制 `.env.example` 为 `.env` 后，`VITE_ASSET_BASE` 留空时页面从 `/placeholders` 读图；克隆下来这些文件不在，对应位置是空的。要看已发布的图，把该变量写成你的资源地址。

## 页面

| 路径 | 内容 |
|------|------|
| `/` | 首页 |
| `/work-index` | 作品 |
| `/twin-sim` | 数字孪生与仿真 |
| `/landscape-arch` | 景观设计 |
| `/photo` | 摄影；照片总览 `/photo/catalog`，主题总览 `/photo/shoots` |
| `/game-dev` | 游戏开发；选单 `/game-dev/menu` |
| `/notes` | 心得 |
| `/profile-resume` | 简历 |
| `/profile-skills` | 技能 |
| `/about` | 关于 |

## 改文字

简历、技能和名词表的 Markdown 在 [`文稿/`](文稿/)。保存后本地预览会热更新。字段怎么写见 [`文稿/README.md`](文稿/README.md)。

心得正文放在 `src/content/notes/`，一篇一个 `.md`，并在同目录 `index.json` 登记。未写入索引的稿不会出现在列表里。

## 常用命令

| 命令 | 作用 |
|------|------|
| `npm run dev` | 本地预览 |
| `npm test` | 跑 `tests/` 下的测试 |
| `npm run build` | 产出 `dist/` |
| `npm run preview` | 预览打包结果 |
| `npm run deploy` | 按本机 `.env.deploy` 构建并发布 |
| 双击 `deploy.bat` | 同上；窗口里输入 `Y` 后开始，结束时停住 |

## 发布

复制 `.env.deploy.example` 为 `.env.deploy`，在本机填写服务器和对象存储。`.env.deploy` 不进 Git。未填写时不要执行发布。

## 同仓库的其它目录

| 目录 | 作用 |
|------|------|
| `工具/网站资源编辑工具` | 维护窗口。不碰正式站点时，用其中的 `fixtures/sample-site` |
| `工具/站点媒体生命周期` | 压图、入库、撤下 |
| `工具/JPG脱敏_施工图用`、`工具/PDF转JPG_施工图用` | 景观施工图转 JPG、脱敏。各自带小样张 |
| `作品中转站` | 投放目录骨架。媒体不进 Git |

设计记录和运维备忘留在本机，不在这个仓库里。
