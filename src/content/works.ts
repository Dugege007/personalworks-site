import { initialWorkProjects } from "./initialWorkProjects";
import { lexicon } from "./lexicon";
import { toListedMedia } from "./listedMedia";
import { photoSizes } from "./photoSizes";
import { effectiveDate, sortByEffectiveDateDescending } from "./workDates";

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
  poster?: string;
  displayName?: string;
  description?: string;
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
  tags?: readonly string[];
  role?: string;
  lineType?: string;
  metrics?: string;
  clientAlias?: string;
  /** 任职单位简称；缺省时从投放夹设计单位段解析。单任职单位栏目不展示。 */
  studioAlias?: string;
  sheetType?: string;
  placeAlias?: string;
  place?: string;
  /** 项目日期，格式为 `YYYY-MM` 或 `YYYY-MM-DD`。 */
  startedOn?: string;
  /** 摄影拍摄日期，格式为 `YYYY-MM-DD`。 */
  capturedOn?: string;
  /** 项目尺度，如大区、示范区。 */
  siteType?: string;
  exifLite?: string;
  consent?: WorkConsent;
  /** 投放箱第一层文件夹；仅维护工具使用，访客页忽略。 */
  stageFolder?: string;
  displayName?: string;
  featured?: boolean;
};

/**
 * 按给定对象键与图名生成媒体，禁止推断连续对象键。
 */
type ListedEntry = readonly [src: string, label: string] | WorkMedia;

function listed(entries: readonly ListedEntry[]): WorkMedia[] {
  return entries.map((entry): WorkMedia => {
    if ("kind" in entry) {
      return entry;
    }
    const [src, label] = entry;
    return toListedMedia(src, label);
  });
}

/**
 * 灯箱与焦点图条共用的「(当前/总数) 图名」。
 */
export function formatShotCaption(index: number, total: number, label: string): string {
  return `(${index + 1}/${total}) ${label}`;
}

/**
 * 格式化项目日期；无法解析时原样返回。
 */
export function formatStartedOn(value: string): string {
  const match = /^(\d{4})-(\d{2})(?:-(\d{2}))?$/.exec(value);
  if (!match) {
    return value;
  }
  return match[3]
    ? `${match[1]}年${match[2]}月${match[3]}日`
    : `${match[1]}年${match[2]}月`;
}

/**
 * 已列入内容池的图片，按 media 原序返回。
 */
export function listWorkImages(work: WorkRecord): WorkMedia[] {
  return work.media.filter((item) => item.kind === "image" && item.src);
}

/**
 * 已列入内容池的视频，按 media 原序返回。
 */
export function listWorkVideos(work: WorkRecord): WorkMedia[] {
  return work.media.filter((item) => item.kind === "video");
}

/**
 * 已列入内容池、可供画册与灯箱翻页的媒体，按 media 原序返回。
 */
export function listWorkShots(work: WorkRecord): WorkMedia[] {
  return work.media.filter((item) => item.src);
}

export type HomeHeroFrame = {
  src: string;
  year: string;
  href?: string;
  width?: number;
  height?: number;
};

/**
 * 风光摄影项目页地址；头图二次点击进入所属相册。
 */
function hrefForLandscapeWork(work: WorkRecord): string {
  return `/${lexicon.photography.key}/${work.channel}/${work.id}`;
}

/**
 * 显影头图图池：已发布风光摄影的全部画面，按有效日期倒序。
 */
export function listLandscapeHeroFrames(): HomeHeroFrame[] {
  const frames: HomeHeroFrame[] = [];
  const seen = new Set<string>();
  for (const work of listPublishedWorks(lexicon.landscapePhoto.key)) {
    const href = hrefForLandscapeWork(work);
    for (const media of listWorkImages(work)) {
      if (!media.src || seen.has(media.src)) {
        continue;
      }
      seen.add(media.src);
      const size = photoSizes[media.src];
      frames.push({
        src: media.src,
        year: work.year,
        href,
        width: size?.width,
        height: size?.height,
      });
    }
  }
  return frames;
}

