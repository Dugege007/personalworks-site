import { lexicon } from "../content/lexicon";
import { categories, gameProjects, playableGames, type WorkCollection } from "../content/site";
import { stockPlaceholderSrc } from "../content/stockMedia";
import {
  drawOfImage,
  listMatchingPhotoFrames,
  listPublishedWorks,
  type WorkRecord,
} from "../content/works";
import { contentDraw } from "../content/stars";
import { uniqueDraws, type DrawSrc } from "../lib/comboCycle";
import { channelsOfKind, kindOfChannel, kindTitle, queryWorks } from "./query";

export const developWorkKinds = [
  lexicon.twinAndSim.key,
  lexicon.gameDev.key,
  lexicon.landscapeArch.key,
  lexicon.photography.key,
] as const;

export type DevelopWorkKind = (typeof developWorkKinds)[number];

export type WorkIndexPath =
  | { layer: "root" }
  | { layer: "kind"; kind: string }
  | { layer: "channel"; kind: string; channel: string }
  | { layer: "detail"; kind: string; channel: string; id: string }
  | { layer: "legacy-id"; id: string };

export type DevelopKindDoor = {
  kind: DevelopWorkKind;
  zh: string;
  en: string;
  deco: string;
  lead: string;
  href: string;
  coverSrc?: string;
  srcs: DrawSrc[];
};

export type DevelopChannelDoor = {
  channel: string;
  zh: string;
  en: string;
  deco: string;
  lead: string;
  href: string;
  comingSoon?: boolean;
  coverSrc?: string;
  /** 本细目可轮换的静帧。不足两张时不换。 */
  srcs?: DrawSrc[];
  count: number;
};

const kindLexicon = {
  [lexicon.twinAndSim.key]: lexicon.twinAndSim,
  [lexicon.gameDev.key]: lexicon.gameDev,
  [lexicon.landscapeArch.key]: lexicon.landscapeArch,
  [lexicon.photography.key]: lexicon.photography,
} as const;

/**
 * 作品墙根路径。
 */
export function workIndexRoot(): string {
  return `/${lexicon.workIndex.key}`;
}

/**
 * 判断是否为显影作品树的四门键。
 */
export function isDevelopWorkKind(value: string | undefined): value is DevelopWorkKind {
  return Boolean(value && (developWorkKinds as readonly string[]).includes(value));
}

/**
 * 门类装饰短写。
 */
export function kindDeco(kind: string): string {
  return kindLexicon[kind as DevelopWorkKind]?.deco ?? kind;
}

/**
 * 门类正式英文。
 */
export function kindEn(kind: string): string {
  return kindLexicon[kind as DevelopWorkKind]?.en ?? kind;
}

/**
 * 显影门类页地址。
 */
export function hrefForDevelopKind(kind: string): string {
  return `${workIndexRoot()}/${kind}`;
}

/**
 * 显影细目列表页地址。
 */
export function hrefForDevelopChannel(kind: string, channel: string): string {
  return `${workIndexRoot()}/${kind}/${channel}`;
}

/**
 * 摄影总览地址；可选预选题材。
 */
export function hrefForPhotoCatalog(theme?: string): string {
  const path = `/${lexicon.photography.key}/${lexicon.photoCatalog.key}`;
  if (!theme) {
    return path;
  }
  return `${path}?theme=${encodeURIComponent(theme)}`;
}

/**
 * 摄影总览地址，预选一个自由标签。
 */
export function hrefForPhotoTag(tag: string): string {
  const path = `/${lexicon.photography.key}/${lexicon.photoCatalog.key}`;
  return `${path}?tag=${encodeURIComponent(tag)}`;
}

/**
 * 摄影门打开总览：有自由标签时按标签，否则按类型键。
 */
export function hrefForPhotoDoor(collection: WorkCollection): string {
  if (collection.catalogTag) {
    return hrefForPhotoTag(collection.catalogTag);
  }
  return hrefForPhotoCatalog(collection.id);
}

/**
 * 摄影门封面用的静帧。展馆按标签，其余门按类型。
 */
export function framesForPhotoDoor(collection: WorkCollection) {
  if (collection.comingSoon) {
    return [];
  }
  if (collection.catalogTag) {
    return listMatchingPhotoFrames({ tags: [collection.catalogTag] });
  }
  return listMatchingPhotoFrames({ themes: [collection.id] });
}

/**
 * 拍摄列表地址。
 */
export function hrefForPhotoShoots(): string {
  return `/${lexicon.photography.key}/${lexicon.photoShoots.key}`;
}

/**
 * 显影项目详情地址；带门类与细目以免 id 跨 channel 撞名。
 */
export function hrefForDevelopWork(work: WorkRecord): string {
  const kind = kindOfChannel(work.channel);
  if (kind) {
    return `${hrefForDevelopChannel(kind, work.channel)}/${work.id}`;
  }
  return `${workIndexRoot()}/${work.id}`;
}

/**
 * 解析 `/work-index` 下的层。
 */
export function parseWorkIndexPath(pathname: string): WorkIndexPath | undefined {
  const root = workIndexRoot();
  if (pathname === root) {
    return { layer: "root" };
  }
  if (!pathname.startsWith(`${root}/`)) {
    return undefined;
  }
  const rest = pathname.slice(root.length + 1).split("/").filter(Boolean);
  if (rest.length === 0) {
    return { layer: "root" };
  }
  if (rest.length === 1) {
    if (isDevelopWorkKind(rest[0])) {
      return { layer: "kind", kind: rest[0] };
    }
    return { layer: "legacy-id", id: rest[0] ?? "" };
  }
  if (isDevelopWorkKind(rest[0])) {
    if (rest.length === 2) {
      return { layer: "channel", kind: rest[0], channel: rest[1] ?? "" };
    }
    return {
      layer: "detail",
      kind: rest[0],
      channel: rest[1] ?? "",
      id: rest[2] ?? "",
    };
  }
  return undefined;
}

