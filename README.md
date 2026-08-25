# 层境 STRATA · 个人站点

本目录是站点工程（React + Vite + TypeScript）。设计文档在 `Docs/页面模块`。

## 本机预览

环境要求：已安装 Node.js（建议 20 或以上）。

```powershell
cd d:\Du_Work\PersonalWorks\PersonalSite
npm install
npm run dev
```

浏览器打开终端提示的地址（默认 `http://localhost:5173/`）。

更完整的步骤（含宝塔、COS、CDN、备案）见 [`Docs/页面模块/00B-本地开发与部署手册.md`](../Docs/页面模块/00B-本地开发与部署手册.md)。

## 常用命令

| 命令 | 作用 |
|------|------|
| `npm run dev` | 本地开发预览 |
| `npm run build` | 产出 `dist/`，用于上传服务器 |
| `npm run preview` | 预览打包结果 |

## 第一批范围

- 主页：形象占位、简介、联系方式占位、滚动引导、4 段分类入口
- 顶栏 7 个栏目走抽屉；分类页进入细目详情（占位字段与框景）
- 动效：颗粒底、首屏光斑、文字揭示、滚动引导、进度刻度（可用 `--motion-*` 令牌关闭）

真实照片、联系方式与作品图待替换。资源前缀使用环境变量 `VITE_ASSET_BASE`，见 `.env.example`。
