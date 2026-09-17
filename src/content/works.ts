import { initialWorkProjects } from "./initialWorkProjects";
import { lexicon } from "./lexicon";
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
function listed(entries: readonly (readonly [src: string, label: string])[]): WorkMedia[] {
  return entries.map(([src, label]) => ({ kind: "image" as const, src, label }));
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

export type HomeHeroFrame = {
  src: string;
  year: string;
  width?: number;
  height?: number;
};

/**
 * 显影头图图池：已发布风光摄影的全部画面，按有效日期倒序。
 */
export function listLandscapeHeroFrames(): HomeHeroFrame[] {
  const frames: HomeHeroFrame[] = [];
  const seen = new Set<string>();
  for (const work of listPublishedWorks(lexicon.landscapePhoto.key)) {
    for (const media of listWorkImages(work)) {
      if (!media.src || seen.has(media.src)) {
        continue;
      }
      seen.add(media.src);
      const size = photoSizes[media.src];
      frames.push({
        src: media.src,
        year: work.year,
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

/** 维护工具确认登记的空壳；访客列表仍按 consent 过滤。 */
const registeredWorks: WorkRecord[] = [];

/**
 * 全站唯一作品内容池。景观施工图与无授权人像不进入该数组。
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
