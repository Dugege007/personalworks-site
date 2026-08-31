import { lexicon } from "./lexicon";

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
  titleEn: string;
  titleDeco: string;
  lead: string;
  theme: string;
  detailBase: string;
  frames: string[];
  comingSoon?: boolean;
};

export type CategoryRecord = {
  id: string;
  /** 网页档案号，由顶栏顺序生成，不与文档文件夹绑定。 */
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
  saveMode: string;
  coverSrc: string;
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

export type ContactChannelId =
  | "profileBase"
  | "profileMail"
  | "profilePhone"
  | "profileWechat"
  | "profileQq"
  | "profileGithub";

export type ContactChannel = {
  id: ContactChannelId;
  value?: string;
  href?: string;
  copy?: boolean;
  qrSrc?: string;
};

export const profile = {
  name: "杜宏博",
  nameEn: "DU HONGBO",
  siteLabel: "层境",
  siteLabelEn: "STRATA",
  identity: `${lexicon.digitalTwin.zh} / ${lexicon.landscapeArch.zh} / ${lexicon.photography.zh}`,
  bio: [
    "现在做数字孪生与产线仿真开发，曾做过四年景观设计，自学游戏开发和摄影。",
    "“我抓不住时间，于是我按下快门。”",
  ],
  portraitSrc: "profile/portrait.webp",
  portraitSrcs: [
    "profile/portrait.webp",
    "profile/462A2697.webp",
    "profile/462A2700.webp",
    "profile/462A2703.webp",
    "profile/462A2707.webp",
    "profile/462A2714.webp",
    "profile/462A2817.webp",
    "profile/R0008304.webp",
  ],
  portraitYear: "2024",
  homeHeroSrc: "landscape-photo/sample-1/01.webp",
  homeHeroYear: "2015",
  contactChannels: [
    { id: "profileBase", value: "上海市 青浦区" },
    { id: "profileMail", value: "351080175@qq.com", copy: true },
    { id: "profileWechat", qrSrc: "home-page/wechat-qr.webp" },
    { id: "profileQq", qrSrc: "home-page/qq-qr.webp" },
    { id: "profileGithub", value: "github.com/Dugege007", href: "https://github.com/Dugege007" },
  ] satisfies ContactChannel[],
};

export const navItems: NavItem[] = [
  { id: "home", label: "主页", labelEn: "HOME", path: "/", theme: "home" },
  {
    id: lexicon.twinAndSim.key,
    label: lexicon.twinAndSim.zh,
    labelEn: lexicon.twinAndSim.deco,
    path: `/${lexicon.twinAndSim.key}`,
    theme: lexicon.twinAndSim.key,
  },
  {
    id: lexicon.gameDev.key,
    label: lexicon.gameDev.zh,
    labelEn: lexicon.gameDev.deco,
    path: `/${lexicon.gameDev.key}`,
    theme: lexicon.gameDev.key,
  },
  {
    id: lexicon.landscapeArch.key,
    label: lexicon.landscapeArch.zh,
    labelEn: lexicon.landscapeArch.deco,
    path: `/${lexicon.landscapeArch.key}`,
    theme: lexicon.landscapeArch.key,
  },
  {
    id: lexicon.photography.key,
    label: lexicon.photography.zh,
    labelEn: lexicon.photography.deco,
    path: `/${lexicon.photography.key}`,
    theme: lexicon.photography.key,
  },
  {
    id: lexicon.notes.key,
    label: lexicon.notes.zh,
    labelEn: lexicon.notes.deco,
    path: `/${lexicon.notes.key}`,
    theme: lexicon.notes.key,
  },
  {
    id: lexicon.about.key,
    label: lexicon.about.zh,
    labelEn: lexicon.about.deco,
    path: `/${lexicon.about.key}`,
    theme: lexicon.about.key,
  },
];

/**
 * 将顶栏顺序格式化为两位档案号，仅用于页面展示。
 */
export function padArchiveIndex(order: number): string {
  return String(order).padStart(2, "0");
}

/**
 * 按顶栏当前顺序生成栏目档案号。主页为 00，与文档文件夹无关。
 */
export function archiveIndexByNavId(id: string): string {
  const order = navItems.findIndex((item) => item.id === id);
  return padArchiveIndex(Math.max(0, order));
}

/**
 * 在栏目档案号下生成细目号，例如游戏选单为当前栏目号加 `.1`。
 */
export function archiveSubIndex(parentId: string, subOrder: number): string {
  return `${archiveIndexByNavId(parentId)}.${subOrder}`;
}

/** 作品类栏目。数组顺序须与顶栏对应项一致；档案号由 navItems 生成。 */
export const categories: CategoryRecord[] = [
  {
    id: lexicon.twinAndSim.key,
    index: archiveIndexByNavId(lexicon.twinAndSim.key),
    indexEn: lexicon.twinAndSim.deco,
    title: lexicon.twinAndSim.zh,
    lead: "把场地与产线收成可量测的层。先看结构，再看运行。",
    path: `/${lexicon.twinAndSim.key}`,
    theme: lexicon.twinAndSim.key,
    homeFrames: ["数字孪生", "产线仿真", "测点归档"],
    collections: [
      {
        id: lexicon.digitalTwin.key,
        title: lexicon.digitalTwin.zh,
        titleEn: lexicon.digitalTwin.en,
        titleDeco: lexicon.digitalTwin.deco,
        lead: "把场地收成可量测的层。先看结构，再看运行。",
        theme: lexicon.digitalTwin.key,
        detailBase: `/${lexicon.twinAndSim.key}/${lexicon.digitalTwin.key}`,
        frames: ["总图叠合", "运行剖切", "测点归档"],
      },
      {
        id: lexicon.lineSimulation.key,
        title: lexicon.lineSimulation.zh,
        titleEn: lexicon.lineSimulation.en,
        titleDeco: lexicon.lineSimulation.deco,
        lead: "节拍、工位与物流在同一条导轨上对位。",
        theme: lexicon.lineSimulation.key,
        detailBase: `/${lexicon.twinAndSim.key}/${lexicon.lineSimulation.key}`,
        frames: ["工位节拍", "物流回路", "瓶颈回放"],
      },
    ],
  },
  {
    id: lexicon.gameDev.key,
    index: archiveIndexByNavId(lexicon.gameDev.key),
    indexEn: lexicon.gameDev.deco,
    title: lexicon.gameDev.zh,
    lead: "可玩的展示向小游戏。先选单，再加载，不登录、不付费。",
    path: `/${lexicon.gameDev.key}`,
    theme: lexicon.gameDev.key,
    homeFrames: ["选单", "关卡切片", "存档提示"],
    collections: [],
  },
  {
    id: lexicon.landscapeArch.key,
    index: archiveIndexByNavId(lexicon.landscapeArch.key),
    indexEn: lexicon.landscapeArch.deco,
    title: lexicon.landscapeArch.zh,
    lead: "空间先被框住，再被光慢慢填满。图纸只展示已脱敏的选页。",
    path: `/${lexicon.landscapeArch.key}`,
    theme: lexicon.landscapeArch.key,
    homeFrames: ["景观效果图", "景观施工图", "庭院断面"],
    collections: [
      {
        id: lexicon.landscapeRendering.key,
        title: lexicon.landscapeRendering.zh,
        titleEn: lexicon.landscapeRendering.en,
        titleDeco: lexicon.landscapeRendering.deco,
        lead: "空间先被框住，再被光慢慢填满。",
        theme: lexicon.landscapeRendering.key,
        detailBase: `/${lexicon.landscapeArch.key}/${lexicon.landscapeRendering.key}`,
        frames: ["主视角", "黄昏庭院", "林缘断面"],
      },
      {
        id: lexicon.landscapeCDs.key,
        title: lexicon.landscapeCDs.zh,
        titleEn: lexicon.landscapeCDs.en,
        titleDeco: lexicon.landscapeCDs.deco,
        lead: "选页展示图纸能力。正式图均需脱敏后再入库。",
        theme: lexicon.landscapeCDs.key,
        detailBase: `/${lexicon.landscapeArch.key}/${lexicon.landscapeCDs.key}`,
        frames: ["总图选页", "铺装详图", "索引裁切"],
      },
    ],
  },
  {
    id: lexicon.photography.key,
    index: archiveIndexByNavId(lexicon.photography.key),
    indexEn: lexicon.photography.deco,
    title: lexicon.photography.zh,
    lead: "把观看停在光线刚好够用的那一瞬。人像只放已授权的肖像。",
    path: `/${lexicon.photography.key}`,
    theme: lexicon.photography.key,
    homeFrames: ["风光摄影", "人文摄影", "人像摄影"],
    collections: [
      {
        id: lexicon.landscapePhoto.key,
        title: lexicon.landscapePhoto.zh,
        titleEn: lexicon.landscapePhoto.en,
        titleDeco: lexicon.landscapePhoto.deco,
        lead: "把观看停在光线刚好够用的那一瞬。",
        theme: lexicon.landscapePhoto.key,
        detailBase: `/${lexicon.photography.key}/${lexicon.landscapePhoto.key}`,
        frames: ["山脊", "水面", "雾色"],
      },
      {
        id: lexicon.humanistPhoto.key,
        title: lexicon.humanistPhoto.zh,
        titleEn: lexicon.humanistPhoto.en,
        titleDeco: lexicon.humanistPhoto.deco,
        lead: "把人留在还在发生的现场里，不是摆拍肖像。",
        theme: lexicon.humanistPhoto.key,
        detailBase: `/${lexicon.photography.key}/${lexicon.humanistPhoto.key}`,
        frames: ["街巷", "市集", "渡口"],
      },
      {
        id: lexicon.portraitPhoto.key,
        title: lexicon.portraitPhoto.zh,
        titleEn: lexicon.portraitPhoto.en,
        titleDeco: lexicon.portraitPhoto.deco,
        lead: "只放已授权的肖像。未授权的照片不会进入生产列表。",
        theme: lexicon.portraitPhoto.key,
        detailBase: `/${lexicon.photography.key}/${lexicon.portraitPhoto.key}`,
        frames: ["留白", "侧光", "静场"],
      },
      {
        id: lexicon.gamePhoto.key,
        title: lexicon.gamePhoto.zh,
        titleEn: lexicon.gamePhoto.en,
        titleDeco: lexicon.gamePhoto.deco,
        lead: "游戏画面与场景静帧。位置已留，作品待收录。",
        theme: lexicon.gamePhoto.key,
        detailBase: `/${lexicon.photography.key}/${lexicon.gamePhoto.key}`,
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

export const placeholderGames: GameCard[] = [
  {
    id: "sample-ridge",
    title: "脊线",
    titleEn: "RIDGE",
    lead: "在简化地形上走一条还没铺完的路。",
    accent: "var(--gold)",
    sizeHint: "约 40MB，首次加载较慢",
    input: "键盘 / 触屏",
    saveMode: "仅保存在本机浏览器",
    coverSrc: "game-dev/sample-ridge/01.webp",
  },
  {
    id: "sample-line",
    title: "节拍",
    titleEn: "LINE",
    lead: "一条被放慢的产线，用来看堵塞如何形成。",
    accent: "var(--signal)",
    sizeHint: "约 28MB，首次加载较慢",
    input: "鼠标 / 触屏",
    saveMode: "仅保存在本机浏览器",
    coverSrc: "game-dev/sample-line/01.webp",
  },
  {
    id: "sample-garden",
    title: "借景",
    titleEn: "BORROWED",
    lead: "把窗口外的一层山，框进庭院。",
    accent: "var(--mist)",
    sizeHint: "约 35MB，首次加载较慢",
    input: "键盘 / 触屏",
    saveMode: "仅保存在本机浏览器",
    coverSrc: "game-dev/sample-garden/01.webp",
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
    title: lexicon.sketching.zh,
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
      "构建产物不含作品原片；人像须授权，景观施工图须脱敏。",
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
 * 按路径前缀匹配栏目；较长路径优先，避免短路径误伤其他段。
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
 * 按细目 channel 查找集合，供作品墙与切肤映射回链。
 */
export function findCollectionByChannel(channel: string): WorkCollection | undefined {
  for (const category of categories) {
    const found = category.collections.find((item) => item.id === channel);
    if (found) {
      return found;
    }
  }
  return undefined;
}

/**
 * 按路径解析壳层 `data-theme`：细目页用集合 theme，手绘篇用 sketches，其余走栏目导航。
 */
export function resolveShellTheme(pathname: string): string {
  if (pathname === `/${lexicon.profileResume.key}` || pathname.startsWith(`/${lexicon.profileResume.key}/`)) {
    return lexicon.profileResume.key;
  }
  if (pathname === `/${lexicon.profileSkills.key}` || pathname.startsWith(`/${lexicon.profileSkills.key}/`)) {
    return lexicon.profileSkills.key;
  }
  if (pathname === `/${lexicon.workIndex.key}` || pathname.startsWith(`/${lexicon.workIndex.key}/`)) {
    return lexicon.workIndex.key;
  }
  const catalogPath = `/${lexicon.photography.key}/${lexicon.photoCatalog.key}`;
  if (pathname === catalogPath || pathname.startsWith(`${catalogPath}/`)) {
    return lexicon.photoCatalog.key;
  }
  if (pathname === `/${lexicon.notes.key}/${lexicon.sketching.key}`) {
    return lexicon.sketching.key;
  }
  const collection = findCollectionByPath(pathname);
  if (collection) {
    return collection.theme;
  }
  const gameRoot = `/${lexicon.gameDev.key}`;
  const gameMenu = `${gameRoot}/${lexicon.gameMenu.key}`;
  if (pathname.startsWith(`${gameRoot}/`) && pathname !== gameMenu) {
    return lexicon.gamePlay.key;
  }
  return findNavByPath(pathname).theme;
}

/**
 * 列表预览最多取文章中的三张图。
 */
export function notePreviewImages(note: NoteCard): string[] {
  return note.previewImages.slice(0, 3);
}
