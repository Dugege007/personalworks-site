import { initialWorkProjects } from "./initialWorkProjects";
import { lexicon } from "./lexicon";
import { toListedMedia } from "./listedMedia";
import {
  filterPhotoFrames,
  photoExhibitTag,
  photoExhibitTitle,
  photoTypeKeys,
  themesOfPhotoFrame,
} from "./photoFacet";
import { photoSizes } from "./photoSizes";
import { photoTakenAt } from "./photoExif";
import { comparePhotoFrameFacts, type PhotoFrameSortRule } from "./photoFrameSort";
import { contentDraw, starsOf } from "./stars";
import { comparePhotoTime, photoTimeKey, sortByEffectiveDateDescending, startedOnDate } from "./workDates";

export type { PhotoCatalogQuery, PhotoFacetOccupancy } from "./photoFacet";
export {
  disabledPhotoFacetValues,
  filterPhotoFrames,
  filterPhotoWorks,
  occupiedPhotoFacetsFromFrames,
  occupiedPhotoFacetsFromWorks,
  customPhotoTags,
  orderPhotoCustomTags,
  photoTagResourceCounts,
  disabledPrimaryFacets,
  isPhotoDefaultTag,
  photoPrimaryFacetOrder,
  photoTypeChipLabel,
  photoTypeKeys,
  photoUntaggedKey,
  primaryFacetValues,
  selectedPrimaryFacets,
  themesOfPhotoFrame,
  themesOfPhotoWork,
} from "./photoFacet";

export type WorkChannel =
  | typeof lexicon.digitalTwin.key
  | typeof lexicon.lineSimulation.key
  | typeof lexicon.landscapeRendering.key
  | typeof lexicon.landscapeCDs.key
  | typeof lexicon.landscapePhoto.key
  | typeof lexicon.humanistPhoto.key
  | typeof lexicon.portraitPhoto.key
  | typeof lexicon.realWorldPhoto.key
  | typeof lexicon.gamePhoto.key
  | typeof lexicon.aiPhoto.key;

export type WorkMedia = {
  kind: "image" | "video";
  label: string;
  src?: string;
  poster?: string;
  displayName?: string;
  description?: string;
  /** 冻结类型键，可多值。 */
  themes?: readonly string[];
  /** 自由标签，不含年份、不含类型键。 */
  tags?: readonly string[];
  /** 0～5 星。缺省视为 0，不写出 0。 */
  stars?: 0 | 1 | 2 | 3 | 4 | 5;
};

export { starsOf } from "./stars";

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
  /** 作品级类型并集；缺省则由帧 themes 或旧 channel 推导。 */
  themes?: readonly string[];
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
  /** 已隐藏资源的星级。不进访客页，不进随机展示。 */
  hiddenStars?: Readonly<Record<string, 1 | 2 | 3 | 4 | 5>>;
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
 * 已发布静帧里可进入随机池的一张。0 星返回空。
 */
export function drawOfImage(media: WorkMedia): { src: string; weight: number } | null {
  if (media.kind === "video" || !media.src) {
    return null;
  }
  return contentDraw(media.src, media);
}

/**
 * 与封面选取同一张，但 0 星不进入随机池。
 */
export function drawOfCover(work: { media: WorkMedia[] }): { src: string; weight: number } | null {
  const listed = work.media.filter((item) => item.src);
  const image = listed.find((item) => item.kind !== "video");
  if (image?.src) {
    return contentDraw(image.src, image);
  }
  const video = listed.find((item) => item.kind === "video" && item.poster);
  if (video?.poster) {
    return contentDraw(video.poster, video);
  }
  return null;
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
  stars?: number;
  /** 非内容层回退图，不按 0 星剔除。 */
  fixed?: boolean;
};

/**
 * 摄影详情地址。现实摄影、游戏摄影、AI摄影的 id 即中转站项目夹名。
 */
export function hrefForPhotoWork(work: WorkRecord): string {
  return `/${lexicon.photography.key}/${work.channel}/${encodeURIComponent(work.id)}`;
}

/**
 * 显影头图图池：已发布、类型含风光的静帧，按有效日期倒序。
 */
