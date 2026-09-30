import { lexicon } from "./lexicon.ts";

/** 照片上的主题类型，顺序固定。游戏摄影与 AI摄影只进筛选，不另开投放夹上的类型门。 */
export const photoTypeKeys = [
  lexicon.landscapePhoto.key,
  lexicon.humanistPhoto.key,
  lexicon.portraitPhoto.key,
  lexicon.gamePhoto.key,
  lexicon.aiPhoto.key,
] as const;

/** 摄影默认自由标签。展馆在风光与人文之间，随拍在人像后，不升为类型门。 */
export const photoDefaultTagLabels = ["展馆", "随拍"] as const;

/** 类型第一行的顺序：风光、展馆、人文、人像、随拍、游戏、AI。 */
export const photoPrimaryFacetOrder = [
  lexicon.landscapePhoto.key,
  photoDefaultTagLabels[0],
  lexicon.humanistPhoto.key,
  lexicon.portraitPhoto.key,
  photoDefaultTagLabels[1],
  lexicon.gamePhoto.key,
  lexicon.aiPhoto.key,
] as const;

/** 没有类型、也没有自由标签时使用的筛选键。 */
export const photoUntaggedKey = "untagged";

/**
 * 筛选条上的类型短名。去掉末尾「摄影」，栏目正式名仍用词表全称。
 */
export function photoTypeChipLabel(zh: string): string {
  return zh.endsWith("摄影") ? zh.slice(0, -2) : zh;
}

/**
 * 是否为插在类型第一行的默认自由标签。
 */
export function isPhotoDefaultTag(value: string): boolean {
  return (photoDefaultTagLabels as readonly string[]).includes(value);
}

export type PhotoCatalogQuery = {
  themes: string[];
  years: string[];
  places: string[];
  tags: string[];
  untagged: boolean;
  sort: "asc" | "desc";
};

type PhotoFacetMedia = {
  src?: string;
  themes?: readonly string[];
  tags?: readonly string[];
};

type PhotoFacetWork = {
  year: string;
  place?: string;
  tags?: readonly string[];
  themes?: readonly string[];
  media: readonly PhotoFacetMedia[];
};

type PhotoFacetFrame = {
  work: PhotoFacetWork;
  media: PhotoFacetMedia;
};

/**
 * 项目类型。只取已上页照片的类型，按风光、人文、人像、游戏、AI 排列。
 */
export function themesOfPhotoWork(work: PhotoFacetWork): string[] {
  const picked = new Set(
    work.media.filter((item) => item.src).flatMap((item) => item.themes ?? []),
  );
  return photoTypeKeys.filter((key) => picked.has(key));
}

/**
 * 单张类型。只认这一张上的类型，可零个或多个，按风光、人文、人像、游戏、AI 排列。
 */
export function themesOfPhotoFrame(_work: PhotoFacetWork, media: PhotoFacetMedia): string[] {
  return photoTypeKeys.filter((key) => (media.themes ?? []).includes(key));
}

function orderedThemes(source: readonly string[]): string[] {
  return photoTypeKeys.filter((key) => source.includes(key));
}

function uniqueTags(source: readonly string[]): string[] {
  const picked: string[] = [];
  for (const tag of source) {
    const text = tag.trim();
    if (!text || picked.includes(text) || (photoTypeKeys as readonly string[]).includes(text)) {
      continue;
    }
    picked.push(text);
  }
  return picked;
}

/**
 * 这一张的自由标签。作品级词也算在这张上，便于总览跟工具里写下的词对齐。
 */
function tagsForFrame(work: PhotoFacetWork, media: PhotoFacetMedia): string[] {
  return uniqueTags([...(media.tags ?? []), ...(work.tags ?? [])]);
}

/**
 * 这一次外出的自由标签：作品级词，加上各张已写明的词。
 */
function tagsForWork(work: PhotoFacetWork): string[] {
  const picked = [...(work.tags ?? [])];
  for (const media of work.media) {
    if (!media.src) {
      continue;
    }
    picked.push(...(media.tags ?? []));
  }
  return uniqueTags(picked);
}

