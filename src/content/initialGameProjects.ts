/**
 * 由首轮项目清单同步的游戏项目。当前未发现 WebGL 发布包，因此均不可进入游玩路径。
 */
export const initialGameProjects = [
  {
    "id": "antigravity",
    "title": "反重力",
    "titleEn": "AntiGravity",
    "lead": "反重力项目截图，共收录 8 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/antigravity/01.webp",
        "反重力_帮助界面 02"
      ],
      [
        "game-dev/antigravity/02.webp",
        "反重力_开始界面 01"
      ],
      [
        "game-dev/antigravity/03.webp",
        "反重力_排行榜界面 01"
      ],
      [
        "game-dev/antigravity/04.webp",
        "反重力_设置界面"
      ],
      [
        "game-dev/antigravity/05.webp",
        "反重力_升级界面 01"
      ],
      [
        "game-dev/antigravity/06.webp",
        "反重力_通关界面 01"
      ],
      [
        "game-dev/antigravity/07.webp",
        "反重力_游戏结束 01"
      ],
      [
        "game-dev/antigravity/08.webp",
        "反重力_游戏中 02"
      ]
    ]
  },
  {
    "id": "unity-basics",
    "title": "Unity基础教程",
    "titleEn": "",
    "lead": "Unity基础教程项目截图，共收录 3 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/unity-basics/01.webp",
        "Unity基础教程 1"
      ],
      [
        "game-dev/unity-basics/02.webp",
        "Unity基础教程 2"
      ],
      [
        "game-dev/unity-basics/03.webp",
        "Unity基础教程 3"
      ]
    ]
  },
  {
    "id": "3d-rpg",
    "title": "3DRPG",
    "titleEn": "",
    "lead": "3DRPG项目截图，共收录 8 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/3d-rpg/01.webp",
        "3DRPG_练习 0"
      ],
      [
        "game-dev/3d-rpg/02.webp",
        "3DRPG_练习 1"
      ],
      [
        "game-dev/3d-rpg/03.webp",
        "3DRPG_练习 2"
      ],
      [
        "game-dev/3d-rpg/04.webp",
        "3DRPG_练习 3"
      ],
      [
        "game-dev/3d-rpg/05.webp",
        "3DRPG_练习 4"
      ],
      [
        "game-dev/3d-rpg/06.webp",
        "3DRPG_练习 5"
      ],
      [
        "game-dev/3d-rpg/07.webp",
        "3DRPG_练习 6"
      ],
      [
        "game-dev/3d-rpg/08.webp",
        "3DRPG_游戏架构图 1"
      ]
    ]
  },
  {
    "id": "dark-queen-3d-action",
    "title": "暗黑女王 3D动作",
    "titleEn": "",
    "lead": "暗黑女王 3D动作项目截图，共收录 3 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/dark-queen-3d-action/01.webp",
        "暗黑女王_练习 1"
      ],
      [
        "game-dev/dark-queen-3d-action/02.webp",
        "暗黑女王_练习 2"
      ],
      [
        "game-dev/dark-queen-3d-action/03.webp",
        "暗黑女王_练习 3"
      ]
    ]
  },
  {
    "id": "classic-games",
    "title": "经典游戏",
    "titleEn": "",
    "lead": "经典游戏项目截图，共收录 6 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/classic-games/01.webp",
        "捕鱼达人_练习 1"
      ],
      [
        "game-dev/classic-games/02.webp",
        "俄罗斯方块_练习 1"
      ],
      [
        "game-dev/classic-games/03.webp",
        "愤怒的小鸟_练习 1"
      ],
      [
        "game-dev/classic-games/04.webp",
        "贪吃蛇_练习 1"
      ],
      [
        "game-dev/classic-games/05.webp",
        "坦克大战_练习 1"
      ],
      [
        "game-dev/classic-games/06.webp",
        "小跳蛙_练习 1"
      ]
    ]
  },
  {
    "id": "p-grade-position-tower-defense",
    "title": "P级阵地 塔防",
    "titleEn": "",
    "lead": "P级阵地 塔防项目截图，共收录 2 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/p-grade-position-tower-defense/01.webp",
        "P级阵地_练习 1"
      ],
      [
        "game-dev/p-grade-position-tower-defense/02.webp",
        "P级阵地_练习 2"
      ]
    ]
  },
  {
    "id": "qframework",
    "title": "QFramework 框架",
    "titleEn": "",
    "lead": "QFramework 框架项目截图，共收录 2 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/qframework/01.webp",
        "背包制作_练习 1"
      ],
      [
        "game-dev/qframework/02.webp",
        "点点点_游戏架构图 1"
      ]
    ]
  },
  {
    "id": "unity-official-tutorial",
    "title": "Unity官方教程",
    "titleEn": "",
    "lead": "Unity官方教程项目截图，共收录 2 张图片。",
    "playable": false,
    "screenshots": [
      [
        "game-dev/unity-official-tutorial/01.webp",
        "官方教程_练习 1"
      ],
      [
        "game-dev/unity-official-tutorial/02.webp",
        "官方教程_练习 2"
      ]
    ]
  }
] as const;

/** 工具确认登记的游戏空壳；无截图时不进访客选单。 */
export const registeredGames: Array<{
  id: string;
  title: string;
  titleEn: string;
  lead: string;
  playable: boolean;
  consent?: string;
  stageFolder?: string;
  screenshots: Array<[string, string]>;
}> = [
  {
    id: "squirmeal",
    title: "蛄蛹者",
    titleEn: "",
    lead: "",
    playable: false,
    consent: "pending",
    stageFolder: "game-dev/参赛作品/蛄蛹者 Squirmeal",
    screenshots: [],
  },
  {
    id: "explorer",
    title: "探索者号",
    titleEn: "",
    lead: "",
    playable: false,
    consent: "pending",
    stageFolder: "game-dev/参赛作品/探索者号 Explorer",
    screenshots: [],
  },
];