export function listLandscapeHeroFrames(): HomeHeroFrame[] {
  const frames: HomeHeroFrame[] = [];
  const seen = new Set<string>();
  for (const work of listPublishedPhotoWorks()) {
    const href = hrefForPhotoWork(work);
    for (const media of listWorkImages(work)) {
      if (!media.src || seen.has(media.src)) {
        continue;
      }
      if (!themesOfPhotoFrame(work, media).includes(lexicon.landscapePhoto.key)) {
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
        stars: starsOf(media),
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
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-beijing-yidu/01.webp", stars: 5 }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-beijing-yidu/08.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-beijing-yidu/09.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-beijing-yidu/10.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-beijing-yidu/101.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-beijing-yidu/103.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-beijing-yidu/120.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-beijing-yidu/121.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-beijing-yidu/127.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-beijing-yidu/130.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-beijing-yidu/131.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-beijing-yidu/132.webp" }, { kind: "image", label: "效果图 13", src: "landscape-cds/shimao-beijing-yidu/140.webp" }, { kind: "image", label: "效果图 14", src: "landscape-cds/shimao-beijing-yidu/147.webp" }, { kind: "image", label: "效果图 15", src: "landscape-cds/shimao-beijing-yidu/148.webp" }, { kind: "image", label: "效果图 16", src: "landscape-cds/shimao-beijing-yidu/149.webp" }, { kind: "image", label: "效果图 17", src: "landscape-cds/shimao-beijing-yidu/150.webp" }, { kind: "image", label: "效果图 18", src: "landscape-cds/shimao-beijing-yidu/154.webp" }, { kind: "image", label: "效果图 19", src: "landscape-cds/shimao-beijing-yidu/159.webp" }, { kind: "image", label: "效果图 20", src: "landscape-cds/shimao-beijing-yidu/162.webp" }, { kind: "image", label: "效果图 21", src: "landscape-cds/shimao-beijing-yidu/163.webp" }, { kind: "image", label: "效果图 22", src: "landscape-cds/shimao-beijing-yidu/164.webp" }, { kind: "image", label: "效果图 23", src: "landscape-cds/shimao-beijing-yidu/165.webp" }, { kind: "image", label: "效果图 24", src: "landscape-cds/shimao-beijing-yidu/166.webp" }, { kind: "image", label: "效果图 25", src: "landscape-cds/shimao-beijing-yidu/182.webp" }, { kind: "image", label: "效果图 26", src: "landscape-cds/shimao-beijing-yidu/184.webp" }, { kind: "image", label: "效果图 27", src: "landscape-cds/shimao-beijing-yidu/33.webp" }, { kind: "image", label: "效果图 28", src: "landscape-cds/shimao-beijing-yidu/34.webp" }, { kind: "image", label: "效果图 29", src: "landscape-cds/shimao-beijing-yidu/35.webp" }, { kind: "image", label: "效果图 30", src: "landscape-cds/shimao-beijing-yidu/36.webp", stars: 5 }, { kind: "image", label: "效果图 31", src: "landscape-cds/shimao-beijing-yidu/39.webp", stars: 5 }, { kind: "image", label: "效果图 32", src: "landscape-cds/shimao-beijing-yidu/40.webp", stars: 5 }, { kind: "image", label: "效果图 33", src: "landscape-cds/shimao-beijing-yidu/41.webp", stars: 5 }, { kind: "image", label: "效果图 34", src: "landscape-cds/shimao-beijing-yidu/44.webp" }, { kind: "image", label: "效果图 35", src: "landscape-cds/shimao-beijing-yidu/45.webp" }, { kind: "image", label: "效果图 36", src: "landscape-cds/shimao-beijing-yidu/46.webp" }, { kind: "image", label: "效果图 37", src: "landscape-cds/shimao-beijing-yidu/49.webp" }, { kind: "image", label: "效果图 38", src: "landscape-cds/shimao-beijing-yidu/50.webp" }, { kind: "image", label: "效果图 39", src: "landscape-cds/shimao-beijing-yidu/51.webp" }, { kind: "image", label: "效果图 40", src: "landscape-cds/shimao-beijing-yidu/52.webp" }, { kind: "image", label: "效果图 41", src: "landscape-cds/shimao-beijing-yidu/53.webp" }, { kind: "image", label: "效果图 42", src: "landscape-cds/shimao-beijing-yidu/54.webp" }, { kind: "image", label: "效果图 43", src: "landscape-cds/shimao-beijing-yidu/55.webp" }, { kind: "image", label: "效果图 44", src: "landscape-cds/shimao-beijing-yidu/56.webp" }, { kind: "image", label: "效果图 45", src: "landscape-cds/shimao-beijing-yidu/57.webp" }, { kind: "image", label: "效果图 46", src: "landscape-cds/shimao-beijing-yidu/58.webp" }, { kind: "image", label: "效果图 47", src: "landscape-cds/shimao-beijing-yidu/59.webp" }, { kind: "image", label: "效果图 48", src: "landscape-cds/shimao-beijing-yidu/60.webp" }, { kind: "image", label: "效果图 49", src: "landscape-cds/shimao-beijing-yidu/61.webp" }, { kind: "image", label: "效果图 50", src: "landscape-cds/shimao-beijing-yidu/62.webp" }, { kind: "image", label: "效果图 51", src: "landscape-cds/shimao-beijing-yidu/72.webp" }, { kind: "image", label: "效果图 52", src: "landscape-cds/shimao-beijing-yidu/73.webp" }, { kind: "image", label: "效果图 53", src: "landscape-cds/shimao-beijing-yidu/74.webp" }, { kind: "image", label: "效果图 54", src: "landscape-cds/shimao-beijing-yidu/75.webp" }, { kind: "image", label: "效果图 55", src: "landscape-cds/shimao-beijing-yidu/78.webp" }, { kind: "image", label: "效果图 56", src: "landscape-cds/shimao-beijing-yidu/79.webp" }, { kind: "image", label: "效果图 57", src: "landscape-cds/shimao-beijing-yidu/80.webp" }, { kind: "image", label: "效果图 58", src: "landscape-cds/shimao-beijing-yidu/81.webp" }, { kind: "image", label: "效果图 59", src: "landscape-cds/shimao-beijing-yidu/82.webp" }, { kind: "image", label: "效果图 60", src: "landscape-cds/shimao-beijing-yidu/83.webp" }, { kind: "image", label: "效果图 61", src: "landscape-cds/shimao-beijing-yidu/84.webp" }, { kind: "image", label: "效果图 62", src: "landscape-cds/shimao-beijing-yidu/85.webp" }, { kind: "image", label: "效果图 63", src: "landscape-cds/shimao-beijing-yidu/86.webp" }, { kind: "image", label: "效果图 64", src: "landscape-cds/shimao-beijing-yidu/87.webp" }],
  }
,
  {
    id: "vandeviele",
    channel: "digital-twin",
    title: "无锡范德威尔",
    year: "2025",
    startedOn: "2025-02-20",
    place: "无锡",
    summary: "注塑车间数字孪生；钣金车间加工站点设计动画演示。",
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
    summary: "裁剪车架数字孪生；上线截图演示。",
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
    summary: "尝试使用FlexSim搭建某工厂车间的装配线，并产生KPI报表，用于分析产能与提供改造方案。",
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
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-jiyang/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-jiyang/02.webp", stars: 5 }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-jiyang/03.webp", stars: 5 }],
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
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/zhangjiagang-4/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/zhangjiagang-4/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/zhangjiagang-4/03.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/zhangjiagang-4/04.webp", stars: 3 }, { kind: "image", label: "效果图 05", src: "landscape-cds/zhangjiagang-4/05.webp", stars: 3 }, { kind: "image", label: "效果图 06", src: "landscape-cds/zhangjiagang-4/06.webp" }],
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
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-jingzhou/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-jingzhou/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-jingzhou/03.webp" }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-jingzhou/04.webp" }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-jingzhou/05.webp", stars: 5 }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-jingzhou/06.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-jingzhou/07.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-jingzhou/08.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-jingzhou/09.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-jingzhou/10.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-jingzhou/11.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-jingzhou/12.webp" }, { kind: "image", label: "效果图 13", src: "landscape-cds/shimao-jingzhou/13.webp" }, { kind: "image", label: "效果图 14", src: "landscape-cds/shimao-jingzhou/14.webp" }, { kind: "image", label: "效果图 15", src: "landscape-cds/shimao-jingzhou/15.webp" }, { kind: "image", label: "效果图 16", src: "landscape-cds/shimao-jingzhou/16.webp" }, { kind: "image", label: "效果图 17", src: "landscape-cds/shimao-jingzhou/17.webp" }, { kind: "image", label: "效果图 18", src: "landscape-cds/shimao-jingzhou/18.webp" }, { kind: "image", label: "效果图 19", src: "landscape-cds/shimao-jingzhou/19.webp" }, { kind: "image", label: "效果图 20", src: "landscape-cds/shimao-jingzhou/20.webp", stars: 3 }],
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
    media: [{ kind: "image", label: "效果图 01", src: "landscape-cds/shimao-hefei-9/01.webp" }, { kind: "image", label: "效果图 02", src: "landscape-cds/shimao-hefei-9/02.webp" }, { kind: "image", label: "效果图 03", src: "landscape-cds/shimao-hefei-9/03.webp", stars: 5 }, { kind: "image", label: "效果图 04", src: "landscape-cds/shimao-hefei-9/04.webp", stars: 5 }, { kind: "image", label: "效果图 05", src: "landscape-cds/shimao-hefei-9/05.webp" }, { kind: "image", label: "效果图 06", src: "landscape-cds/shimao-hefei-9/06.webp" }, { kind: "image", label: "效果图 07", src: "landscape-cds/shimao-hefei-9/07.webp" }, { kind: "image", label: "效果图 08", src: "landscape-cds/shimao-hefei-9/08.webp" }, { kind: "image", label: "效果图 09", src: "landscape-cds/shimao-hefei-9/09.webp" }, { kind: "image", label: "效果图 10", src: "landscape-cds/shimao-hefei-9/10.webp" }, { kind: "image", label: "效果图 11", src: "landscape-cds/shimao-hefei-9/11.webp" }, { kind: "image", label: "效果图 12", src: "landscape-cds/shimao-hefei-9/12.webp" }],
  }


,
  {
    id: "20191125 新加坡",
    channel: "real-world-photo",
    title: "新加坡",
    year: "2019",
    startedOn: "2019-11-25",
    place: "新加坡",
    summary: "道田景观公司团建，新加坡旅行",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20191125 新加坡",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"P1330899-3","src":"photo/real-world-photo/20191125 新加坡/05.webp","themes":["landscape-photo"],"tags":["城市","夜景"], "displayName": "金沙酒店远眺", "stars": 5 }, {"kind":"image","label":"P1340964","src":"photo/real-world-photo/20191125 新加坡/06.webp","themes":["landscape-photo"],"tags":["城市"], "stars": 5 }, {"kind":"image","label":"P1350049-3","src":"photo/real-world-photo/20191125 新加坡/07.webp","themes":["landscape-photo"],"tags":["城市"], "displayName": "鱼尾狮公园", "stars": 5 }, {"kind":"image","label":"P1350052-Enhanced-4","src":"photo/real-world-photo/20191125 新加坡/08.webp","themes":["landscape-photo"],"tags":["城市"], "displayName": "市政大厦", "stars": 5 }],
  }
,
  {
    id: "20211119 南京",
    channel: "real-world-photo",
    title: "南京",
    year: "2021",
    startedOn: "2021-11-19",
    place: "南京",
    summary: "和老朋友去南京",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20211119 南京",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"P1588490","src":"photo/real-world-photo/20211119 南京/03.webp","tags":["随拍","夜景","街头"], "stars": 5 }, {"kind":"image","label":"P1599032","src":"photo/real-world-photo/20211119 南京/04.webp","themes":["landscape-photo"],"tags":["城市"], "stars": 5 }],
  }
,
  {
    id: "20220912 鹤壁 星空",
    channel: "real-world-photo",
    title: "鹤壁 星空",
    year: "2022",
    startedOn: "2022-09-12",
    place: "鹤壁",
    summary: "去城市边缘喂蚊子",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20220912 鹤壁 星空",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"20220912-106","src":"photo/real-world-photo/20220912 鹤壁 星空/02.webp","themes":["landscape-photo"],"tags":["夜景","星空"], "stars": 5 }],
  }
,
  {
    id: "20230903 苏州 大阳山",
    channel: "real-world-photo",
    title: "苏州 大阳山",
    year: "2023",
    startedOn: "2023-09-03",
    place: "苏州",
    summary: "和阿岳、大黄、文章一起去徒步",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20230903 苏州 大阳山",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"P1360849","src":"photo/real-world-photo/20230903 苏州 大阳山/05.webp","themes":["landscape-photo"],"tags":["古建"], "stars": 5 }, {"kind":"image","label":"P1360934","src":"photo/real-world-photo/20230903 苏州 大阳山/06.webp","themes":["landscape-photo"],"tags":["城市"], "stars": 5 }, {"kind":"image","label":"P1370007","src":"photo/real-world-photo/20230903 苏州 大阳山/07.webp","themes":["landscape-photo"],"tags":["山景"], "stars": 3 }, {"kind":"image","label":"P1370159","src":"photo/real-world-photo/20230903 苏州 大阳山/08.webp","tags":["随拍","街头"], "stars": 5 }],
  }
