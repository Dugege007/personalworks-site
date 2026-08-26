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
  exifLite?: string;
  consent?: WorkConsent;
  displayName?: string;
};

function images(...labels: string[]): WorkMedia[] {
  return labels.map((label) => ({ kind: "image", label }));
}

export const placeholderWorks: WorkRecord[] = [
  {
    id: "sample-1",
    channel: "digital-twin",
    title: "总图叠合",
    year: "2024",
    tags: ["场地", "分层"],
    role: "场景搭建与数据分层",
    summary: "把总图、建档与测点收进同一套可量测的层。",
    body: "占位正文。先叠合场地总图，再把运行层与测点层对齐。后续替换为项目说明与脱敏截图。",
    media: [...images("总图", "分层", "测点"), { kind: "video", label: "漫游切片 · 静音点击播放" }],
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
    media: images("剖切 A", "剖切 B", "注释层"),
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
    media: images("编号墙", "点位"),
  },
  {
    id: "sample-1",
    channel: "line-sim",
    title: "工位节拍",
    year: "2024",
    tags: ["节拍"],
    lineType: "装配线",
    metrics: "节拍 48s · 纯展示",
    summary: "把工位节拍放慢，用来看堵塞如何形成。",
    body: "占位正文。强调工位、节拍与等待，而不是地球级孪生。视频默认静音。",
    media: [...images("工位", "节拍板"), { kind: "video", label: "节拍回放 · 静音点击播放" }],
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
    media: images("回路", "缓冲位", "回流"),
  },
  {
    id: "sample-3",
    channel: "line-sim",
    title: "瓶颈回放",
    year: "2023",
    lineType: "瓶颈观察",
    summary: "回放一次堵塞，用来说明观察方法。",
    body: "占位正文。瓶颈回放只作结果说明，不接入实时数据。",
    media: images("堵塞前", "堵塞中"),
  },
  {
    id: "sample-1",
    channel: "landscape-rendering",
    title: "主视角",
    year: "2022",
    role: "方案与空间叙事",
    clientAlias: "某庭院（别名）",
    summary: "空间先被框住，再被光慢慢填满。",
    body: "占位正文。效果图序列说明空间层次；客户真名默认不展示。",
    media: images("主视角", "近景", "林缘"),
  },
  {
    id: "sample-2",
    channel: "landscape-rendering",
    title: "黄昏庭院",
    year: "2021",
    role: "效果图",
    clientAlias: "某宅院（别名）",
    summary: "用黄昏金把庭院的一层光留下来。",
    body: "占位正文。宽画幅在手机上改为纵向堆叠，避免横向撑破视口。",
    media: images("黄昏", "水面", "廊"),
  },
  {
    id: "sample-3",
    channel: "landscape-rendering",
    title: "林缘断面",
    year: "2021",
    role: "断面叙事",
    summary: "用断面说明林缘如何把园子托住。",
    body: "占位正文。断面用于说明，不是施工依据。",
    media: images("断面", "层次"),
  },
  {
    id: "sample-1",
    channel: "landscape-cds",
    title: "总图选页",
    year: "2020",
    sheetType: "总图",
    summary: "脱敏后的总图选页，只展示图纸能力。",
    body: "占位正文。正式景观施工图须为网图版本后再入库。本页不作施工依据。",
    media: images("总图选页", "图框", "索引"),
  },
  {
    id: "sample-2",
    channel: "landscape-cds",
    title: "铺装详图",
    year: "2020",
    sheetType: "详图",
    summary: "铺装节点的脱敏选页。",
    body: "占位正文。详图仅展示表达能力，不提供下载原图。",
    media: images("铺装", "收边"),
  },
  {
    id: "sample-3",
    channel: "landscape-cds",
    title: "索引裁切",
    year: "2019",
    sheetType: "索引",
    summary: "从完整图册裁出的索引页。",
    body: "占位正文。索引用于说明图种组织，不作为出图模板。",
    media: images("索引"),
  },
  {
    id: "sample-1",
    channel: "landscape-photo",
    title: "山脊",
    year: "2015",
    place: "杭州",
    placeAlias: "某山脊（别名）",
    exifLite: "1/250 · f/8 · ISO 100",
    tags: ["山", "晨雾"],
    summary: "把观看停在光线刚好够用的那一瞬。",
    body: "界面让于画面。地点只用别名；EXIF 已去掉 GPS。",
    media: images("山脊", "云隙"),
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
    media: images("水面", "倒影", "岸"),
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
    media: images("雾"),
  },
  {
    id: "sample-1",
    channel: "humanist-photo",
    title: "街巷",
    year: "2016",
    place: "上海",
    placeAlias: "某巷（别名）",
    exifLite: "1/125 · f/5.6 · ISO 400",
    tags: ["随拍"],
    summary: "把人留在还在发生的现场里。",
    body: "占位正文。人文摄影看日常场域，不按摆拍肖像收录。可辨认近景人脸改走授权人像细目。",
    media: images("街巷", "檐下"),
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
    media: images("市集", "秤"),
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
    media: images("渡口"),
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
    media: images("留白", "侧光"),
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
    media: images("侧光"),
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
    media: images("静场", "手", "窗"),
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