const initialWorks: WorkRecord[] = initialWorkProjects.map((item) => ({
  ...item,
  channel: item.channel as WorkChannel,
  media: listed(item.media),
}));

// 首轮没有可直接公开的产线仿真静帧。视频项目不伪造图片占位，后续补图后再入内容池。
const lineSimulationWorks: WorkRecord[] = [];

/** 维护工具确认登记的空壳；无图保持 pending，上页后补丁改为 granted。人像授权除外。 */
const registeredWorks: WorkRecord[] = [
  {
    id: "shimao-beijing-yidu",
    channel: "landscape-cds",
    title: "世茂北京一渡",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201709 世茂北京一渡",
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-beijing-yidu/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-beijing-yidu/08.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-beijing-yidu/09.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-beijing-yidu/10.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-beijing-yidu/101.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-beijing-yidu/103.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-beijing-yidu/120.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-beijing-yidu/121.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-beijing-yidu/127.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-beijing-yidu/130.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-beijing-yidu/131.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-beijing-yidu/132.webp" }, { kind: "image", label: "效果图 13", src: "landscape-cds/shimao-beijing-yidu/140.webp" }, { kind: "image", label: "效果图 14", src: "landscape-cds/shimao-beijing-yidu/147.webp" }, { kind: "image", label: "效果图 15", src: "landscape-cds/shimao-beijing-yidu/148.webp" }, { kind: "image", label: "效果图 16", src: "landscape-cds/shimao-beijing-yidu/149.webp" }, { kind: "image", label: "效果图 17", src: "landscape-cds/shimao-beijing-yidu/150.webp" }, { kind: "image", label: "效果图 18", src: "landscape-cds/shimao-beijing-yidu/154.webp" }, { kind: "image", label: "效果图 19", src: "landscape-cds/shimao-beijing-yidu/159.webp" }, { kind: "image", label: "效果图 20", src: "landscape-cds/shimao-beijing-yidu/162.webp" }, { kind: "image", label: "效果图 21", src: "landscape-cds/shimao-beijing-yidu/163.webp" }, { kind: "image", label: "效果图 22", src: "landscape-cds/shimao-beijing-yidu/164.webp" }, { kind: "image", label: "效果图 23", src: "landscape-cds/shimao-beijing-yidu/165.webp" }, { kind: "image", label: "效果图 24", src: "landscape-cds/shimao-beijing-yidu/166.webp" }, { kind: "image", label: "效果图 25", src: "landscape-cds/shimao-beijing-yidu/182.webp" }, { kind: "image", label: "效果图 26", src: "landscape-cds/shimao-beijing-yidu/184.webp" }, { kind: "image", label: "效果图 27", src: "landscape-cds/shimao-beijing-yidu/33.webp" }, { kind: "image", label: "效果图 28", src: "landscape-cds/shimao-beijing-yidu/34.webp" }, { kind: "image", label: "效果图 29", src: "landscape-cds/shimao-beijing-yidu/35.webp" }, { kind: "image", label: "效果图 30", src: "landscape-cds/shimao-beijing-yidu/36.webp" }, { kind: "image", label: "效果图 31", src: "landscape-cds/shimao-beijing-yidu/39.webp" }, { kind: "image", label: "效果图 32", src: "landscape-cds/shimao-beijing-yidu/40.webp" }, { kind: "image", label: "效果图 33", src: "landscape-cds/shimao-beijing-yidu/41.webp" }, { kind: "image", label: "效果图 34", src: "landscape-cds/shimao-beijing-yidu/44.webp" }, { kind: "image", label: "效果图 35", src: "landscape-cds/shimao-beijing-yidu/45.webp" }, { kind: "image", label: "效果图 36", src: "landscape-cds/shimao-beijing-yidu/46.webp" }, { kind: "image", label: "效果图 37", src: "landscape-cds/shimao-beijing-yidu/49.webp" }, { kind: "image", label: "效果图 38", src: "landscape-cds/shimao-beijing-yidu/50.webp" }, { kind: "image", label: "效果图 39", src: "landscape-cds/shimao-beijing-yidu/51.webp" }, { kind: "image", label: "效果图 40", src: "landscape-cds/shimao-beijing-yidu/52.webp" }, { kind: "image", label: "效果图 41", src: "landscape-cds/shimao-beijing-yidu/53.webp" }, { kind: "image", label: "效果图 42", src: "landscape-cds/shimao-beijing-yidu/54.webp" }, { kind: "image", label: "效果图 43", src: "landscape-cds/shimao-beijing-yidu/55.webp" }, { kind: "image", label: "效果图 44", src: "landscape-cds/shimao-beijing-yidu/56.webp" }, { kind: "image", label: "效果图 45", src: "landscape-cds/shimao-beijing-yidu/57.webp" }, { kind: "image", label: "效果图 46", src: "landscape-cds/shimao-beijing-yidu/58.webp" }, { kind: "image", label: "效果图 47", src: "landscape-cds/shimao-beijing-yidu/59.webp" }, { kind: "image", label: "效果图 48", src: "landscape-cds/shimao-beijing-yidu/60.webp" }, { kind: "image", label: "效果图 49", src: "landscape-cds/shimao-beijing-yidu/61.webp" }, { kind: "image", label: "效果图 50", src: "landscape-cds/shimao-beijing-yidu/62.webp" }, { kind: "image", label: "效果图 51", src: "landscape-cds/shimao-beijing-yidu/72.webp" }, { kind: "image", label: "效果图 52", src: "landscape-cds/shimao-beijing-yidu/73.webp" }, { kind: "image", label: "效果图 53", src: "landscape-cds/shimao-beijing-yidu/74.webp" }, { kind: "image", label: "效果图 54", src: "landscape-cds/shimao-beijing-yidu/75.webp" }, { kind: "image", label: "效果图 55", src: "landscape-cds/shimao-beijing-yidu/78.webp" }, { kind: "image", label: "效果图 56", src: "landscape-cds/shimao-beijing-yidu/79.webp" }, { kind: "image", label: "效果图 57", src: "landscape-cds/shimao-beijing-yidu/80.webp" }, { kind: "image", label: "效果图 58", src: "landscape-cds/shimao-beijing-yidu/81.webp" }, { kind: "image", label: "效果图 59", src: "landscape-cds/shimao-beijing-yidu/82.webp" }, { kind: "image", label: "效果图 60", src: "landscape-cds/shimao-beijing-yidu/83.webp" }, { kind: "image", label: "效果图 61", src: "landscape-cds/shimao-beijing-yidu/84.webp" }, { kind: "image", label: "效果图 62", src: "landscape-cds/shimao-beijing-yidu/85.webp" }, { kind: "image", label: "效果图 63", src: "landscape-cds/shimao-beijing-yidu/86.webp" }, { kind: "image", label: "效果图 64", src: "landscape-cds/shimao-beijing-yidu/87.webp" }],
  }
,
  {
    id: "vandeviele",
    channel: "digital-twin",
    title: "范德威尔",
    year: "2025",
    startedOn: "2025-02-20",
    place: "无锡",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "digital-twin/20250220 范德威尔",
    media: [{ kind: "video", src: "digital-twin/vandeviele/01.mp4", poster: "digital-twin/vandeviele/01.poster.webp", label: "范德威尔_注塑车间 服务器端运行 _250508" }, { kind: "video", src: "digital-twin/vandeviele/02.mp4", poster: "digital-twin/vandeviele/02.poster.webp", label: "范德威尔_钣金车间自动化演示_250430" }],
  }
,
  {
    id: "shanghai-baoniao-garments",
    channel: "digital-twin",
    title: "上海宝鸟服饰",
    year: "2025",
    startedOn: "2025-05-27",
    place: "上海",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "digital-twin/20250527 上海宝鸟服饰",
    media: [{ kind: "video", src: "digital-twin/shanghai-baoniao-garments/01.mp4", poster: "digital-twin/shanghai-baoniao-garments/01.poster.webp", label: "宝鸟_智能裁床动画演示_0.8.0.1b_250723" }],
  }
,
  {
    id: "hangyu-assembly-workshop",
    channel: "line-sim",
    title: "某工厂车间装配线仿真",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "line-sim/FlexSim/20260821 某工厂车间装配线仿真",
    media: [{ kind: "video", src: "line-sim/hangyu-assembly-workshop/01.mp4", poster: "line-sim/hangyu-assembly-workshop/01.poster.webp", label: "某工厂车间_装配产线仿真_2" }],
  }
,
  {
    id: "shimao-jiyang",
    channel: "landscape-cds",
    title: "世茂济阳3#",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201803 世茂济阳3#",
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-jiyang/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-jiyang/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-jiyang/03.webp" }],
  }
,
  {
    id: "zhangjiagang-4",
    channel: "landscape-cds",
    title: "张家港地块四",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201805 张家港地块四",
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/zhangjiagang-4/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/zhangjiagang-4/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/zhangjiagang-4/03.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/zhangjiagang-4/04.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/zhangjiagang-4/05.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/zhangjiagang-4/06.webp" }],
  }
,
  {
    id: "deyang-shifanqu",
    channel: "landscape-cds",
    title: "德阳示范区",
    year: "2026",
    summary: "",
    body: "",
    consent: "pending",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201805 德阳示范区",
    media: [],
  }
,
  {
    id: "tianmen-shangkun",
    channel: "landscape-cds",
    title: "天门上坤",
    year: "2026",
    summary: "",
    body: "",
    consent: "pending",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201806 上坤",
    media: [],
  }
,
  {
    id: "shimao-jingzhou",
    channel: "landscape-cds",
    title: "世茂荆州",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201808 世茂荆州",
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-jingzhou/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-jingzhou/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-jingzhou/03.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-jingzhou/04.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-jingzhou/05.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-jingzhou/06.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-jingzhou/07.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-jingzhou/08.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-jingzhou/09.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-jingzhou/10.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-jingzhou/11.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-jingzhou/12.webp" }, { kind: "image", label: "效果图 13", src: "landscape-cds/shimao-jingzhou/13.webp" }, { kind: "image", label: "效果图 14", src: "landscape-cds/shimao-jingzhou/14.webp" }, { kind: "image", label: "效果图 15", src: "landscape-cds/shimao-jingzhou/15.webp" }, { kind: "image", label: "效果图 16", src: "landscape-cds/shimao-jingzhou/16.webp" }, { kind: "image", label: "效果图 17", src: "landscape-cds/shimao-jingzhou/17.webp" }, { kind: "image", label: "效果图 18", src: "landscape-cds/shimao-jingzhou/18.webp" }, { kind: "image", label: "效果图 19", src: "landscape-cds/shimao-jingzhou/19.webp" }, { kind: "image", label: "效果图 20", src: "landscape-cds/shimao-jingzhou/20.webp" }],
  }
,
  {
    id: "shimao-hefei-9",
    channel: "landscape-cds",
    title: "世茂合肥9#",
    year: "2026",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201905 合肥9#",
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-hefei-9/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-hefei-9/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-hefei-9/03.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-hefei-9/04.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-hefei-9/05.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-hefei-9/06.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-hefei-9/07.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-hefei-9/08.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-hefei-9/09.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-hefei-9/10.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-hefei-9/11.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-hefei-9/12.webp" }],
  }
];