,
  {
    id: "20231115 南昌 滕王阁",
    channel: "real-world-photo",
    title: "南昌 滕王阁",
    year: "2023",
    startedOn: "2023-11-15",
    place: "南昌",
    summary: "和阿岳去南昌",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20231115 南昌 滕王阁",
    themes: ["landscape-photo", "humanist-photo"],

    media: [{"kind":"image","label":"P1370776","src":"photo/real-world-photo/20231115 南昌 滕王阁/04.webp","themes":["humanist-photo"],"tags":["古建"], "stars": 3 }, {"kind":"image","label":"P1370939","src":"photo/real-world-photo/20231115 南昌 滕王阁/05.webp","themes":["humanist-photo"],"tags":["夜景"], "stars": 5 }, {"kind":"image","label":"P1370967","src":"photo/real-world-photo/20231115 南昌 滕王阁/06.webp","themes":["landscape-photo"],"tags":["夜景","古建"], "stars": 5 }],
  }
,
  {
    id: "20241002 鹤壁 星空",
    channel: "real-world-photo",
    title: "鹤壁 星空",
    year: "2024",
    startedOn: "2024-10-02",
    place: "鹤壁",
    summary: "和阿岳去水库边拍星空",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20241002 鹤壁 星空",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"DSC00768","src":"photo/real-world-photo/20241002 鹤壁 星空/02.webp","themes":["landscape-photo"],"tags":["夜景","星空"], "stars": 5 }],
  }
,
  {
    id: "20250413 上海 静安寺",
    channel: "real-world-photo",
    title: "上海 静安寺",
    year: "2025",
    startedOn: "2025-04-13",
    place: "上海",
    summary: "来拜拜佛",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20250413 上海 静安寺",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"DSC04068","src":"photo/real-world-photo/20250413 上海 静安寺/05.webp","themes":["landscape-photo"],"tags":["古建","寺庙"], "stars": 5 }, {"kind":"image","label":"DSC04069","src":"photo/real-world-photo/20250413 上海 静安寺/06.webp","themes":["landscape-photo"],"tags":["古建","寺庙"], "stars": 5 }, {"kind":"image","label":"DSC04073","src":"photo/real-world-photo/20250413 上海 静安寺/07.webp","themes":["landscape-photo"],"tags":["古建","寺庙"], "stars": 3 }, {"kind":"image","label":"DSC04081","src":"photo/real-world-photo/20250413 上海 静安寺/08.webp","themes":["landscape-photo"],"tags":["古建","寺庙"], "stars": 4 }],
  }
,
  {
    id: "20250519 安阳 殷墟博物馆",
    channel: "real-world-photo",
    title: "安阳 殷墟博物馆",
    year: "2025",
    startedOn: "2025-05-19",
    place: "安阳",
    summary: "“殷墟我向往已久”",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20250519 安阳 殷墟博物馆",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"DSC05712","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/13.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC05716","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/14.webp","tags":["展馆"], "stars": 2 }, {"kind":"image","label":"DSC05717","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/15.webp","tags":["展馆","建筑"], "stars": 4 }, {"kind":"image","label":"DSC05724","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/16.webp","tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC05730","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/17.webp","tags":["展馆"], "stars": 5 }, {"kind":"image","label":"DSC05732","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/18.webp","tags":["展馆"], "stars": 2 }, {"kind":"image","label":"DSC05747","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/19.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC05771","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/20.webp","tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC05834","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/21.webp","tags":["展馆"], "stars": 5 }, {"kind":"image","label":"DSC05879","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/22.webp","themes":["landscape-photo"],"tags":["展馆","建筑"], "stars": 3 }, {"kind":"image","label":"DSC06020","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/23.webp","tags":["随拍","街头"], "stars": 3 }, {"kind":"image","label":"DSC06092","src":"photo/real-world-photo/20250519 安阳 殷墟博物馆/24.webp","tags":["随拍","街头"], "stars": 2 }],
  }
,
  {
    id: "20250531 上海 龙美术馆",
    channel: "real-world-photo",
    title: "上海 龙美术馆",
    year: "2025",
    startedOn: "2025-05-31",
    place: "上海",
    summary: "出来拍照",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20250531 上海 龙美术馆",
    themes: ["humanist-photo"],

    media: [{"kind":"image","label":"DSC06877","src":"photo/real-world-photo/20250531 上海 龙美术馆/03.webp","themes":["humanist-photo"],"tags":["街头","雕塑"], "stars": 4 }, {"kind":"image","label":"DSC06881","src":"photo/real-world-photo/20250531 上海 龙美术馆/04.webp","themes":["humanist-photo"],"tags":["街头"], "stars": 3 }],
  }
,
  {
    id: "20250713 杭州 黑神话展",
    channel: "real-world-photo",
    title: "杭州 黑神话展",
    year: "2025",
    startedOn: "2025-07-13",
    place: "杭州",
    summary: "和陆哥去看展",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20250713 杭州 黑神话展",

    media: [{"kind":"image","label":"DSC07631","src":"photo/real-world-photo/20250713 杭州 黑神话展/12.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07707","src":"photo/real-world-photo/20250713 杭州 黑神话展/13.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07718","src":"photo/real-world-photo/20250713 杭州 黑神话展/14.webp","tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC07791","src":"photo/real-world-photo/20250713 杭州 黑神话展/15.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07825","src":"photo/real-world-photo/20250713 杭州 黑神话展/16.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07845","src":"photo/real-world-photo/20250713 杭州 黑神话展/17.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07887","src":"photo/real-world-photo/20250713 杭州 黑神话展/18.webp","tags":["展馆","雕塑"], "stars": 4 }, {"kind":"image","label":"DSC07889","src":"photo/real-world-photo/20250713 杭州 黑神话展/19.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07921","src":"photo/real-world-photo/20250713 杭州 黑神话展/20.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07931","src":"photo/real-world-photo/20250713 杭州 黑神话展/21.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07956","src":"photo/real-world-photo/20250713 杭州 黑神话展/22.webp","tags":["展馆"], "stars": 3 }],
  }
,
  {
    id: "20251004 杭州 九溪十八涧",
    channel: "real-world-photo",
    title: "杭州 九溪十八涧",
    year: "2025",
    startedOn: "2025-10-04",
    place: "杭州",
    summary: "徒步随拍",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20251004 杭州 九溪十八涧",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"DSC08058","src":"photo/real-world-photo/20251004 杭州 九溪十八涧/04.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 5 }, {"kind":"image","label":"DSC08154","src":"photo/real-world-photo/20251004 杭州 九溪十八涧/05.webp","themes":["landscape-photo"],"tags":["山景"], "stars": 5 }, {"kind":"image","label":"DSC08194","src":"photo/real-world-photo/20251004 杭州 九溪十八涧/06.webp","themes":["landscape-photo"],"tags":["山景"], "stars": 5 }],
  }
,
  {
    id: "20251008 重庆 渝中区",
    channel: "real-world-photo",
    title: "重庆 渝中区",
    year: "2025",
    startedOn: "2025-10-08",
    place: "重庆",
    summary: "梦幻般的钢铁丛林",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20251008 重庆 渝中区",
    themes: ["landscape-photo"],

    media: [{"kind":"image","label":"DSC09551-已增强-降噪","src":"photo/real-world-photo/20251008 重庆 渝中区/06.webp","themes":["landscape-photo"],"tags":["夜景","城市"], "stars": 5 }, {"kind":"image","label":"DSC09558-已增强-降噪","src":"photo/real-world-photo/20251008 重庆 渝中区/07.webp","themes":["landscape-photo"],"tags":["夜景","城市"], "stars": 4 }, {"kind":"image","label":"DSC09632-已增强-降噪","src":"photo/real-world-photo/20251008 重庆 渝中区/08.webp","themes":["landscape-photo"],"tags":["夜景","城市"], "stars": 5 }, {"kind":"image","label":"DSC09668-已增强-降噪","src":"photo/real-world-photo/20251008 重庆 渝中区/09.webp","themes":["landscape-photo"],"tags":["夜景","城市"], "stars": 5 }, {"kind":"image","label":"DSC09674-已增强-降噪","src":"photo/real-world-photo/20251008 重庆 渝中区/10.webp","themes":["landscape-photo"],"tags":["夜景","城市"], "stars": 5 }],
  }