/**
 * 四门入口：画面取该门已发布作品的封面，缺图由版式井底承接。
 */
export function listDevelopKindDoors(): DevelopKindDoor[] {
  return developWorkKinds.map((kind) => {
    const entry = kindLexicon[kind];
    const category = categories.find((item) => item.id === kind);
    return {
      kind,
      zh: entry.zh,
      en: entry.en,
      deco: entry.deco,
      lead: category?.lead ?? "",
      href: hrefForDevelopKind(kind),
      coverSrc: coverOfKind(kind),
      srcs: shotsOfKind(kind),
    };
  });
}

/**
 * 某门下的细目入口。游戏项目在门类页浏览，选单只统计可玩项目。
 */
export function listDevelopChannelDoors(kind: string): DevelopChannelDoor[] {
  if (kind === lexicon.gameDev.key) {
    return [
      {
        channel: lexicon.gameMenu.key,
        zh: lexicon.gameMenu.zh,
        en: lexicon.gameMenu.en,
        deco: lexicon.gameMenu.deco,
        lead: "先选单，再加载。不登录、不付费。",
        href: hrefForDevelopChannel(kind, lexicon.gameMenu.key),
        coverSrc: playableGames[0]?.coverSrc ?? gameProjects[0]?.coverSrc ?? "game-dev/stock/01.webp",
        count: playableGames.length,
      },
    ];
  }
  const category = categories.find((item) => item.id === kind);
  if (!category) {
    return [];
  }
  return category.collections.map((collection) => {
    const photoDoor = kind === lexicon.photography.key;
    const frames = photoDoor && !collection.comingSoon ? framesForPhotoDoor(collection) : [];
    const works = photoDoor ? [] : listPublishedWorks(collection.id);
    return {
      channel: collection.id,
      zh: collection.title,
      en: collection.titleEn,
      deco: collection.titleDeco,
      lead: collection.lead,
      href: photoDoor && !collection.comingSoon
        ? hrefForPhotoDoor(collection)
        : hrefForDevelopChannel(kind, collection.id),
      comingSoon: collection.comingSoon,
      coverSrc: photoDoor
        ? (frames[0]?.src ?? `${collection.theme}/stock/01.webp`)
        : (firstCoverSrc(works) ?? `${collection.id}/stock/01.webp`),
      srcs: photoDoor
        ? uniqueDraws(frames.map((frame) => drawOfImage(frame.media)))
        : channelStillDraws(collection.id),
      count: photoDoor ? frames.length : works.length,
    };
  });
}

/**
 * 细目是否属于该门。游戏开发的细目键为选单。
 */
export function isChannelOfKind(kind: string, channel: string | undefined): boolean {
  if (!channel) {
    return false;
  }
  if (kind === lexicon.gameDev.key) {
    return channel === lexicon.gameMenu.key;
  }
  const category = categories.find((item) => item.id === kind);
  if (category?.collections.some((item) => item.id === channel)) {
    return true;
  }
  return channelsOfKind(kind).includes(channel);
}

/**
 * 某门全部可轮换画面。内容层静帧去掉 0 星和未写星级。
 * 游戏截图与其它静帧同一规则，不因仍是二元组而放行。
 */
export function shotsOfKind(kind: string): DrawSrc[] {
  if (kind === lexicon.gameDev.key) {
    const list: Array<DrawSrc | null> = [];
    for (const game of gameProjects) {
      const seen = new Set<string>();
      for (const shot of game.screenshots) {
        if (shot.kind === "video") {
          continue;
        }
        const draw = contentDraw(shot.src, shot);
        if (!draw || seen.has(draw.src)) {
          continue;
        }
        seen.add(draw.src);
        list.push(draw);
      }
    }
    return uniqueDraws(list);
  }
  const works = channelsOfKind(kind).flatMap((channel) => listPublishedWorks(channel));
  return doorStills(works);
}

/**
 * 某一细目的轮换静帧。0 星和未写星级不进池，张数不够也不补回。
 * 视频海报只在该条自身为 1～5 星时进入。不足两张时调用处不轮换。
 */
export function channelStillDraws(channel: string): DrawSrc[] {
  return doorStills(listPublishedWorks(channel));
}

function doorStills(works: WorkRecord[]): DrawSrc[] {
  const rated: Array<DrawSrc | null> = [];
  for (const work of works) {
    for (const media of work.media) {
      if (media.kind === "video") {
        rated.push(contentDraw(media.poster, media));
        continue;
      }
      rated.push(drawOfImage(media));
    }
  }
  return uniqueDraws(rated);
}

function coverOfKind(kind: string): string | undefined {
  if (kind === lexicon.gameDev.key) {
    return gameProjects[0]?.coverSrc ?? stockPlaceholderSrc(lexicon.gameDev.key, 1);
  }
  const featured = queryWorks({ source: "works", kind, featured: true, limit: 1 });
  const rest = queryWorks({ source: "works", kind, limit: 8 });
  return firstCoverSrc(featured) ?? firstCoverSrc(rest);
}

function firstCoverSrc(works: WorkRecord[]): string | undefined {
  for (const work of works) {
    const src = work.media.find((item) => item.src)?.src;
    if (src) {
      return src;
    }
  }
  return undefined;
}

export { kindTitle };
