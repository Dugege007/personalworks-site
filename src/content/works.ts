import { lexicon } from "./lexicon";

export type WorkChannel =
  | typeof lexicon.digitalTwin.key
  | typeof lexicon.lineSimulation.key
  | typeof lexicon.landscapeRendering.key
  | typeof lexicon.landscapeCDs.key
  | typeof lexicon.landscapePhoto.key
  | typeof lexicon.humanistPhoto.key
  | typeof lexicon.portraitPhoto.key;

export type WorkMedia = {
  kind: "image" | "video";
  label: string;
  src?: string;
};

export type WorkConsent = "granted" | "denied" | "pending";

export type WorkRecord = {
  id: string;
  channel: WorkChannel;
  title: string;
  year: string;
  summary: string;
  body: string;
  media: WorkMedia[];
  tags?: string[];
  role?: string;
  lineType?: string;
  metrics?: string;
  clientAlias?: string;
  sheetType?: string;
  placeAlias?: string;
  place?: string;
  /** 项目开工年月，格式 `YYYY-MM`，页面显示为「YYYY年MM月」。 */
  startedOn?: string;
  /** 项目尺度，如大区、示范区。 */
  siteType?: string;
  exifLite?: string;
  consent?: WorkConsent;
  displayName?: string;
  featured?: boolean;
};

function images(...labels: string[]): WorkMedia[] {
  return labels.map((label) => ({ kind: "image", label }));
}

function cover(channel: WorkChannel, id: string, label: string): WorkMedia {
  return { kind: "image", label, src: `${channel}/${id}/01.webp` };
}

function covered(channel: WorkChannel, id: string, labels: string[]): WorkMedia[] {
  return labels.map((label, index) => ({
    kind: "image" as const,
    label,
    ...(index === 0 ? { src: `${channel}/${id}/01.webp` } : {}),
  }));
}

/**
 * 连续编号的效果图组，扩展名与正式位对象一致。
 */
function album(channel: WorkChannel, id: string, count: number, ext = "jpg"): WorkMedia[] {
  return Array.from({ length: count }, (_, index) => {
    const slot = String(index + 1).padStart(2, "0");
    return {
      kind: "image" as const,
      label: `效果图 ${slot}`,
      src: `${channel}/${id}/${slot}.${ext}`,
    };
  });
}

/**
 * 跨前缀拼一组效果图，保持给定顺序。
 */
function listed(entries: Array<[src: string, label: string]>): WorkMedia[] {
  return entries.map(([src, label]) => ({ kind: "image" as const, src, label }));
}

/**
 * 灯箱与焦点图条共用的「(当前/总数) 图名」。
 */
export function formatShotCaption(index: number, total: number, label: string): string {
  return `(${index + 1}/${total}) ${label}`;
}

/**
 * 把 `YYYY-MM` 写成「YYYY年MM月」；无法解析则原样返回。
 */
export function formatStartedOn(value: string): string {
  const match = /^(\d{4})-(\d{2})$/.exec(value);
  if (!match) {
    return value;
  }
  return `${match[1]}年${match[2]}月`;
}

/**
 * 已入库的效果图，按 media 原序。
 */
export function listWorkImages(work: WorkRecord): WorkMedia[] {
  return work.media.filter((item) => item.kind === "image" && item.src);
}