function isFrameUntagged(work: PhotoFacetWork, media: PhotoFacetMedia): boolean {
  return themesForFrame(work, media).length === 0 && tagsForFrame(work, media).length === 0;
}

function isWorkUntagged(work: PhotoFacetWork): boolean {
  return work.media.some((media) => media.src && isFrameUntagged(work, media));
}

/**
 * 单张写了类型就只认这一张。项目里还没有任何一张写类型时，没写的才沿用项目类型。
 * 已经有张写了类型之后，其余没写的不再沿用，避免项目汇总类型把同组照片一起带进筛选。
 */
function themesForFrame(work: PhotoFacetWork, media: PhotoFacetMedia): string[] {
  if (media.themes && media.themes.length > 0) {
    return orderedThemes(media.themes);
  }
  const projectAlreadyTyped = work.media.some(
    (item) => Boolean(item.src) && (item.themes?.length ?? 0) > 0,
  );
  if (projectAlreadyTyped) {
    return [];
  }
  return orderedThemes(work.themes ?? []);
}

/**
 * 该次外出的类型：拍摄上的类型，加上各张已写明的类型。
 */
function themesForWork(work: PhotoFacetWork): string[] {
  const picked = new Set<string>(work.themes ?? []);
  for (const media of work.media) {
    if (!media.src) {
      continue;
    }
    for (const theme of media.themes ?? []) {
      picked.add(theme);
    }
  }
  return orderedThemes([...picked]);
}

function matchesEvery(actual: readonly string[], selected: readonly string[]): boolean {
  if (selected.length === 0) {
    return true;
  }
  return selected.every((value) => actual.includes(value));
}

function matchesOnlyValue(actual: string | undefined, selected: readonly string[]): boolean {
  if (selected.length === 0) {
    return true;
  }
  if (!actual) {
    return false;
  }
  return selected.every((value) => value === actual);
}

/**
 * 按已选标签取交集过滤摄影作品。同一维内多个取值也须同时命中。未选某维不限制。
 */
export function filterPhotoWorks<T extends PhotoFacetWork>(
  works: readonly T[],
  query: PhotoCatalogQuery,
): T[] {
  return works.filter((work) => {
    if (!matchesEvery(themesForWork(work), query.themes)) {
      return false;
    }
    if (!matchesOnlyValue(work.year, query.years)) {
      return false;
    }
    if (!matchesOnlyValue(work.place, query.places)) {
      return false;
    }
    if (!matchesEvery(tagsForWork(work), query.tags)) {
      return false;
    }
    if (query.untagged && !isWorkUntagged(work)) {
      return false;
    }
    return true;
  });
}

/**
 * 按已选标签取交集过滤摊帧。类型读这一张，须同时含有全部所选类型。
 */
export function filterPhotoFrames<T extends PhotoFacetFrame>(
  frames: readonly T[],
  query: PhotoCatalogQuery,
): T[] {
  return frames.filter((frame) => {
    const work = frame.work;
    if (!matchesEvery(themesForFrame(work, frame.media), query.themes)) {
      return false;
    }
    if (!matchesOnlyValue(work.year, query.years)) {
      return false;
    }
    if (!matchesOnlyValue(work.place, query.places)) {
      return false;
    }
    if (!matchesEvery(tagsForFrame(work, frame.media), query.tags)) {
      return false;
    }
    if (query.untagged && !isFrameUntagged(work, frame.media)) {
      return false;
    }
    return true;
  });
}

export type PhotoFacetOccupancy = {
  themes: ReadonlySet<string>;
  years: ReadonlySet<string>;
  places: ReadonlySet<string>;
  tags: ReadonlySet<string>;
  untagged: boolean;
};

/**
 * 统计这批拍摄里实际出现过的类型、年份、地点与标签。
 */
