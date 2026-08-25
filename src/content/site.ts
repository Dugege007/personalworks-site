export type NavItem = {
  id: string;
  label: string;
  labelEn: string;
  path: string;
  theme: string;
};

export type WorkSection = {
  id: string;
  index: string;
  indexEn: string;
  title: string;
  lead: string;
  path: string;
  theme: string;
  frames: string[];
};

export type WorkCollection = {
  id: string;
  title: string;
  lead: string;
  theme: string;
  detailBase: string;
  frames: string[];
  comingSoon?: boolean;
};

export type CategoryRecord = {
  id: string;
  index: string;
  indexEn: string;
  title: string;
  lead: string;
  path: string;
  theme: string;
  homeFrames: string[];
  collections: WorkCollection[];
};

export type GameCard = {
  id: string;
  title: string;
  titleEn: string;
  lead: string;
  accent: string;
  sizeHint: string;
  input: string;
};

export type NoteCard = {
  slug: string;
  title: string;
  date: string;
  summary: string;
  body: string;
  previewImages: string[];
};

export type AboutFeature = {
  id: string;
  title: string;
  lead: string;
  points: string[];
};

export const profile = {
  name: "杜宏博",
  nameEn: "DU HONGBO",
  siteLabel: "层境",
  siteLabelEn: "STRATA",
  identity: "数字孪生 / 景观 / 摄影",
  bio: "风景园林出身，做过四年景观设计，现在做数字孪生与产线仿真。用测绘的方式看世界，也用拍照和手绘把看到的留下来。",
  contacts: [
    { label: "Mail", value: "hello@duhongbo.com", href: "mailto:hello@duhongbo.com", note: "占位，待替换" },
    { label: "Site", value: "duhongbo.com", href: "https://duhongbo.com", note: "备案完成后正式开放" },
  ],
};

export const categories: CategoryRecord[] = [
  {
    id: "twin-sim",
    index: "02",
    indexEn: "TWIN & SIM",
    title: "数字孪生&仿真",
    lead: "把场地与产线收成可量测的层。先看结构，再看运行。",
    path: "/twin-sim",
    theme: "twin",
    homeFrames: ["数字孪生", "产线仿真", "测点归档"],
    collections: [
      {
        id: "twin",
        title: "数字孪生",
        lead: "把场地收成可量测的层。先看结构，再看运行。",
        theme: "twin",
        detailBase: "/twin-sim/twin",
        frames: ["总图叠合", "运行剖切", "测点归档"],
      },
      {
        id: "factory",
        title: "产线仿真",
        lead: "节拍、工位与物流在同一条导轨上对位。",
        theme: "factory",
        detailBase: "/twin-sim/factory",
        frames: ["工位节拍", "物流回路", "瓶颈回放"],
      },
    ],
  },
  {
    id: "games",
    index: "03",
    indexEn: "GAME DEV",
    title: "游戏开发",
    lead: "可玩的展示向小游戏。先选单，再加载，不登录、不付费。",
    path: "/games",
    theme: "games",
    homeFrames: ["选单", "关卡切片", "存档提示"],
    collections: [],
  },
  {
    id: "landscape",
    index: "04",
    indexEn: "LANDSCAPE",
    title: "景观设计",
    lead: "空间先被框住，再被光慢慢填满。图纸只展示已脱敏的选页。",
    path: "/landscape",
    theme: "render",
    homeFrames: ["景观效果图", "景观施工图", "庭院断面"],
    collections: [
      {
        id: "render",
        title: "景观效果图",
        lead: "空间先被框住，再被光慢慢填满。",
        theme: "render",
        detailBase: "/landscape/render",
        frames: ["主视角", "黄昏庭院", "林缘断面"],
      },
      {
        id: "construction",
        title: "景观施工图",
        lead: "选页展示图纸能力。正式图均需脱敏后再入库。",
        theme: "drawing",
        detailBase: "/landscape/construction",
        frames: ["总图选页", "铺装详图", "索引裁切"],
      },
    ],
  },
  {
    id: "photo",
    index: "05",
    indexEn: "PHOTO",
    title: "摄影",
    lead: "把观看停在光线刚好够用的那一瞬。人像只放已授权的肖像。",
    path: "/photo",
    theme: "landscape",
    homeFrames: ["风光摄影", "人像摄影", "游戏摄影"],
    collections: [
      {
        id: "landscape-photo",
        title: "风光摄影",
        lead: "把观看停在光线刚好够用的那一瞬。",
        theme: "landscape",
        detailBase: "/photo/landscape",
        frames: ["山脊", "水面", "雾色"],
      },
      {
        id: "portrait",
        title: "人像摄影",
        lead: "只放已授权的肖像。未授权的照片不会进入生产列表。",
        theme: "portrait",
        detailBase: "/photo/portrait",
        frames: ["留白", "侧光", "静场"],
      },
      {
        id: "game-photo",
        title: "游戏摄影",
        lead: "游戏画面与场景静帧。位置已留，作品待收录。",
        theme: "games",
        detailBase: "/photo/game",
        frames: ["场景", "角色", "光影"],
        comingSoon: true,
      },
    ],
  },
];