export const placeholderWorks: WorkRecord[] = [
  {
    id: "sample-1",
    channel: "digital-twin",
    title: "总图叠合",
    featured: true,
    year: "2024",
    tags: ["场地", "分层"],
    role: "场景搭建与数据分层",
    summary: "把总图、建档与测点收进同一套可量测的层。",
    body: "占位正文。先叠合场地总图，再把运行层与测点层对齐。后续替换为项目说明与脱敏截图。",
    media: [cover("digital-twin", "sample-1", "总图"), ...images("分层", "测点"), { kind: "video", label: "漫游切片 · 静音点击播放" }],
  },
  {
    id: "sample-2",
    channel: "digital-twin",
    title: "运行剖切",
    year: "2024",
    tags: ["剖切"],
    role: "运行态说明",
    summary: "沿一条剖切看设备如何进入与离开视野。",
    body: "占位正文。剖切用于说明运行关系，不在浏览器里驱动仿真引擎。",
    media: covered("digital-twin", "sample-2", ["剖切 A", "剖切 B", "注释层"]),
  },
  {
    id: "sample-3",
    channel: "digital-twin",
    title: "测点归档",
    year: "2023",
    tags: ["测点"],
    role: "测点编目",
    summary: "把散落的测点收成可检索的档案编号。",
    body: "占位正文。测点以编号归档，详情页只展示已脱敏的位置别名。",
    media: covered("digital-twin", "sample-3", ["编号墙", "点位"]),
  },
  {
    id: "sample-1",
    channel: "line-sim",
    title: "工位节拍",
    featured: true,
    year: "2024",
    tags: ["节拍"],
    lineType: "装配线",
    metrics: "节拍 48s · 纯展示",
    summary: "把工位节拍放慢，用来看堵塞如何形成。",
    body: "占位正文。强调工位、节拍与等待，而不是地球级孪生。视频默认静音。",
    media: [cover("line-sim", "sample-1", "工位"), ...images("节拍板"), { kind: "video", label: "节拍回放 · 静音点击播放" }],
  },
  {
    id: "sample-2",
    channel: "line-sim",
    title: "物流回路",
    year: "2024",
    lineType: "循环物流",
    metrics: "回路 3 段",
    summary: "物料在回路里对位，而不是在地图上飘。",
    body: "占位正文。物流回路用水平导轨的方式阅读，不提供在线调参。",
    media: covered("line-sim", "sample-2", ["回路", "缓冲位", "回流"]),
  },
  {
    id: "sample-3",
    channel: "line-sim",
    title: "瓶颈回放",
    year: "2023",
    lineType: "瓶颈观察",
    summary: "回放一次堵塞，用来说明观察方法。",
    body: "占位正文。瓶颈回放只作结果说明，不接入实时数据。",
    media: covered("line-sim", "sample-3", ["堵塞前", "堵塞中"]),
  },
  {
    id: "xiaowayao",
    channel: "landscape-rendering",
    title: "丰台小瓦窑",
    featured: true,
    year: "2018",
    startedOn: "2018-04",
    place: "北京",
    siteType: "示范区",
    tags: ["日式禅意", "现代东方"],
    role: "方案与效果图",
    summary:
      "叠拼样板庭院分南北两院，南院下叠、北院上叠，看房分精装与清水两路。按六口之家的生活场景，用日式禅意和现代东方做去繁从简的庭园。",
    body: "北京世茂丰台小瓦窑样板庭院。南院为下叠花园，北院为上叠花园。方案按六口之家模拟业主日常：手工、观天、书画与儿童活动。空间语言先走日式禅意，再落到现代东方的聚会与收藏。",
    media: album("landscape-rendering", "xiaowayao", 8),
  },
  {
    id: "huaian-fukang-15",
    channel: "landscape-rendering",
    title: "淮安富康城15#",
    year: "2020",
    startedOn: "2020-03",
    place: "淮安",
    siteType: "大区",
    tags: ["简约艺术", "诗意栖居"],
    role: "方案与效果图",
    summary:
      "淮阴区安置商住地块 15# 组团。周边以教育和居住为主，竞品多为新古典。以月相组织一环一带四花园，用简约艺术做全龄共享的大区。",
    body: "淮自然（阴）挂2019第6号地块商住安置项目 15# 组团。基地在长江西路以南、西安路以东。规划以高层安置为主，中心留出共享中庭。景观用晓月、满月、弦月等月相落位入口水景、林下活动和宅间花园。",
    media: listed([
      ["landscape-rendering/huaian-fukang/01.jpg", "效果图 01"],
      ["landscape-rendering/huaian-fukang/02.jpg", "效果图 02"],
      ["landscape-rendering/huaian-fukang/03.jpg", "效果图 03"],
      ["landscape-rendering/huaian-fukang/04.jpg", "效果图 04"],
      ["landscape-rendering/huaian-fukang/05.jpg", "效果图 05"],
      ["landscape-rendering/huaian-fukang/06.jpg", "效果图 06"],
      ["landscape-rendering/huaian-fukang-15/01.jpg", "效果图 07"],
      ["landscape-rendering/huaian-fukang-15/02.jpg", "效果图 08"],
    ]),
  },
  {
    id: "huaian-fukang-3",
    channel: "landscape-rendering",
    title: "淮安富康城3#",
    year: "2020",
    startedOn: "2020-08",
    place: "淮安",
    siteType: "大区",
    tags: ["简约艺术", "诗意栖居"],
    role: "方案与效果图",
    summary:
      "淮阴区安置商住地块 3# 组团。周边以教育和居住为主，竞品多为新古典。以月相组织一环一带四花园，用简约艺术做全龄共享的大区。",
    body: "淮自然（阴）挂2019第6号地块商住安置项目 3# 组团。基地在长江西路以南、西安路以东。规划以高层安置为主，中心留出共享中庭。景观用晓月、满月、弦月等月相落位入口水景、林下活动和宅间花园。",
    media: listed([
      ["landscape-rendering/huaian-fukang/07.jpg", "效果图 01"],
      ["landscape-rendering/huaian-fukang/08.jpg", "效果图 02"],
      ["landscape-rendering/huaian-fukang/09.jpg", "效果图 03"],
      ["landscape-rendering/huaian-fukang/10.jpg", "效果图 04"],
      ["landscape-rendering/huaian-fukang-3/01.jpg", "效果图 05"],
      ["landscape-rendering/huaian-fukang-3/02.jpg", "效果图 06"],
      ["landscape-rendering/huaian-fukang-3/03.jpg", "效果图 07"],
      ["landscape-rendering/huaian-fukang-3/04.jpg", "效果图 08"],
      ["landscape-rendering/huaian-fukang-3/05.jpg", "效果图 09"],
      ["landscape-rendering/huaian-fukang-3/06.jpg", "效果图 10"],
    ]),
  },
  {
    id: "wuxi-north-station",
    channel: "landscape-rendering",
    title: "无锡汽车北站",
    year: "2019",
    startedOn: "2019-06",
    place: "无锡",
    siteType: "大区",
    tags: ["现代简洁", "森活"],
    role: "方案与效果图",
    summary:
      "梁溪区原汽车北站西侧，东侧紧邻火车站轨道，绿地偏紧、高层遮阴。延续建筑的现代线条，以乌桕林下氧吧和全龄活动做成森活社区。",
    body: "世茂无锡原汽车北站西侧。基地在兴源北路与惠勤路交叉口西北，东侧火车站轨道带来噪声与震动，绿地率紧张。景观策略是酒店式入口、九棵乌桕的林下氧吧、儿童活动与宅间秘境，把房子放进森林里。",
    media: album("landscape-rendering", "wuxi-north-station", 9),
  },
  {
    id: "xingyi-78",
    channel: "landscape-rendering",
    title: "富康城兴义7#8#",
    year: "2019",
    startedOn: "2019-09",
    place: "兴义",
    siteType: "示范区",
    tags: ["片石水景", "林荫道"],
    role: "方案与效果图",
    summary:
      "麓榕岛7#8#样板庭院与宅间。用入户汀步、片石水景和对景景墙组织两套庭院，林荫道把归家界面连起来。",
    body: "富康城兴义麓榕岛样板庭院。庭院A以汀步、片石水景、对景景墙和茶室为主；庭院B补户外餐桌、廊架与吧台。同期效果图也收录林荫道与幼儿园示范界面。",
    media: album("landscape-rendering", "xingyi-78", 8),
  },
  {
    id: "sample-1",
    channel: "landscape-cds",
    title: "总图选页",
    year: "2020",
    sheetType: "总图",
    summary: "脱敏后的总图选页，只展示图纸能力。",
    body: "占位正文。正式景观施工图须为网图版本后再入库。本页不作施工依据。",
    media: covered("landscape-cds", "sample-1", ["总图选页", "图框", "索引"]),
  },
  {
    id: "sample-2",
    channel: "landscape-cds",
    title: "铺装详图",
    year: "2020",
    sheetType: "详图",
    summary: "铺装节点的脱敏选页。",
    body: "占位正文。详图仅展示表达能力，不提供下载原图。",
    media: covered("landscape-cds", "sample-2", ["铺装", "收边"]),
  },
  {
    id: "sample-3",
    channel: "landscape-cds",
    title: "索引裁切",
    year: "2019",
    sheetType: "索引",
    summary: "从完整图册裁出的索引页。",
    body: "占位正文。索引用于说明图种组织，不作为出图模板。",
    media: covered("landscape-cds", "sample-3", ["索引"]),
  },
  {
    id: "sample-1",
    channel: "landscape-photo",
    title: "山脊",
    featured: true,
    year: "2015",
    place: "杭州",
    placeAlias: "某山脊（别名）",
    exifLite: "1/250 · f/8 · ISO 100",
    tags: ["山", "晨雾"],
    summary: "把观看停在光线刚好够用的那一瞬。",
    body: "界面让于画面。地点只用别名；EXIF 已去掉 GPS。",
    media: [cover("landscape-photo", "sample-1", "山脊"), ...images("云隙")],
  },
  {
    id: "sample-2",
    channel: "landscape-photo",
    title: "水面",
    year: "2017",
    place: "苏州",
    placeAlias: "某湖（别名）",
    exifLite: "1/60 · f/11 · ISO 64",
    tags: ["水"],
    summary: "水面把天光收成一层。",
    body: "占位正文。系列图控制在数张以内，不塞原片。",
    media: covered("landscape-photo", "sample-2", ["水面", "倒影", "岸"]),
  },
  {
    id: "sample-3",
    channel: "landscape-photo",
    title: "雾色",
    year: "2024",
    place: "南京",
    placeAlias: "某谷（别名）",
    tags: ["晨雾"],
    summary: "雾把层次减到还能走的程度。",
    body: "占位正文。无 EXIF 时不留空行。",
    media: covered("landscape-photo", "sample-3", ["雾"]),
  },
  {
    id: "sample-1",
    channel: "humanist-photo",
    title: "街巷",
    featured: true,
    year: "2016",
    place: "上海",
    placeAlias: "某巷（别名）",
    exifLite: "1/125 · f/5.6 · ISO 400",
    tags: ["随拍"],
    summary: "把人留在还在发生的现场里。",
    body: "占位正文。人文摄影看日常场域，不按摆拍肖像收录。可辨认近景人脸改走授权人像细目。",
    media: [cover("humanist-photo", "sample-1", "街巷"), ...images("檐下")],
  },
  {
    id: "sample-2",
    channel: "humanist-photo",
    title: "市集",
    year: "2018",
    place: "新加坡",
    placeAlias: "某集（别名）",
    tags: ["随拍", "市井"],
    summary: "摊位把一天的光切成一段一段。",
    body: "占位正文。地点只用别名；无 EXIF 时不留空行。",
    media: covered("humanist-photo", "sample-2", ["市集", "秤"]),
  },
  {
    id: "sample-3",
    channel: "humanist-photo",
    title: "渡口",
    year: "2020",
    place: "长滩岛",
    placeAlias: "某渡（别名）",
    exifLite: "1/250 · f/8 · ISO 200",
    tags: ["水"],
    summary: "等人的空隙里，河面还在走。",
    body: "占位正文。系列图控制在数张以内，不塞原片。",
    media: covered("humanist-photo", "sample-3", ["渡口"]),
  },
  {
    id: "sample-1",
    channel: "portrait-photo",
    title: "留白",
    year: "2019",
    place: "上海",
    consent: "granted",
    tags: ["室内"],
    summary: "已授权肖像的抽象框景，不展示被摄者真名。",
    body: "占位正文。人像页信息量低于风光；未提供 displayName 时不写姓名。",
    media: covered("portrait-photo", "sample-1", ["留白", "侧光"]),
  },
  {
    id: "sample-2",
    channel: "portrait-photo",
    title: "侧光",
    year: "2021",
    place: "苏州",
    consent: "granted",
    tags: ["侧光"],
    summary: "侧光把轮廓留下来，背景尽量空。",
    body: "占位正文。生产列表只收录 consent 为已授权的条目。",
    media: covered("portrait-photo", "sample-2", ["侧光"]),
  },
  {
    id: "sample-3",
    channel: "portrait-photo",
    title: "静场",
    year: "2025",
    place: "杭州",
    consent: "granted",
    tags: ["室内"],
    summary: "把人留在刚好够用的光线里。",
    body: "占位正文。下架时更换对象键并刷新 CDN，本轮无真实人脸。",
    media: covered("portrait-photo", "sample-3", ["静场", "手", "窗"]),
  },
  {
    id: "held",
    channel: "portrait-photo",
    title: "未授权条目",
    year: "2024",
    consent: "denied",
    summary: "不应出现在列表或详情。",
    body: "此条仅用于验证未授权不对外可见。",
    media: images("不应展示"),
  },
];

