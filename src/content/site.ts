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

export const navItems: NavItem[] = [
  { id: "home", label: "主页", labelEn: "HOME", path: "/", theme: "home" },
  { id: "digital-twin", label: "数字孪生", labelEn: "TWIN", path: "/digital-twin", theme: "twin" },
  { id: "factory-sim", label: "产线仿真", labelEn: "FACTORY", path: "/factory-sim", theme: "factory" },
  { id: "landscape-render", label: "景观效果图", labelEn: "RENDER", path: "/landscape-render", theme: "render" },
  { id: "construction", label: "施工图", labelEn: "DRAWING", path: "/construction", theme: "drawing" },
  { id: "landscape-photo", label: "风光摄影", labelEn: "LANDSCAPE", path: "/landscape-photo", theme: "landscape" },
  { id: "portrait", label: "人像摄影", labelEn: "PORTRAIT", path: "/portrait", theme: "portrait" },
  { id: "games", label: "游戏开发", labelEn: "GAMES", path: "/games", theme: "games" },
  { id: "sketch", label: "手绘", labelEn: "SKETCH", path: "/sketch", theme: "sketch" },
  { id: "notes", label: "心得", labelEn: "NOTES", path: "/notes", theme: "notes" },
];

export const workSections: WorkSection[] = [
  {
    id: "digital-twin",
    index: "01",
    indexEn: "DIGITAL TWIN",
    title: "数字孪生",
    lead: "把场地与产线收成可量测的层。先看结构，再看运行。",
    path: "/digital-twin",
    theme: "twin",
    frames: ["总图叠合", "运行剖切", "测点归档"],
  },
  {
    id: "factory-sim",
    index: "02",
    indexEn: "FACTORY SIM",
    title: "产线仿真",
    lead: "节拍、工位与物流在同一条导轨上对位。",
    path: "/factory-sim",
    theme: "factory",
    frames: ["工位节拍", "物流回路", "瓶颈回放"],
  },
  {
    id: "landscape-render",
    index: "03",
    indexEn: "LANDSCAPE RENDER",
    title: "景观效果图",
    lead: "空间先被框住，再被光慢慢填满。",
    path: "/landscape-render",
    theme: "render",
    frames: ["主视角", "黄昏庭院", "林缘断面"],
  },
  {
    id: "construction",
    index: "04",
    indexEn: "CONSTRUCTION",
    title: "施工图",
    lead: "选页展示图纸能力。正式图均需脱敏后再入库。",
    path: "/construction",
    theme: "drawing",
    frames: ["总图选页", "铺装详图", "索引裁切"],
  },
  {
    id: "landscape-photo",
    index: "05",
    indexEn: "LANDSCAPE PHOTO",
    title: "风光摄影",
    lead: "把观看停在光线刚好够用的那一瞬。",
    path: "/landscape-photo",
    theme: "landscape",
    frames: ["山脊", "水面", "雾色"],
  },
  {
    id: "portrait",
    index: "06",
    indexEn: "PORTRAIT",
    title: "人像摄影",
    lead: "只放已授权的肖像。未授权的照片不会进入生产列表。",
    path: "/portrait",
    theme: "portrait",
    frames: ["留白", "侧光", "静场"],
  },
  {
    id: "games",
    index: "07",
    indexEn: "GAME DEV",
    title: "游戏开发",
    lead: "可玩的展示向小游戏。先选单，再加载，不登录、不付费。",
    path: "/games",
    theme: "games",
    frames: ["选单", "关卡切片", "存档提示"],
  },
  {
    id: "sketch",
    index: "08",
    indexEn: "SKETCH",
    title: "手绘",
    lead: "纸面上的测绘与想象，笔触比渲染更早到达。",
    path: "/sketch",
    theme: "sketch",
    frames: ["速写", "空间草图", "水墨淡彩"],
  },
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
  },
  {
    slug: "measure",
    title: "先量，再写",
    date: "2026.08.01",
    summary: "占位文章。后续替换为真实心得。",
  },
  {
    slug: "quiet-tools",
    title: "工具应当安静",
    date: "2026.07.18",
    summary: "占位文章。仿真软件若一直抢视线，模型就看不见了。",
  },
];

export function findNavByPath(pathname: string): NavItem {
  const exact = navItems.find((item) => item.path !== "/" && pathname.startsWith(item.path));
  return exact ?? navItems[0];
}