,
  {
    id: "20251009 重庆 南岸区",
    channel: "real-world-photo",
    title: "重庆 南岸区",
    year: "2025",
    startedOn: "2025-10-09",
    place: "重庆",
    summary: "没有逛完，下次还来",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20251009 重庆 南岸区",

    media: [{"kind":"image","label":"DSC00543","src":"photo/real-world-photo/20251009 重庆 南岸区/04.webp","tags":["夜景","城市","蓝调"], "stars": 5 }, {"kind":"image","label":"DSC00552","src":"photo/real-world-photo/20251009 重庆 南岸区/05.webp","tags":["夜景","城市"], "stars": 4 }, {"kind":"image","label":"DSC00826","src":"photo/real-world-photo/20251009 重庆 南岸区/06.webp","tags":["夜景","城市"], "stars": 5 }],
  }
,
  {
    id: "20251010 重庆 涪陵 816核工程遗址",
    channel: "real-world-photo",
    title: "重庆 816核工程遗址",
    year: "2025",
    startedOn: "2025-10-10",
    place: "重庆",
    summary: "“岂曰无名，山河为证”",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20251010 重庆 涪陵 816核工程遗址",

    media: [{"kind":"image","label":"DSC01033","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/08.webp","tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC01043","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/09.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC01051","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/10.webp","tags":["展馆"], "stars": 2 }, {"kind":"image","label":"DSC01075","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/11.webp","tags":["展馆"], "stars": 2 }, {"kind":"image","label":"DSC01127","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/12.webp","tags":["展馆"], "stars": 5 }, {"kind":"image","label":"DSC01142","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/13.webp","tags":["展馆"], "stars": 5 }, {"kind":"image","label":"DSC01158","src":"photo/real-world-photo/20251010 重庆 涪陵 816核工程遗址/14.webp","tags":["展馆"], "stars": 4 }],
  }
,
  {
    id: "20251010 重庆 涪陵 白鹤梁",
    channel: "real-world-photo",
    title: "重庆 白鹤梁",
    year: "2025",
    startedOn: "2025-10-10",
    place: "重庆",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20251010 重庆 涪陵 白鹤梁",
    themes: ["humanist-photo"],

    media: [{"kind":"image","label":"DSC01284","src":"photo/real-world-photo/20251010 重庆 涪陵 白鹤梁/04.webp","themes":["humanist-photo"],"tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC01312","src":"photo/real-world-photo/20251010 重庆 涪陵 白鹤梁/05.webp","tags":["展馆"], "stars": 4 }, {"kind":"image","label":"DSC01319","src":"photo/real-world-photo/20251010 重庆 涪陵 白鹤梁/06.webp","tags":["展馆"], "stars": 3 }],
  }
,
  {
    id: "20260504 舟山 普陀山",
    channel: "real-world-photo",
    title: "舟山 普陀山",
    year: "2026",
    startedOn: "2026-05-04",
    place: "舟山",
    summary: "跟团来拜拜佛",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20260504 舟山 普陀山",
    themes: ["landscape-photo", "humanist-photo"],

    media: [{"kind":"image","label":"DSC01544","src":"photo/real-world-photo/20260504 舟山 普陀山/09.webp","themes":["landscape-photo"],"tags":["动物"], "stars": 5 }, {"kind":"image","label":"DSC01648","src":"photo/real-world-photo/20260504 舟山 普陀山/10.webp","themes":["landscape-photo"],"tags":["动物"], "stars": 5 }, {"kind":"image","label":"DSC01746","src":"photo/real-world-photo/20260504 舟山 普陀山/11.webp","themes":["landscape-photo"],"tags":["寺庙","古建"], "stars": 5 }, {"kind":"image","label":"DSC01767","src":"photo/real-world-photo/20260504 舟山 普陀山/12.webp","themes":["landscape-photo"],"tags":["寺庙","古建","雕塑"], "stars": 4 }, {"kind":"image","label":"DSC01828","src":"photo/real-world-photo/20260504 舟山 普陀山/13.webp","themes":["landscape-photo"], "stars": 5 }, {"kind":"image","label":"DSC01868","src":"photo/real-world-photo/20260504 舟山 普陀山/14.webp","themes":["humanist-photo"],"tags":["寺庙","古建"], "stars": 4 }, {"kind":"image","label":"DSC01880","src":"photo/real-world-photo/20260504 舟山 普陀山/15.webp","themes":["landscape-photo"],"tags":["寺庙","雕塑"], "stars": 3 }, {"kind":"image","label":"DSC01953","src":"photo/real-world-photo/20260504 舟山 普陀山/16.webp","themes":["landscape-photo"],"tags":["寺庙","雕塑"], "stars": 3 }],
  }
,
  {
    id: "20260517 兴义 马岭古道",
    channel: "real-world-photo",
    title: "兴义 马岭古道",
    year: "2026",
    startedOn: "2026-05-17",
    place: "兴义",
    summary: "徒步随拍",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20260517 兴义 马岭古道",
    themes: ["landscape-photo", "humanist-photo", "portrait-photo"],

    media: [{"kind":"image","label":"DSC02092","src":"photo/real-world-photo/20260517 兴义 马岭古道/08.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 5 }, {"kind":"image","label":"DSC02128","src":"photo/real-world-photo/20260517 兴义 马岭古道/09.webp","themes":["landscape-photo","humanist-photo"],"tags":["古建"], "stars": 3 }, {"kind":"image","label":"DSC02165","src":"photo/real-world-photo/20260517 兴义 马岭古道/10.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 5 }, {"kind":"image","label":"DSC02263","src":"photo/real-world-photo/20260517 兴义 马岭古道/11.webp","themes":["landscape-photo"], "stars": 4 }, {"kind":"image","label":"DSC02301","src":"photo/real-world-photo/20260517 兴义 马岭古道/12.webp","themes":["landscape-photo","humanist-photo"],"tags":["江河"], "stars": 5 }, {"kind":"image","label":"DSC02515","src":"photo/real-world-photo/20260517 兴义 马岭古道/13.webp","themes":["portrait-photo"],"tags":["运动","江河"], "stars": 4 }, {"kind":"image","label":"DSC02520","src":"photo/real-world-photo/20260517 兴义 马岭古道/14.webp","themes":["portrait-photo"],"tags":["运动","江河"], "stars": 5 }],
  }
,
  {
    id: "20260618 上海 萤火虫基地",
    channel: "real-world-photo",
    title: "上海 萤火虫基地",
    year: "2026",
    startedOn: "2026-06-18",
    place: "上海",
    summary: "AI推荐我来拍萤火虫，但是这天下雨，没拍到很多",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/20260618 上海 萤火虫基地",
    themes: ["landscape-photo"],

    media: [{ kind: "image", label: "DSC02760-已增强-降噪", src: "photo/real-world-photo/20260618 上海 萤火虫基地/05.webp", themes: ["landscape-photo"], tags: ["夜景", "昆虫"], stars: 4 }, { kind: "image", label: "DSC02762-已增强-降噪", src: "photo/real-world-photo/20260618 上海 萤火虫基地/06.webp", themes: ["landscape-photo"], tags: ["夜景", "昆虫"], stars: 3 }, { kind: "image", label: "DSC02776-已增强-降噪", src: "photo/real-world-photo/20260618 上海 萤火虫基地/08.webp", themes: ["landscape-photo"], tags: ["夜景", "昆虫"], stars: 5 }],
  }
