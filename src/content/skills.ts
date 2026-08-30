import { lexicon } from "./lexicon";
import type { LocalizedString } from "../prefs/types";

export type SkillItem = {
  id: string;
  name: LocalizedString;
  proof: LocalizedString;
  tools?: string[];
};

export type SkillDomain = {
  id: string;
  name: LocalizedString;
  deco: string;
  items: SkillItem[];
  tools: string[];
  workKind?: string;
  workLabel?: LocalizedString;
  workHrefFallback?: string;
};

/**
 * 技能正本。按领域分组，证明句写做过的事，不用百分比和星级。
 */
export const skillDomains: SkillDomain[] = [
  {
    id: "skill-twin-sim",
    name: `${lexicon.digitalTwin.zh}与仿真`,
    deco: lexicon.twinAndSim.deco,
    workKind: lexicon.twinAndSim.key,
    workLabel: lexicon.twinAndSim.zh,
    items: [
      {
        id: "twin-line",
        name: lexicon.lineSimulation.zh,
        proof: "产线里对过工位节拍、物流回路和等待。",
      },
      {
        id: "twin-site",
        name: lexicon.digitalTwin.zh,
        proof: "把场地收成可量测的层，先看结构再看运行。",
      },
      {
        id: "twin-script",
        name: "工程脚本",
        proof: "用 C# 把运行关系收成可复用模块，不堆一次性脚本。",
      },
    ],
    tools: ["C#", "Git", "Unity"],
  },
  {
    id: "skill-landscape",
    name: lexicon.landscapeArch.zh,
    deco: lexicon.landscapeArch.deco,
    workKind: lexicon.landscapeArch.key,
    workLabel: lexicon.landscapeArch.zh,
    items: [
      {
        id: "land-cds",
        name: lexicon.landscapeCDs.zh,
        proof: "四年里画详图和通图，也参与过公司图纸规范。",
      },
      {
        id: "land-render",
        name: lexicon.landscapeRendering.zh,
        proof: "后期用 SketchUp 建模、Lumion 出图，做过方案深化。",
      },
      {
        id: "land-cad",
        name: "AutoCAD",
        proof: "市政与住区施工图都在 CAD 里收口。",
      },
    ],
    tools: ["AutoCAD", "SketchUp", "Lumion", "Photoshop", "Rhino", "Grasshopper"],
  },
  {
    id: "skill-game",
    name: lexicon.gameDev.zh,
    deco: lexicon.gameDev.deco,
    workKind: lexicon.gameDev.key,
    workLabel: lexicon.gameDev.zh,
    items: [
      {
        id: "game-unity",
        name: "Unity 客户端",
        proof: "自学。做过 UGUI、动画状态机、对象池和编辑器扩展。",
      },
      {
        id: "game-csharp",
        name: "C#",
        proof: "常用单例、命令、观察者、策略；用 ScriptableObject 配波次和升级。",
      },
      {
        id: "game-jam",
        name: "独立作品",
        proof: "GameJam 拿过奖。联机斗地主用 Socket 和 MySQL 做过 Demo。",
      },
    ],
    tools: ["Unity", "C#", "QFramework", "Odin", "DOTween", "FairyGUI", "Aseprite", "MySQL"],
  },
  {
    id: "skill-photo",
    name: lexicon.photography.zh,
    deco: lexicon.photography.deco,
    workKind: lexicon.photography.key,
    workLabel: lexicon.photography.zh,
    items: [
      {
        id: "photo-land",
        name: lexicon.landscapePhoto.zh,
        proof: "拍场地和光线，不当成景观方案图。",
      },
      {
        id: "photo-human",
        name: lexicon.humanistPhoto.zh,
        proof: "拍人在场里的样子，不作街头或纪实栏目。",
      },
      {
        id: "photo-portrait",
        name: lexicon.portraitPhoto.zh,
        proof: "形象照自己拍过，后期走 Lightroom。",
      },
    ],
    tools: ["Lightroom", "Photoshop"],
  },
  {
    id: "skill-craft",
    name: "工程与表达",
    deco: "CRAFT",
    workLabel: lexicon.sketching.zh,
    workHrefFallback: `/${lexicon.notes.key}/${lexicon.sketching.key}`,
    items: [
      {
        id: "craft-doc",
        name: "文档",
        proof: "技术笔记和站点文案都自己写，少口号。",
      },
      {
        id: "craft-en",
        name: "英文阅读",
        proof: "开发时直接读英文文档，不靠机翻硬顶。",
      },
      {
        id: "craft-sketch",
        name: lexicon.sketching.zh,
        proof: "景观训练留下的手绘底子，现在收在心得里。",
      },
      {
        id: "craft-edit",
        name: "剪辑",
        proof: "用 Premiere 剪过短片，服务出图和记录，不是成片工种。",
      },
    ],
    tools: ["Git", "Premiere", "Markdown"],
  },
];