/**
 * 全站唯一作品内容池。未授权人像与无图空壳不进访客查询。
 */
export const workRecords: WorkRecord[] = [...initialWorks, ...lineSimulationWorks, ...registeredWorks];

function isPublished(work: WorkRecord): boolean {
  return !work.consent || work.consent === "granted";
}

/**
 * 列出某细目下对访客可见的作品，默认按有效日期倒序。
 */
export function listPublishedWorks(channel: string): WorkRecord[] {
  return sortByEffectiveDateDescending(
    workRecords.filter((item) => item.channel === channel && isPublished(item)),
  );
}

/**
 * 列出全部对访客可见的作品，默认按有效日期倒序。
 */
export function listAllPublishedWorks(): WorkRecord[] {
  return sortByEffectiveDateDescending(workRecords.filter(isPublished));
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
  const work = workRecords.find((item) => item.channel === channel && item.id === id);
  return work && isPublished(work) ? work : undefined;
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
 * 列出访客可见的摄影作品，默认按完整拍摄日期倒序。
 */
export function listPublishedPhotoWorks(): WorkRecord[] {
  const allowed = new Set<string>(photoWorkChannels);
  return sortByEffectiveDateDescending(
    workRecords.filter((item) => allowed.has(item.channel) && isPublished(item)),
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
 * 按完整有效日期排序；同日按标题与 id 稳定排序。
 */
export function sortPhotoWorks(works: WorkRecord[], sort: "asc" | "desc"): WorkRecord[] {
  const descending = sortByEffectiveDateDescending(works);
  if (sort === "desc") {
    return descending;
  }
  return descending.sort((a, b) => {
    const dateDiff = effectiveDate(a).localeCompare(effectiveDate(b));
    if (dateDiff !== 0) {
      return dateDiff;
    }
    const titleDiff = a.title.localeCompare(b.title, "zh-CN");
    return titleDiff !== 0 ? titleDiff : a.id.localeCompare(b.id);
  });
}

export type PhotoCatalogFrame = {
  work: WorkRecord;
  media: WorkMedia;
  src: string;
};

/**
 * 把已发布摄影作品摊成静帧；帧先继承所在记录的 channel。
 * //TODO: 帧级题材字段
 * //TODO: 历史重复批次合并为一次外出
 */
export function listPublishedPhotoFrames(): PhotoCatalogFrame[] {
  const frames: PhotoCatalogFrame[] = [];
  for (const work of listPublishedPhotoWorks()) {
    for (const media of listWorkImages(work)) {
      if (!media.src) {
        continue;
      }
      frames.push({ work, media, src: media.src });
    }
  }
  return frames;
}

/**
 * 按维度过滤摊帧：维度内并集，维度间交集。题材暂读记录 channel。
 */
export function filterPhotoFrames(frames: PhotoCatalogFrame[], query: PhotoCatalogQuery): PhotoCatalogFrame[] {
  return frames.filter((frame) => {
    const work = frame.work;
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
 * 按拍摄日期排序摊帧；同一次外出保持 media 原序。
 */
export function sortPhotoFrames(frames: PhotoCatalogFrame[], sort: "asc" | "desc"): PhotoCatalogFrame[] {
  const decorated = frames.map((frame) => ({
    frame,
    date: effectiveDate(frame.work),
    mediaIndex: frame.work.media.indexOf(frame.media),
  }));
  decorated.sort((a, b) => {
    const dateDiff = a.date.localeCompare(b.date);
    if (dateDiff !== 0) {
      return sort === "asc" ? dateDiff : -dateDiff;
    }
    const titleDiff = a.frame.work.title.localeCompare(b.frame.work.title, "zh-CN");
    if (titleDiff !== 0) {
      return titleDiff;
    }
    const idDiff = a.frame.work.id.localeCompare(b.frame.work.id);
    if (idDiff !== 0) {
      return idDiff;
    }
    return a.mediaIndex - b.mediaIndex;
  });
  return decorated.map((item) => item.frame);
}