export const workSections: WorkSection[] = categories.map((item) => ({
  id: item.id,
  index: item.index,
  indexEn: item.indexEn,
  title: item.title,
  lead: item.lead,
  path: item.path,
  theme: item.theme,
  frames: item.homeFrames,
}));

export const navItems: NavItem[] = [
  { id: "home", label: "主页", labelEn: "HOME", path: "/", theme: "home" },
  { id: "twin-sim", label: "数字孪生&仿真", labelEn: "TWIN & SIM", path: "/twin-sim", theme: "twin" },
  { id: "games", label: "游戏开发", labelEn: "GAMES", path: "/games", theme: "games" },
  { id: "landscape", label: "景观设计", labelEn: "LANDSCAPE", path: "/landscape", theme: "render" },
  { id: "photo", label: "摄影", labelEn: "PHOTO", path: "/photo", theme: "landscape" },
  { id: "notes", label: "心得", labelEn: "NOTES", path: "/notes", theme: "notes" },
  { id: "about", label: "关于", labelEn: "ABOUT", path: "/about", theme: "about" },
];

export const placeholderGames: GameCard[] = [
  {
    id: "sample-ridge",
    title: "脊线",
    titleEn: "RIDGE",
    lead: "在简化地形上走一条还没铺完的路。",
    accent: "#C4A574",
    sizeHint: "约 40MB，首次加载较慢",
    input: "键盘 / 触屏",
  },
  {
    id: "sample-line",
    title: "节拍",
    titleEn: "LINE",
    lead: "一条被放慢的产线，用来看堵塞如何形成。",
    accent: "#C9925A",
    sizeHint: "约 28MB，首次加载较慢",
    input: "鼠标 / 触屏",
  },
  {
    id: "sample-garden",
    title: "借景",
    titleEn: "BORROWED",
    lead: "把窗口外的一层山，框进庭院。",
    accent: "#7BA3A8",
    sizeHint: "约 35MB，首次加载较慢",
    input: "键盘 / 触屏",
  },
];

export const placeholderNotes: NoteCard[] = [
  {
    slug: "sample",
    title: "关于「层」的笔记",
    date: "2026.08.24",
    summary: "园林里的层、孪生里的层、照片里的层，其实是同一种观看。",
    body: "占位正文。园林、孪生与照片里的层，是同一种观看方式。后续用 Markdown 替换。",
    previewImages: ["园中一层", "剖切一层", "底片一层"],
  },
  {
    slug: "sketches",
    title: "手绘",
    date: "2026.08.20",
    summary: "纸面上的测绘与想象，笔触比渲染更早到达。原「手绘」栏目收进这篇笔记。",
    body: "手绘不再作为独立栏目。速写、空间草图与淡彩作为文章图组收录，最多在列表中预览三张。",
    previewImages: ["速写", "空间草图", "水墨淡彩"],
  },
  {
    slug: "measure",
    title: "先量，再写",
    date: "2026.08.01",
    summary: "占位文章。后续替换为真实心得。",
    body: "占位正文。先量场地，再写说明。",
    previewImages: ["尺", "笔记"],
  },
  {
    slug: "quiet-tools",
    title: "工具应当安静",
    date: "2026.07.18",
    summary: "占位文章。仿真软件若一直抢视线，模型就看不见了。",
    body: "占位正文。工具应当安静，把视线留给模型。",
    previewImages: ["界面留白", "模型前景", "日志收起"],
  },
];