,
  {
    id: "20260814 舟山 东极岛 东福山",
    channel: "real-world-photo",
    title: "舟山 东极岛 东福山",
    year: "2026",
    startedOn: "2026-08-14",
    place: "舟山",
    summary: "徒步随拍",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260814 舟山 东极岛 东福山",
    themes: ["landscape-photo", "humanist-photo", "portrait-photo"],

    media: [{ kind: "image", label: "DSC04248", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/01.webp", themes: ["landscape-photo"], tags: ["海景"], stars: 5 }, { kind: "image", label: "DSC04329", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/02.webp", themes: ["landscape-photo"], tags: ["海景"], stars: 5 }, { kind: "image", label: "DSC04338", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/03.webp", themes: ["landscape-photo"], tags: ["建筑", "山景"], stars: 5 }, { kind: "image", label: "DSC04347", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/04.webp", themes: ["landscape-photo"], tags: ["山景"], stars: 4 }, { kind: "image", label: "DSC04395", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/05.webp", themes: ["landscape-photo"], tags: ["海景"], stars: 5 }, { kind: "image", label: "DSC04457", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/06.webp", themes: ["landscape-photo"], tags: ["海景"], stars: 5 }, { kind: "image", label: "DSC04484", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/07.webp", themes: ["landscape-photo"], tags: ["海景"], stars: 5 }, { kind: "image", label: "DSC04506", src: "photo/real-world-photo/20260814 舟山 东极岛 东福山/08.webp", themes: ["landscape-photo"], tags: ["建筑"], stars: 3 }, {"kind":"image","label":"DSC04301","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/09.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC04318","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/10.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC04517","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/11.webp","themes":["portrait-photo"],"tags":["夜景","蓝调"], "stars": 5 }, {"kind":"image","label":"DSC04523","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/12.webp","themes":["portrait-photo"],"tags":["夜景","蓝调"], "stars": 5 }, {"kind":"image","label":"DSC04562","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/13.webp","themes":["humanist-photo"],"tags":["夜景"], "stars": 4 }, {"kind":"image","label":"DSC04580","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/14.webp","themes":["humanist-photo"],"tags":["夜景"], "stars": 4 }, {"kind":"image","label":"DSC04588","src":"photo/real-world-photo/20260814 舟山 东极岛 东福山/15.webp","themes":["humanist-photo"],"tags":["夜景"], "stars": 5 }],
  }
,
  {
    id: "20260815 舟山 东极岛 庙子湖",
    channel: "real-world-photo",
    title: "舟山 东极岛 庙子湖",
    year: "2026",
    startedOn: "2026-08-15",
    place: "舟山",
    summary: "徒步随拍",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260815 舟山 东极岛 庙子湖",
    themes: ["landscape-photo", "portrait-photo"],

    media: [{"kind":"image","label":"DSC04707","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/01.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 4 }, {"kind":"image","label":"DSC04725","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/02.webp","themes":["landscape-photo"],"tags":["海景"], "stars": 4 }, {"kind":"image","label":"DSC04728","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/03.webp","themes":["landscape-photo"],"tags":["海景"], "stars": 5 }, {"kind":"image","label":"DSC04731","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/04.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 3 }, {"kind":"image","label":"DSC04749","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/05.webp","themes":["landscape-photo"],"tags":["建筑"], "stars": 3 }, {"kind":"image","label":"DSC04680","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/06.webp","themes":["portrait-photo"],"tags":["海景"], "stars": 4 }, {"kind":"image","label":"DSC04710_2026-08-16-21-36-31-915_创意图","src":"photo/real-world-photo/20260815 舟山 东极岛 庙子湖/07.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 3 }],
  }
,
  {
    id: "20260725 上海 南京路 小不点",
    channel: "real-world-photo",
    title: "上海 南京路 小不点",
    year: "2026",
    startedOn: "2026-07-25",
    place: "上海",
    summary: "和小不点出来逛街",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260725 上海 南京路 小不点",
    themes: ["portrait-photo"],

    media: [{"kind":"image","label":"DSC04165_(2)","src":"photo/real-world-photo/20260725 上海 南京路 小不点/01.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC04166_(3)","src":"photo/real-world-photo/20260725 上海 南京路 小不点/02.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC04210_(2)","src":"photo/real-world-photo/20260725 上海 南京路 小不点/03.webp","themes":["portrait-photo"],"tags":["夜景"], "stars": 5 }],
  }
,
  {
    id: "20260912 上海 蟠龙天地",
    channel: "real-world-photo",
    title: "上海 蟠龙天地",
    year: "2026",
    startedOn: "2026-09-12",
    place: "上海",
    summary: "和子良出来逛街",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260912 上海 蟠龙天地",
    themes: ["humanist-photo"],

    media: [{"kind":"image","label":"DSC06148","src":"photo/real-world-photo/20260912 上海 蟠龙天地/01.webp","themes":["humanist-photo"],"tags":["街头"], "stars": 3 }, {"kind":"image","label":"DSC06151","src":"photo/real-world-photo/20260912 上海 蟠龙天地/02.webp","tags":["随拍","街头","夜景"], "stars": 3 }, {"kind":"image","label":"DSC06180","src":"photo/real-world-photo/20260912 上海 蟠龙天地/03.webp","tags":["随拍","街头","夜景"] }, {"kind":"image","label":"DSC06189","src":"photo/real-world-photo/20260912 上海 蟠龙天地/04.webp","tags":["随拍","街头","夜景"], "stars": 3 }, {"kind":"image","label":"DSC06214","src":"photo/real-world-photo/20260912 上海 蟠龙天地/05.webp","tags":["随拍","街头"], "stars": 4 }, {"kind":"image","label":"DSC06231","src":"photo/real-world-photo/20260912 上海 蟠龙天地/06.webp","tags":["随拍","街头","夜景"], "stars": 4 }],
  }
,
  {
    id: "20260926 南京 凡人展",
    channel: "real-world-photo",
    title: "南京 凡人展",
    year: "2026",
    startedOn: "2026-09-26",
    place: "南京",
    summary: "去看凡人修仙传展，Coser挺还原的，没看到结婴银月有点遗憾",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260926 南京 凡人展",
    themes: ["portrait-photo"],

    media: [{ kind: "image", label: "DSC07437_2026-09-27-14-13-52-924_创意图", src: "photo/real-world-photo/20260926 南京 凡人展/02.webp", tags: ["展馆", "雕塑"], stars: 3 }, { kind: "image", label: "DSC07438", src: "photo/real-world-photo/20260926 南京 凡人展/03.webp", tags: ["展馆", "雕塑"], stars: 4 }, { kind: "image", label: "DSC07477", src: "photo/real-world-photo/20260926 南京 凡人展/05.webp", themes: ["portrait-photo"], tags: ["展馆", "Cos"], stars: 3 }, { kind: "image", label: "DSC07522_2026-09-27-14-21-15-785_创意图", src: "photo/real-world-photo/20260926 南京 凡人展/06.webp", tags: ["展馆", "Cos"], stars: 5 }, { kind: "image", label: "DSC07540_2026-09-27-14-23-22-151_创意图", src: "photo/real-world-photo/20260926 南京 凡人展/07.webp", tags: ["展馆", "Cos"], stars: 5 }, { kind: "image", label: "DSC07583", src: "photo/real-world-photo/20260926 南京 凡人展/08.webp", themes: ["portrait-photo"], tags: ["展馆", "Cos"], stars: 5 }, {"kind":"image","label":"DSC07446_2026-09-27-14-18-59-568_创意图_(2)","src":"photo/real-world-photo/20260926 南京 凡人展/09.webp","themes":["portrait-photo"],"tags":["展馆","Cos"]}],
  }
,
  {
    id: "20260926 南京 牛首山 佛顶宫",
    channel: "real-world-photo",
    title: "南京 牛首山 佛顶宫",
    year: "2026",
    startedOn: "2026-09-26",
    place: "南京",
    summary: "朋友推荐我来牛首山逛逛",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260926 南京 牛首山 佛顶宫",
    themes: ["landscape-photo", "portrait-photo"],

    media: [{"kind":"image","label":"DSC07627_2026-09-27-20-23-07-228_创意图","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/01.webp","tags":["展馆","建筑"], "stars": 4 }, {"kind":"image","label":"DSC07647","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/02.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07651","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/03.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07657","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/04.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07665","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/05.webp","tags":["展馆","雕塑"], "stars": 4 }, {"kind":"image","label":"DSC07670","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/06.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07680","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/07.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07681","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/08.webp","tags":["展馆"], "stars": 3 }, {"kind":"image","label":"DSC07720_2026-09-27-20-15-31-472_创意图","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/09.webp","themes":["portrait-photo"],"tags":["展馆","建筑"], "stars": 5 }, {"kind":"image","label":"DSC07747","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/10.webp","tags":["展馆"], "stars": 5 }, {"kind":"image","label":"DSC07749","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/11.webp","tags":["展馆","建筑"], "stars": 4 }, {"kind":"image","label":"DSC07752","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/12.webp","tags":["展馆","雕塑"], "stars": 5 }, {"kind":"image","label":"DSC07767_2026-09-27-20-23-52-123_创意图","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/13.webp","tags":["展馆","建筑"], "stars": 5 }, {"kind":"image","label":"DSC07777","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/14.webp","tags":["展馆","建筑"], "stars": 4 }, {"kind":"image","label":"DSC07810","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/15.webp","themes":["landscape-photo"],"tags":["展馆","古建","寺庙","夜景","蓝调"], "stars": 5 }, {"kind":"image","label":"DSC07835","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/16.webp","themes":["landscape-photo"],"tags":["夜景","蓝调"], "stars": 4 }, {"kind":"image","label":"DSC07841","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/17.webp","themes":["landscape-photo"],"tags":["夜景","蓝调"], "stars": 5 }, {"kind":"image","label":"IMG_20260926_164950","src":"photo/real-world-photo/20260926 南京 牛首山 佛顶宫/18.webp","tags":["展馆","建筑"], "stars": 5 }],
  }
,
  {
    id: "20180323 武汉",
    channel: "real-world-photo",
    title: "武汉",
    year: "2018",
    startedOn: "2018-03-23",
    place: "武汉",
    summary: "和同学去武汉",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180323 武汉",
    themes: ["landscape-photo", "portrait-photo"],

    media: [{"kind":"image","label":"P1210021","src":"photo/real-world-photo/20180323 武汉/01.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 5 }, {"kind":"image","label":"P1210046","src":"photo/real-world-photo/20180323 武汉/02.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 3 }, {"kind":"image","label":"P1210048","src":"photo/real-world-photo/20180323 武汉/03.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 5 }, {"kind":"image","label":"P1210068","src":"photo/real-world-photo/20180323 武汉/04.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 4 }, {"kind":"image","label":"P1210110","src":"photo/real-world-photo/20180323 武汉/05.webp","themes":["landscape-photo"],"tags":["湖景"], "stars": 3 }, {"kind":"image","label":"P1210132","src":"photo/real-world-photo/20180323 武汉/06.webp","themes":["portrait-photo"],"tags":["湖景"], "stars": 4 }, {"kind":"image","label":"P1210221","src":"photo/real-world-photo/20180323 武汉/07.webp","themes":["landscape-photo","portrait-photo"],"tags":["湖景"], "stars": 5 }, {"kind":"image","label":"P1210235","src":"photo/real-world-photo/20180323 武汉/08.webp","themes":["portrait-photo"],"tags":["湖景"], "stars": 5 }, {"kind":"image","label":"P1210241","src":"photo/real-world-photo/20180323 武汉/09.webp","themes":["landscape-photo"],"tags":["湖景"], "stars": 3 }, {"kind":"image","label":"P1210310","src":"photo/real-world-photo/20180323 武汉/10.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"P1210335","src":"photo/real-world-photo/20180323 武汉/11.webp","themes":["portrait-photo"], "stars": 2 }, {"kind":"image","label":"P1210417","src":"photo/real-world-photo/20180323 武汉/12.webp","themes":["portrait-photo"],"tags":["随拍","夜景"], "stars": 2 }, {"kind":"image","label":"P1210435","src":"photo/real-world-photo/20180323 武汉/13.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 3 }, {"kind":"image","label":"P1210522","src":"photo/real-world-photo/20180323 武汉/14.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 4 }, {"kind":"image","label":"P1210773","src":"photo/real-world-photo/20180323 武汉/15.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 2 }, {"kind":"image","label":"P1210826","src":"photo/real-world-photo/20180323 武汉/16.webp","tags":["展馆","雕塑"], "displayName": "给给" , "description": "看到这个雕塑我都惊了", "stars": 2 }, {"kind":"image","label":"P1210848","src":"photo/real-world-photo/20180323 武汉/17.webp","tags":["展馆","雕塑"], "stars": 1 }, {"kind":"image","label":"P1220051","src":"photo/real-world-photo/20180323 武汉/18.webp","tags":["随拍","街头"], "stars": 3 }, {"kind":"image","label":"P1220055","src":"photo/real-world-photo/20180323 武汉/19.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 3 }, {"kind":"image","label":"P1220074","src":"photo/real-world-photo/20180323 武汉/20.webp","tags":["展馆","随拍","科技"], "stars": 2 }, {"kind":"image","label":"P1220099","src":"photo/real-world-photo/20180323 武汉/21.webp","themes":["portrait-photo"],"tags":["街头"], "stars": 1 }],
  }
,
  {
    id: "20180406 上海 外滩",
    channel: "real-world-photo",
    title: "上海 外滩",
    year: "2018",
    startedOn: "2018-04-06",
    place: "上海",
    summary: "第一次来上海",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180406 上海 外滩",
    themes: ["landscape-photo"],

    media: [{ kind: "image", label: "P1220178", src: "photo/real-world-photo/20180406 上海 外滩/01.webp", themes: ["landscape-photo"], tags: ["城市", "夜景", "江河"], stars: 1 }],
  }
,
  {
    id: "20180523 毕业啦",
    channel: "real-world-photo",
    title: "毕业啦",
    year: "2018",
    startedOn: "2018-05-23",
    place: "洛阳",
    summary: "整了一些有意思的毕业照",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180523 毕业啦",
    themes: ["portrait-photo"],

    media: [{"kind":"image","label":"P1240226","src":"photo/real-world-photo/20180523 毕业啦/01.webp","themes":["portrait-photo"], "stars": 2 }, {"kind":"image","label":"P1240171","src":"photo/real-world-photo/20180523 毕业啦/02.webp","themes":["portrait-photo"], "displayName": "磊哥", "stars": 4 }, {"kind":"image","label":"P1240367","src":"photo/real-world-photo/20180523 毕业啦/03.webp","themes":["portrait-photo"], "stars": 2 }, {"kind":"image","label":"P1240539","src":"photo/real-world-photo/20180523 毕业啦/04.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"P1240656","src":"photo/real-world-photo/20180523 毕业啦/05.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"P1240675","src":"photo/real-world-photo/20180523 毕业啦/06.webp","themes":["portrait-photo"], "displayName": "磊哥", "stars": 5 }, {"kind":"image","label":"P1240993","src":"photo/real-world-photo/20180523 毕业啦/07.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"P1250121","src":"photo/real-world-photo/20180523 毕业啦/08.webp","themes":["portrait-photo"], "displayName": "文兄", "stars": 5 }, {"kind":"image","label":"P1250214","src":"photo/real-world-photo/20180523 毕业啦/09.webp","themes":["portrait-photo"], "stars": 4 }, {"kind":"image","label":"P1250248","src":"photo/real-world-photo/20180523 毕业啦/10.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"P1250409","src":"photo/real-world-photo/20180523 毕业啦/11.webp","themes":["portrait-photo"],"tags":["运动"], "displayName": "一排大长腿", "stars": 3 }, {"kind":"image","label":"P1250463","src":"photo/real-world-photo/20180523 毕业啦/12.webp","themes":["portrait-photo"], "displayName": "琴湖", "stars": 2 }],
  }
,
  {
    id: "20180728 上海 现场酒吧",
    channel: "real-world-photo",
    title: "上海 现场酒吧",
    year: "2018",
    startedOn: "2018-07-28",
    place: "上海",
    summary: "第二次去酒吧",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180728 上海 现场酒吧",
    themes: ["portrait-photo"],

    media: [{"kind":"image","label":"P1270168","src":"photo/real-world-photo/20180728 上海 现场酒吧/01.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 3 }, {"kind":"image","label":"P1270183","src":"photo/real-world-photo/20180728 上海 现场酒吧/02.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 5 }, {"kind":"image","label":"P1270262","src":"photo/real-world-photo/20180728 上海 现场酒吧/03.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 2 }, {"kind":"image","label":"P1270364","src":"photo/real-world-photo/20180728 上海 现场酒吧/04.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 2 }, {"kind":"image","label":"P1270522","src":"photo/real-world-photo/20180728 上海 现场酒吧/05.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 5 }, {"kind":"image","label":"P1270571","src":"photo/real-world-photo/20180728 上海 现场酒吧/06.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 4 }, {"kind":"image","label":"P1270598","src":"photo/real-world-photo/20180728 上海 现场酒吧/07.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 5 }, {"kind":"image","label":"P1270770","src":"photo/real-world-photo/20180728 上海 现场酒吧/08.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 4 }, {"kind":"image","label":"P1270795","src":"photo/real-world-photo/20180728 上海 现场酒吧/09.webp","themes":["portrait-photo"],"tags":["酒吧"], "stars": 4 }],
  }
,
  {
    id: "20260627 南京 JK室外 糖次",
    channel: "real-world-photo",
    title: "南京 JK室外 糖次",
    year: "2026",
    startedOn: "2026-06-27",
    place: "南京",
    summary: "麻家班课堂练习",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260627 南京 JK室外 糖次",
    themes: ["portrait-photo"],

    media: [{ kind: "image", label: "DSC03371", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/01.webp", themes: ["portrait-photo"], stars: 3 }, { kind: "image", label: "DSC03420", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/02.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC03584", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/03.webp", themes: ["portrait-photo"], stars: 4 }, { kind: "image", label: "DSC03586", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/04.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC03730", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/05.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC03760", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/06.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC03772", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/07.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC03803", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/08.webp", themes: ["portrait-photo"], stars: 3 }, { kind: "image", label: "DSC03861", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/09.webp", themes: ["portrait-photo"], stars: 4 }, { kind: "image", label: "DSC03917", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/10.webp", themes: ["portrait-photo"], stars: 3 }, { kind: "image", label: "DSC04000", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/11.webp", themes: ["portrait-photo"], stars: 5 }, { kind: "image", label: "DSC04050", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/12.webp", themes: ["portrait-photo"], tags: ["夜景"], stars: 4 }, { kind: "image", label: "DSC04072", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/13.webp", themes: ["portrait-photo"], tags: ["夜景"], stars: 4 }, { kind: "image", label: "DSC04088", src: "photo/real-world-photo/20260627 南京 JK室外 糖次/14.webp", themes: ["portrait-photo"], tags: ["夜景"], stars: 3 }, {"kind":"image","label":"DSC04022","src":"photo/real-world-photo/20260627 南京 JK室外 糖次/15.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC03929","src":"photo/real-world-photo/20260627 南京 JK室外 糖次/16.webp","themes":["portrait-photo"], "stars": 1 }],
  }
,
  {
    id: "20260627 南京 树野小屋 糖次",
    channel: "real-world-photo",
    title: "南京 树野小屋 糖次",
    year: "2026",
    startedOn: "2026-06-27",
    place: "南京",
    summary: "麻家班课堂练习",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2026/20260627 南京 树野小屋 糖次",
    themes: ["portrait-photo"],

    media: [{"kind":"image","label":"DSC03036","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/01.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC03049","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/02.webp","themes":["portrait-photo"], "stars": 3 }, {"kind":"image","label":"DSC03091","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/03.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03100","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/04.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03204","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/05.webp","themes":["portrait-photo"], "stars": 4 }, {"kind":"image","label":"DSC03241","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/06.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03250","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/07.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03273","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/08.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03280","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/09.webp","themes":["portrait-photo"], "stars": 4 }, {"kind":"image","label":"DSC03297","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/10.webp","themes":["portrait-photo"], "stars": 5 }, {"kind":"image","label":"DSC03314","src":"photo/real-world-photo/20260627 南京 树野小屋 糖次/11.webp","themes":["portrait-photo"], "stars": 4 }],
  }
,
  {
    id: "20180804 上海 ChinaJoy",
    channel: "real-world-photo",
    title: "上海 ChinaJoy",
    year: "2018",
    startedOn: "2018-08-04",
    place: "上海",
    summary: "和老蔡去逛展，富士的颜色真舒服",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180804 上海 ChinaJoy",
    themes: ["humanist-photo", "portrait-photo"],

    media: [{"kind":"image","label":"P1280049","src":"photo/real-world-photo/20180804 上海 ChinaJoy/01.webp","tags":["展馆"]}, {"kind":"image","label":"P1280107","src":"photo/real-world-photo/20180804 上海 ChinaJoy/02.webp","tags":["展馆"]}, {"kind":"image","label":"P1280114","src":"photo/real-world-photo/20180804 上海 ChinaJoy/03.webp","tags":["展馆"]}, {"kind":"image","label":"P1280116","src":"photo/real-world-photo/20180804 上海 ChinaJoy/04.webp","themes":["portrait-photo"],"tags":["展馆","Cos"]}, {"kind":"image","label":"P1280141","src":"photo/real-world-photo/20180804 上海 ChinaJoy/05.webp","tags":["展馆","雕塑"]}, {"kind":"image","label":"DSCF0569","src":"photo/real-world-photo/20180804 上海 ChinaJoy/06.webp","themes":["portrait-photo"],"tags":["展馆"]}, {"kind":"image","label":"DSCF0604","src":"photo/real-world-photo/20180804 上海 ChinaJoy/07.webp","themes":["portrait-photo"],"tags":["展馆"]}, {"kind":"image","label":"DSCF0606","src":"photo/real-world-photo/20180804 上海 ChinaJoy/08.webp","themes":["portrait-photo"],"tags":["展馆"]}, {"kind":"image","label":"DSCF0610","src":"photo/real-world-photo/20180804 上海 ChinaJoy/09.webp","themes":["humanist-photo"],"tags":["展馆"]}, {"kind":"image","label":"DSCF0614","src":"photo/real-world-photo/20180804 上海 ChinaJoy/10.webp","tags":["展馆"]}, {"kind":"image","label":"P1280021","src":"photo/real-world-photo/20180804 上海 ChinaJoy/11.webp","tags":["展馆"]}, {"kind":"image","label":"P1280058","src":"photo/real-world-photo/20180804 上海 ChinaJoy/12.webp","tags":["展馆","雕塑"]}, {"kind":"image","label":"DSCF0571","src":"photo/real-world-photo/20180804 上海 ChinaJoy/13.webp","tags":["展馆","雕塑"]}],
  }
,
  {
    id: "20180902 上海 装饰艺术展",
    channel: "real-world-photo",
    title: "上海 装饰艺术展",
    year: "2018",
    startedOn: "2018-09-02",
    place: "上海",
    summary: "和老蔡去逛展",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20180902 上海 装饰艺术展",
    media: [{"kind":"image","label":"P1280176","src":"photo/real-world-photo/20180902 上海 装饰艺术展/01.webp","tags":["展馆"]}, {"kind":"image","label":"P1280197","src":"photo/real-world-photo/20180902 上海 装饰艺术展/02.webp","tags":["展馆","建筑"]}, {"kind":"image","label":"P1280199","src":"photo/real-world-photo/20180902 上海 装饰艺术展/03.webp","tags":["展馆"]}, {"kind":"image","label":"P1280204","src":"photo/real-world-photo/20180902 上海 装饰艺术展/04.webp","tags":["展馆"]}, {"kind":"image","label":"P1280226","src":"photo/real-world-photo/20180902 上海 装饰艺术展/05.webp","tags":["展馆"]}, {"kind":"image","label":"P1280231","src":"photo/real-world-photo/20180902 上海 装饰艺术展/06.webp","tags":["展馆"]}, {"kind":"image","label":"P1280240","src":"photo/real-world-photo/20180902 上海 装饰艺术展/07.webp","tags":["展馆"]}, {"kind":"image","label":"P1280246","src":"photo/real-world-photo/20180902 上海 装饰艺术展/08.webp","tags":["展馆"]}, {"kind":"image","label":"P1280266","src":"photo/real-world-photo/20180902 上海 装饰艺术展/09.webp","tags":["展馆","建筑"]}, {"kind":"image","label":"P1280278","src":"photo/real-world-photo/20180902 上海 装饰艺术展/10.webp","tags":["展馆"]}, {"kind":"image","label":"P1280329","src":"photo/real-world-photo/20180902 上海 装饰艺术展/11.webp","tags":["展馆","建筑"]}],
  }
,
  {
    id: "20181114 长滩岛",
    channel: "real-world-photo",
    title: "长滩岛",
    year: "2018",
    startedOn: "2018-11-14",
    place: "长滩岛",
    summary: "道田景观公司团建，菲律宾旅行",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20181114 长滩岛",
    themes: ["landscape-photo", "humanist-photo", "portrait-photo"],

    media: [{"kind":"image","label":"P1280439","src":"photo/real-world-photo/20181114 长滩岛/01.webp","themes":["portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1280466","src":"photo/real-world-photo/20181114 长滩岛/02.webp","themes":["landscape-photo"],"tags":["海景"]}, {"kind":"image","label":"P1280527","src":"photo/real-world-photo/20181114 长滩岛/03.webp","themes":["landscape-photo","portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1280557","src":"photo/real-world-photo/20181114 长滩岛/04.webp","themes":["landscape-photo","portrait-photo"],"tags":["金调","海景"]}, {"kind":"image","label":"P1280704","src":"photo/real-world-photo/20181114 长滩岛/05.webp","description":"最满意的一张","themes":["landscape-photo","humanist-photo"],"tags":["海景"]}, {"kind":"image","label":"P1280711","src":"photo/real-world-photo/20181114 长滩岛/06.webp","themes":["portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1280848","src":"photo/real-world-photo/20181114 长滩岛/07.webp","themes":["portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1290065","src":"photo/real-world-photo/20181114 长滩岛/08.webp","themes":["portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1290173","src":"photo/real-world-photo/20181114 长滩岛/09.webp","themes":["portrait-photo"],"tags":["金调","海景"]}, {"kind":"image","label":"P1290174","src":"photo/real-world-photo/20181114 长滩岛/10.webp","themes":["landscape-photo","portrait-photo"],"tags":["金调","海景"]}, {"kind":"image","label":"P1290198","src":"photo/real-world-photo/20181114 长滩岛/11.webp","themes":["landscape-photo","portrait-photo"],"tags":["金调","海景"]}, {"kind":"image","label":"P1290233","src":"photo/real-world-photo/20181114 长滩岛/12.webp","themes":["landscape-photo","portrait-photo"],"tags":["金调","海景"]}, {"kind":"image","label":"P1290360","src":"photo/real-world-photo/20181114 长滩岛/13.webp","themes":["landscape-photo","portrait-photo"],"tags":["海景"]}, {"kind":"image","label":"P1290396","src":"photo/real-world-photo/20181114 长滩岛/14.webp","themes":["humanist-photo"],"tags":["海景"]}],
  }
,
  {
    id: "20181229 上海 参观项目",
    channel: "real-world-photo",
    title: "上海 参观项目",
    year: "2018",
    startedOn: "2018-12-29",
    place: "上海",
    summary: "",
    body: "",
    consent: "granted",
    stageFolder: "real-world-photo/2018/20181229 上海 参观项目",
    themes: ["landscape-photo"],

    media: [{ kind: "image", label: "P1290445", src: "photo/real-world-photo/20181229 上海 参观项目/01.webp", themes: ["landscape-photo"], tags: ["建筑", "雕塑"] }, { kind: "image", label: "P1290459", src: "photo/real-world-photo/20181229 上海 参观项目/02.webp", themes: ["landscape-photo"], tags: ["建筑", "雕塑"] }, { kind: "image", label: "P1290567", src: "photo/real-world-photo/20181229 上海 参观项目/03.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290665", src: "photo/real-world-photo/20181229 上海 参观项目/04.webp", tags: ["建筑"] }, { kind: "image", label: "P1290666", src: "photo/real-world-photo/20181229 上海 参观项目/05.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290676", src: "photo/real-world-photo/20181229 上海 参观项目/06.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290686", src: "photo/real-world-photo/20181229 上海 参观项目/07.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290695", src: "photo/real-world-photo/20181229 上海 参观项目/08.webp", tags: ["街头"] }, { kind: "image", label: "P1290700", src: "photo/real-world-photo/20181229 上海 参观项目/09.webp", tags: ["街头"] }, { kind: "image", label: "P1290737", src: "photo/real-world-photo/20181229 上海 参观项目/10.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290742", src: "photo/real-world-photo/20181229 上海 参观项目/11.webp", themes: ["landscape-photo"], tags: ["建筑", "雕塑"] }, { kind: "image", label: "P1290776", src: "photo/real-world-photo/20181229 上海 参观项目/12.webp", themes: ["landscape-photo"], tags: ["建筑"] }, { kind: "image", label: "P1290821", src: "photo/real-world-photo/20181229 上海 参观项目/13.webp", tags: ["动物"] }, { kind: "image", label: "P1290841", src: "photo/real-world-photo/20181229 上海 参观项目/14.webp", tags: ["动物"] }, { kind: "image", label: "P1290864", src: "photo/real-world-photo/20181229 上海 参观项目/15.webp", themes: ["landscape-photo"], tags: ["建筑", "蓝调"] }, { kind: "image", label: "P1290881", src: "photo/real-world-photo/20181229 上海 参观项目/16.webp", themes: ["landscape-photo"], tags: ["建筑", "街头", "蓝调"] }, { kind: "image", label: "P1290885", src: "photo/real-world-photo/20181229 上海 参观项目/17.webp", themes: ["landscape-photo"], tags: ["街头", "蓝调"] }, { kind: "image", label: "P1290547", src: "photo/real-world-photo/20181229 上海 参观项目/18.webp", stars: 2 }],
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

/** 访客类型门：风光 / 人文 / 人像。不是投放夹。 */
export const photoWorkChannels = [
  lexicon.landscapePhoto.key,
  lexicon.humanistPhoto.key,
  lexicon.portraitPhoto.key,
] as const;

/** 摄影大栏目下的投放夹。详情与资源前缀为 `photo/{channel}/{项目夹名}/`。 */
export const photoDeliveryChannels = [
  lexicon.realWorldPhoto.key,
  lexicon.gamePhoto.key,
  lexicon.aiPhoto.key,
] as const;

export const photoPoolChannels = [...photoWorkChannels, ...photoDeliveryChannels] as const;

export type PhotoWorkChannel = (typeof photoWorkChannels)[number];

/**
 * 读取类型查询。`theme` 为主名，旧 `channel` 仅在没有 `theme` 时作别名。
 */
export function readPhotoThemes(params: URLSearchParams): string[] {
  const themes = params.getAll("theme");
  if (themes.length > 0) {
    return themes;
  }
  return params.getAll("channel");
}

/**
 * 是否为主题类型键：风光、人文、人像、游戏、AI。
 */
export function isPhotoTypeKey(value: string): boolean {
  return (photoTypeKeys as readonly string[]).includes(value);
}

/**
 * 是否为摄影内容池栏目（旧三栏或现实 / 游戏 / AI 摄影）。
 */
export function isPhotoPoolChannel(channel: string): boolean {
  return (photoPoolChannels as readonly string[]).includes(channel);
}

/**
 * 列出访客可见的摄影作品，默认按完整拍摄日期倒序。
 */
export function listPublishedPhotoWorks(): WorkRecord[] {
  const allowed = new Set<string>(photoPoolChannels);
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
    for (const media of work.media) {
      if (!media.src) {
        continue;
      }
      for (const tag of media.tags ?? []) {
        tags.add(tag);
      }
    }
  }
  return {
    years: [...years].sort(),
    places: [...places].sort((a, b) => a.localeCompare(b, "zh-CN")),
    tags: [...tags].sort((a, b) => a.localeCompare(b, "zh-CN")),
  };
}

/**
 * 一次外出的排序时刻：已上页照片里最早的拍摄时刻，没有则用开始日期的零点。
 */
function workPhotoTime(work: WorkRecord): string {
  let earliest = "";
  for (const media of listWorkImages(work)) {
    const taken = photoTakenAt(media.src);
    if (!taken) {
      continue;
    }
    if (!earliest || taken < earliest) {
      earliest = taken;
    }
  }
  return photoTimeKey(earliest, startedOnDate(work));
}

/**
 * 按拍摄时刻排序。主题总览用该次外出最早一张的时刻；没有时刻时退回开始日期。
 */
export function sortPhotoWorks(works: WorkRecord[], sort: "asc" | "desc"): WorkRecord[] {
  return [...works].sort((a, b) => {
    const dateDiff = comparePhotoTime(workPhotoTime(a), workPhotoTime(b), sort);
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
 * 把已发布摄影作品摊成静帧。类型只读这一张。
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
 * 按类型或自由标签筛已发布静帧。未传的维不限制。
 */
export function listMatchingPhotoFrames(partial: { themes?: string[]; tags?: string[] }): PhotoCatalogFrame[] {
  return filterPhotoFrames(listPublishedPhotoFrames(), {
    themes: partial.themes ?? [],
    years: [],
    places: [],
    tags: partial.tags ?? [],
    untagged: false,
    sort: "desc",
  });
}

/**
 * 层境首页摄影框的代表静帧。风光、人像按类型，展馆按自由标签。
 */
export function firstPhotoHomeSrc(label: string): string | undefined {
  if (label === lexicon.landscapePhoto.zh) {
    return listMatchingPhotoFrames({ themes: [lexicon.landscapePhoto.key] })[0]?.src;
  }
  if (label === photoExhibitTitle) {
    return listMatchingPhotoFrames({ tags: [photoExhibitTag] })[0]?.src;
  }
  if (label === lexicon.portraitPhoto.zh) {
    return listMatchingPhotoFrames({ themes: [lexicon.portraitPhoto.key] })[0]?.src;
  }
  return undefined;
}

/**
 * 按已入列键排序。星级只有从高到低。平局再按标题、id 与 media 原序。
 */
export function sortPhotoFrames(
  frames: PhotoCatalogFrame[],
  sort: "asc" | "desc" | readonly PhotoFrameSortRule[],
): PhotoCatalogFrame[] {
  const rules: PhotoFrameSortRule[] = typeof sort === "string" ? [{ key: "time", dir: sort }] : [...sort];
  const decorated = frames.map((frame) => ({
    frame,
    mediaIndex: frame.work.media.indexOf(frame.media),
  }));
  decorated.sort((a, b) => {
    const left = photoTimeKey(photoTakenAt(a.frame.src), startedOnDate(a.frame.work));
    const right = photoTimeKey(photoTakenAt(b.frame.src), startedOnDate(b.frame.work));
    const factDiff = comparePhotoFrameFacts(
      { stars: starsOf(a.frame.media), time: left },
      { stars: starsOf(b.frame.media), time: right },
      rules,
    );
    if (factDiff !== 0) {
      return factDiff;
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