export function occupiedPhotoFacetsFromWorks(works: readonly PhotoFacetWork[]): PhotoFacetOccupancy {
  const themes = new Set<string>();
  const years = new Set<string>();
  const places = new Set<string>();
  const tags = new Set<string>();
  let untagged = false;
  for (const work of works) {
    for (const theme of themesForWork(work)) {
      themes.add(theme);
    }
    years.add(work.year);
    if (work.place) {
      places.add(work.place);
    }
    for (const tag of tagsForWork(work)) {
      tags.add(tag);
    }
    if (isWorkUntagged(work)) {
      untagged = true;
    }
  }
  return { themes, years, places, tags, untagged };
}

/**
 * 统计这批静帧里实际出现过的类型、年份、地点与标签。类型按 themesForFrame。
 */
export function occupiedPhotoFacetsFromFrames(frames: readonly PhotoFacetFrame[]): PhotoFacetOccupancy {
  const themes = new Set<string>();
  const years = new Set<string>();
  const places = new Set<string>();
  const tags = new Set<string>();
  let untagged = false;
  for (const frame of frames) {
    for (const theme of themesForFrame(frame.work, frame.media)) {
      themes.add(theme);
    }
    years.add(frame.work.year);
    if (frame.work.place) {
      places.add(frame.work.place);
    }
    for (const tag of tagsForFrame(frame.work, frame.media)) {
      tags.add(tag);
    }
    if (isFrameUntagged(frame.work, frame.media)) {
      untagged = true;
    }
  }
  return { themes, years, places, tags, untagged };
}

/**
 * 类型第一行。有未打标签的照片时，末尾加上「无标签」。
 */
export function primaryFacetValues(showUntagged: boolean): string[] {
  return showUntagged ? [...photoPrimaryFacetOrder, photoUntaggedKey] : [...photoPrimaryFacetOrder];
}

/**
 * 类型第一行里当前点中的取值。
 */
export function selectedPrimaryFacets(query: PhotoCatalogQuery): string[] {
  return [
    ...query.themes,
    ...query.tags.filter((tag) => isPhotoDefaultTag(tag)),
    ...(query.untagged ? [photoUntaggedKey] : []),
  ];
}

/**
 * 类型第一行里当前结果对不上、且未点中的取值。
 */
export function disabledPrimaryFacets(
  values: readonly string[],
  occupied: PhotoFacetOccupancy,
  query: PhotoCatalogQuery,
): string[] {
  const selected = new Set(selectedPrimaryFacets(query));
  return values.filter((value) => {
    if (selected.has(value)) {
      return false;
    }
    if (value === photoUntaggedKey) {
      return !occupied.untagged;
    }
    if (isPhotoDefaultTag(value)) {
      return !occupied.tags.has(value);
    }
    return !occupied.themes.has(value);
  });
}

/**
 * 自定义标签。默认的展馆、随拍留在类型第一行。
 */
export function customPhotoTags(tags: readonly string[]): string[] {
  return tags.filter((tag) => !isPhotoDefaultTag(tag));
}

/**
 * 筛选前，每个自由标签对应的已上页照片数。同一张上重复出现的词只计一次。
 */
export function photoTagResourceCounts(works: readonly PhotoFacetWork[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const work of works) {
    for (const media of work.media) {
      if (!media.src) {
        continue;
      }
      for (const tag of tagsForFrame(work, media)) {
        counts.set(tag, (counts.get(tag) ?? 0) + 1);
      }
    }
  }
  return counts;
}

/**
 * 类型第二行：按筛选前的照片数量从多到少，数量相同再按中文排序。
 */
export function orderPhotoCustomTags(
  tags: readonly string[],
  counts: ReadonlyMap<string, number>,
): string[] {
  return customPhotoTags(tags).sort((a, b) => {
    const diff = (counts.get(b) ?? 0) - (counts.get(a) ?? 0);
    return diff !== 0 ? diff : a.localeCompare(b, "zh-CN");
  });
}

/**
 * 未选中、且当前结果里没有作品的取值。已选中的留下，便于取消。
 */
export function disabledPhotoFacetValues(
  values: readonly string[],
  occupied: ReadonlySet<string>,
  selected: readonly string[],
): string[] {
  return values.filter((value) => !selected.includes(value) && !occupied.has(value));
}