/**
 * 列出某细目下对访客可见的作品；未授权人像不进入列表。
 */
export function listPublishedWorks(channel: string): WorkRecord[] {
  return placeholderWorks.filter(
    (item) => item.channel === channel && (!item.consent || item.consent === "granted"),
  );
}

/**
 * 列出全部对访客可见的作品。
 */
export function listAllPublishedWorks(): WorkRecord[] {
  return placeholderWorks.filter((item) => !item.consent || item.consent === "granted");
}

/**
 * 按 id 查找可见作品。id 跨 channel 可能重复，调用方须自行取舍。
 */
export function findPublishedWorksById(id: string): WorkRecord[] {
  return listAllPublishedWorks().filter((item) => item.id === id);
}

/**
 * 按细目与 id 查找可见作品。未知 id 或未授权人像一律视为不存在。
 */
export function findPublishedWork(channel: string, id: string): WorkRecord | undefined {
  const work = placeholderWorks.find((item) => item.channel === channel && item.id === id);
  if (!work) {
    return undefined;
  }
  if (work.consent && work.consent !== "granted") {
    return undefined;
  }
  return work;
}

export const photoWorkChannels = [
  lexicon.landscapePhoto.key,
  lexicon.humanistPhoto.key,
  lexicon.portraitPhoto.key,
] as const;

