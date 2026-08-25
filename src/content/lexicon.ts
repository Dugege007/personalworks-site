/**
 * 中英术语冻结表。
 * `en`：正文与语言切换；`deco`：顶栏副标与档案编号。
 * `key`：路由段、COS 前缀、`public/placeholders` 目录名、中转站括号内键。
 * `key` 为小写短横线简写，须带领域前缀，禁止占用通用词（如 render、construction、games）。
 */
export type LexiconEntry = {
  zh: string;
  en: string;
  deco: string;
  key: string;
};

export const lexicon = {
  digitalTwin: {
    zh: "数字孪生",
    en: "Digital Twin",
    deco: "DIGITAL TWIN",
    key: "digital-twin",
  },
  plantSimulation: {
    zh: "工厂仿真",
    en: "Plant Simulation",
    deco: "PLANT SIM",
    key: "plant-sim",
  },
  lineSimulation: {
    zh: "产线仿真",
    en: "Production Line Simulation",
    deco: "LINE SIM",
    key: "line-sim",
  },
  twinAndSim: {
    zh: "数字孪生&仿真",
    en: "Digital Twin & Simulation",
    deco: "DIGITAL TWIN & SIM",
    key: "twin-sim",
  },
  landscapeArch: {
    zh: "景观设计",
    en: "Landscape Architecture",
    deco: "LANDSCAPE ARCH",
    key: "landscape-arch",
  },
  landscapeRendering: {
    zh: "景观效果图",
    en: "Landscape Rendering",
    deco: "L. RENDER",
    key: "landscape-rendering",
  },
  landscapeCDs: {
    zh: "景观施工图",
    en: "Landscape Construction Drawings",
    deco: "L. CDS",
    key: "landscape-cds",
  },
  photography: { zh: "摄影", en: "Photography", deco: "PHOTO", key: "photo" },
  landscapePhoto: {
    zh: "风光摄影",
    en: "Landscape Photography",
    deco: "L. PHOTO",
    key: "landscape-photo",
  },
  portraitPhoto: {
    zh: "人像摄影",
    en: "Portrait Photography",
    deco: "PORTRAIT",
    key: "portrait-photo",
  },
  gamePhoto: {
    zh: "游戏摄影",
    en: "Game Photography",
    deco: "GAME PHOTO",
    key: "game-photo",
  },
  gameDesign: {
    zh: "游戏设计",
    en: "Game Design",
    deco: "GAME DESIGN",
    key: "game-design",
  },
  gameDev: {
    zh: "游戏开发",
    en: "Game Development",
    deco: "GAME DEV",
    key: "game-dev",
  },
  gameMenu: { zh: "游戏选单", en: "Game Menu", deco: "MENU", key: "menu" },
  gamePlay: { zh: "游戏游玩", en: "Gameplay", deco: "PLAY", key: "play" },
  notes: { zh: "心得", en: "Notes", deco: "NOTES", key: "notes" },
  sketching: { zh: "手绘", en: "Sketching", deco: "SKETCHES", key: "sketches" },
  about: { zh: "关于", en: "About", deco: "ABOUT", key: "about" },
  profile: { zh: "形象", en: "Profile", deco: "PROFILE", key: "profile" },
} as const satisfies Record<string, LexiconEntry>;