export const aboutFeatures: AboutFeature[] = [
  {
    id: "strata",
    title: "层境气质",
    lead: "站点不是热闹展示台，而是一层层走进去的作品场。",
    points: [
      "风格名冻结为层境 STRATA：近黑底、宋体标题、测绘框景。",
      "强调色只用黄昏金、雾青与少量信号橙，不复用任何商业 IP。",
      "滚动等同于在园中行走：先引导，再展开，禁止整页闪切。",
    ],
  },
  {
    id: "ia",
    title: "三层信息架构",
    lead: "先进入门类，再进入细目，避免顶栏被十个平行栏目摊平。",
    points: [
      "首页：认识人、向下探索、四类作品入口。",
      "分类页：数字孪生&仿真、游戏开发、景观设计、摄影。",
      "细分子页：作品详情、游戏选单与游玩、心得文章。",
    ],
  },
  {
    id: "shell",
    title: "壳层与皮肤",
    lead: "顶栏、色板与偏好和内容层剥离，后续换风格不必改页面结构。",
    points: [
      "菜单在标识左侧；右上角为皮肤与语言槽位，栏目走抽屉。",
      "`html[data-skin]` 管全站令牌；`.shell[data-theme]` 只偏置栏目强调色。",
      "皮肤选择写入本地偏好；非法 id 回退默认皮肤「层境」。",
    ],
  },
  {
    id: "motion",
    title: "滚动叙事",
    lead: "主路径必须让访客知道页面还可以往下走。",
    points: [
      "Lenis 平滑滚动 + GSAP ScrollTrigger 做章节揭示。",
      "首批动效五项：颗粒、光斑、文字揭示、滚动引导、进度刻度，均可关闭。",
      "刻度按视口中线判定当前页，避免交界回跳。",
    ],
  },
  {
    id: "assets",
    title: "资源分流",
    lead: "轻量服务器只托管页面，媒体不进 60G 系统盘。",
    points: [
      "SPA 走 Nginx；图片、视频、WebGL 走 COS + CDN。",
      "`VITE_ASSET_BASE` 区分本地占位与线上 CDN。",
      "构建产物不含作品原片；人像须授权，施工图须脱敏。",
    ],
  },
  {
    id: "games-path",
    title: "游戏加载路径",
    lead: "入口页不挂运行时，避免首屏与内存同时失控。",
    points: [
      "栏目页只说明与导览，零 iframe。",
      "选单列出包体提示；进入单款游玩页才加载 WebGL。",
      "存档留在访客浏览器，不登录、不付费、无云同步。",
    ],
  },
  {
    id: "notes",
    title: "心得与手绘",
    lead: "阅读流单独成栏，不插入主页作品走查。",
    points: [
      "列表展示日期、摘要，并从正文抽取最多三张图作预览。",
      "手绘不再作为顶栏栏目，收成心得中的一篇图文。",
      "正文走阅读排版，不再叠加测绘 HUD。",
    ],
  },
  {
    id: "publish",
    title: "内容与上线",
    lead: "访客站保持无登录；有生命周期的内容另走维护者后台方案。",
    points: [
      "当前作品与文章仍在前端内容层，便于对照预览。",
      "后台方案冻结草稿 / 定时 / 发布 / 回滚，媒体直传 COS。",
      "备案完成前域名仅作标识；正式 HTTPS 按部署手册执行。",
    ],
  },
];

/**
 * 按路径前缀匹配栏目；较长路径优先，避免 /games 误伤其他段。
 */
export function findNavByPath(pathname: string): NavItem {
  const ranked = navItems
    .filter((item) => item.path !== "/" && (pathname === item.path || pathname.startsWith(`${item.path}/`)))
    .sort((a, b) => b.path.length - a.path.length);
  return ranked[0] ?? navItems[0];
}

/**
 * 按分类路径查找分类记录。
 */
export function findCategoryByPath(pathname: string): CategoryRecord | undefined {
  return categories.find((item) => pathname === item.path || pathname.startsWith(`${item.path}/`));
}

/**
 * 按详情前缀查找细目集合，供作品详情页回链。
 */
export function findCollectionByPath(pathname: string): WorkCollection | undefined {
  const category = findCategoryByPath(pathname);
  if (!category) {
    return undefined;
  }
  return category.collections
    .filter((item) => pathname === item.detailBase || pathname.startsWith(`${item.detailBase}/`))
    .sort((a, b) => b.detailBase.length - a.detailBase.length)[0];
}

/**
 * 列表预览最多取文章中的三张图。
 */
export function notePreviewImages(note: NoteCard): string[] {
  return note.previewImages.slice(0, 3);
}