export type PhotoWorkChannel = (typeof photoWorkChannels)[number];

export type PhotoCatalogQuery = {
  channels: string[];
  years: string[];
  places: string[];
  tags: string[];
  sort: "asc" | "desc";
};

/**
 * 列出访客可见的摄影作品（不含未授权人像、不含游戏摄影留位）。
 */
export function listPublishedPhotoWorks(): WorkRecord[] {
  const allowed = new Set<string>(photoWorkChannels);
  return placeholderWorks.filter(
    (item) => allowed.has(item.channel) && (!item.consent || item.consent === "granted"),
  );
}

/**
 * 从摄影作品集提取筛选项；年份升序，其余按中文排序。
 */
export function collectPhotoFacets(works: WorkRecord[]): {
  years: string[];
  places: string[];
  tags: string[];
} {
  const years = new Set<string>();
  const places = new Set<string>();
  const tags = new Set<string>();
  for (const work of works) {
    years.add(work.year);
    if (work.place) {
      places.add(work.place);
    }
    for (const tag of work.tags ?? []) {
      tags.add(tag);
    }
  }
  return {
    years: [...years].sort(),
    places: [...places].sort((a, b) => a.localeCompare(b, "zh-CN")),
    tags: [...tags].sort((a, b) => a.localeCompare(b, "zh-CN")),
  };
}

/**
 * 按维度过滤摄影作品：维度内并集，维度间交集。
 */
export function filterPhotoWorks(works: WorkRecord[], query: PhotoCatalogQuery): WorkRecord[] {
  return works.filter((work) => {
    if (query.channels.length > 0 && !query.channels.includes(work.channel)) {
      return false;
    }
    if (query.years.length > 0 && !query.years.includes(work.year)) {
      return false;
    }
    if (query.places.length > 0 && (!work.place || !query.places.includes(work.place))) {
      return false;
    }
    if (query.tags.length > 0 && !query.tags.some((tag) => work.tags?.includes(tag))) {
      return false;
    }
    return true;
  });
}

/**
 * 按年份排序；同年按标题稳定次序。
 */
export function sortPhotoWorks(works: WorkRecord[], sort: "asc" | "desc"): WorkRecord[] {
  const sign = sort === "asc" ? 1 : -1;
  return [...works].sort((a, b) => {
    const yearDiff = a.year.localeCompare(b.year) * sign;
    if (yearDiff !== 0) {
      return yearDiff;
    }
    return a.title.localeCompare(b.title, "zh-CN");
  });
}
