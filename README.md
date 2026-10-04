# PersonalWorks

个人网站，以及把作品放上这个网站的几件本机工具。网页在 `PersonalSite`。改图、登记、上页都走旁边的工具，不在页面源码里手填一串图片路径。

## 目前可以复用

简历页和技能页可以换内容。

简历改 [`文稿/resume.md`](PersonalSite/文稿/resume.md)。版式是简介、经历、教育、技能和语言。某行留空，页面上就不显示。姓名和联系方式不在这份文稿里，沿用站点形象。

技能改 [`文稿/skills.md`](PersonalSite/文稿/skills.md)。工具、领域、分数和熟练度档都在文稿里，现有领域可以删掉换成自己的分组。图标文件放在 `PersonalSite/public/skill-icons/`，文稿里只写文件名。名词表在 [`文稿/glossary.md`](PersonalSite/文稿/glossary.md)；技能名对上名词表时，悬停说明读名词表。

## 还不能直接复用

网站还在开发中。栏目、路由和首页入口按现有这几门职业写在代码里，现在不能直接改成另一个行业的作品集。等整站搭完，会将更多功能模块化，方便其他行业的人增删栏目、放自己的作品。到那一步，这段说明会改掉。

在那之前要改栏目，得会 TypeScript 和 React。栏目的中文名、英文和路径键在 `PersonalSite/src/content/lexicon.ts`，顶栏和栏目正文在 `site.ts`，每个栏目的地址在 `src/App.tsx` 里单独写着。只改显示名，页面仍是原来的种类：摄影有总览，游戏有选单，景观施工图有未脱敏就不能上页的限制。新增或换掉一门，这几处以及首页、中转站目录、编辑工具里的栏目配置要一起改。

维护窗口是另一套程序，C# / WPF / .NET 8。施工图两个小工具是 Python。不改工具本身时，把它们当现成命令用。

## 站点

正在运行的网站：[https://duhongbo.com](https://duhongbo.com)。

访客看到的是作品站：首页、作品、数字孪生与仿真、景观设计、摄影、游戏开发、心得、简历、技能、关于。皮肤可以切换。界面默认中文，英文切换正在开发中。各页路径、预览和发布命令见 [PersonalSite/README.md](PersonalSite/README.md)。

## 素材与发布

图片不进 Git。原片放在 `作品中转站`，文件夹写成「中文（技术键）」，例如 `数字孪生（digital-twin）`。仓库里只留了这些夹和两个示例夹。照片在维护用的电脑上。上页时，网站资源编辑工具把图压成网页用的 WebP，视频压成 MP4 和封面。站点媒体生命周期把成片复制到 `PersonalSite/public/placeholders`，并写上站点里的引用。中转站里的原文件还在。撤下时删的是正式位，不把网图搬回去。

景观施工图要先处理。`PDF转JPG_施工图用` 把单页 PDF 转成 JPG，并按文字或图签转到正向。`JPG脱敏_施工图用` 再盖住图签上的签字栏，源 JPG 不覆盖。两个目录里都有小样，可以不拿真实图纸先跑一遍。

发到公网用的是 `PersonalSite` 里的发布脚本。它按本机的 `.env.deploy` 把静态页送到服务器，把图片同步到对象存储。这份配置不进仓库。`.env.deploy.example` 里只有占位，没有真实地址。克隆之后页面能打开，图是空的。`VITE_ASSET_BASE` 留空时从 `/placeholders` 读图；要看已经发布的图，把它写成资源地址。


## 运行

站点需要 Node.js 20 或以上。在仓库根执行：

```powershell
cd PersonalSite
npm install
npm run dev
```

浏览器打开终端里的地址，默认是 `http://localhost:5173/`。

维护窗口是 Windows 上的 WPF 程序，需要 .NET 8。
不想改正式站点时，用它自带的模拟站 `fixtures/sample-site`。
怎么运行、窗口能做什么，见 [工具/网站资源编辑工具/README.md](工具/网站资源编辑工具/README.md)。
压图入库、PDF 转 JPG 和脱敏的命令在各自目录的 README 里。

## 目录

```text
.
├── PersonalSite/                         站点
│   ├── src/                              页面与内容
│   ├── 文稿/                             简历、技能、名词表
│   └── public/placeholders/              网页正式位。图片不进 Git
├── 工具/
│   ├── 网站资源编辑工具/                  维护窗口，含模拟站
│   ├── 站点媒体生命周期/                  入库与撤下
│   ├── JPG脱敏_施工图用/                  图签脱敏。客户实图不进 Git
│   └── PDF转JPG_施工图用/                 单页施工图 PDF 转 JPG
└── 作品中转站/                           投放目录，只入库空骨架
    ├── 首页（home-page）/
    ├── 形象照（profile）/
    ├── 简历（profile-resume）/
    ├── 技能（profile-skills）/
    ├── 数字孪生（digital-twin）/
    ├── 产线仿真（line-sim）/
    ├── 游戏开发（game-dev）/
    ├── 景观效果图（landscape-rendering）/
    ├── 景观施工图（landscape-cds）/
    ├── 摄影（photo）/
    └── 心得（notes）/
```

> 本站使用 Vibe Coding 的方式搭建，目前使用 Cursor 继续完善功能。