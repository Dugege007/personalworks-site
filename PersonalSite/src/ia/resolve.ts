import { lexicon } from "../content/lexicon";
import { findCollectionByChannel } from "../content/site";
import {
  findPublishedWork,
  findPublishedWorksById,
  hrefForPhotoWork,
  photoDeliveryChannels,
  photoWorkChannels,
} from "../content/works";
import { strataKindPathDict } from "./strata";
import type { IaId } from "./types";
import { splitHref } from "./photoNav";
import {
  hrefForDevelopChannel,
  hrefForDevelopKind,
  hrefForPhotoCatalog,
  parseWorkIndexPath,
  workIndexRoot,
} from "./workTree";

export type ResolvedPath = {
  pathname: string;
  search: string;
};

const photoTypeSet = new Set<string>(photoWorkChannels);
const photoDeliverySet = new Set<string>(photoDeliveryChannels);

/**
 * 切肤时按目标 IA 映射路径；无对等则回该 IA 首页。调用方须 `replace`。
 */
export function resolvePathForSkin(pathname: string, search: string, targetIa: IaId): ResolvedPath {
  if (targetIa === "develop-editorial") {
    return mapToDevelop(pathname, search);
  }
  if (targetIa === "strata-archive") {
    return mapToStrata(pathname, search);
  }
  return { pathname: "/", search: "" };
}

function mapToDevelop(pathname: string, search: string): ResolvedPath {
  const stay = { pathname, search };
  if (pathname === "/" || pathname === `/${lexicon.profileResume.key}` || pathname === `/${lexicon.profileSkills.key}`) {
    return stay;
  }
  const parsed = parseWorkIndexPath(pathname);
  if (parsed?.layer === "channel" && parsed.kind === lexicon.photography.key) {
    const catalog = mapPhotoChannelToCatalog(parsed.channel);
    if (catalog) {
      return catalog;
    }
  }
  if (pathname === workIndexRoot() || pathname.startsWith(`${workIndexRoot()}/`)) {
    return stay;
  }
  if (pathname === `/${lexicon.twinAndSim.key}`) {
    return { pathname: hrefForDevelopKind(lexicon.twinAndSim.key), search: "" };
  }
  if (pathname === `/${lexicon.gameDev.key}`) {
    return { pathname: hrefForDevelopKind(lexicon.gameDev.key), search: "" };
  }
  if (pathname === `/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`) {
    return { pathname: hrefForDevelopChannel(lexicon.gameDev.key, lexicon.gameMenu.key), search: "" };
  }
  if (pathname === `/${lexicon.landscapeArch.key}`) {
    return { pathname: hrefForDevelopKind(lexicon.landscapeArch.key), search: "" };
  }
  if (pathname === `/${lexicon.photography.key}`) {
    return { pathname: hrefForDevelopKind(lexicon.photography.key), search: "" };
  }
  if (isSharedPhotoPath(pathname) || isPhotoDetail(pathname)) {
    return stay;
  }
  if (isGamePlay(pathname)) {
    return stay;
  }
  const nested = mapStrataDetailToDevelop(pathname);
  if (nested) {
    return nested;
  }
  if (pathname === `/${lexicon.notes.key}` || pathname.startsWith(`/${lexicon.notes.key}/`)) {
    return stay;
  }
  if (pathname === `/${lexicon.about.key}`) {
    return stay;
  }
  return { pathname: "/", search: "" };
}

function mapToStrata(pathname: string, search: string): ResolvedPath {
  const stay = { pathname, search };
  if (pathname === "/" || pathname === `/${lexicon.profileResume.key}` || pathname === `/${lexicon.profileSkills.key}`) {
    return stay;
  }
  const parsed = parseWorkIndexPath(pathname);
  if (parsed) {
    return mapWorkIndexToStrata(parsed, search);
  }
  if (isSharedPhotoPath(pathname) || isPhotoDetail(pathname)) {
    return stay;
  }
  if (isGamePlay(pathname)) {
    return stay;
  }
  if (
    pathname === `/${lexicon.twinAndSim.key}` ||
    pathname.startsWith(`/${lexicon.twinAndSim.key}/`) ||
    pathname === `/${lexicon.gameDev.key}` ||
    pathname.startsWith(`/${lexicon.gameDev.key}/`) ||
    pathname === `/${lexicon.landscapeArch.key}` ||
    pathname.startsWith(`/${lexicon.landscapeArch.key}/`) ||
    pathname === `/${lexicon.photography.key}` ||
    pathname === `/${lexicon.notes.key}` ||
    pathname.startsWith(`/${lexicon.notes.key}/`) ||
    pathname === `/${lexicon.about.key}`
  ) {
    return stay;
  }
  return { pathname: "/", search: "" };
}

function mapWorkIndexToStrata(
  parsed: NonNullable<ReturnType<typeof parseWorkIndexPath>>,
  search: string,
): ResolvedPath {
  if (parsed.layer === "root") {
    const kind = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search).get("kind");
    if (kind && strataKindPathDict[kind]) {
      return { pathname: strataKindPathDict[kind], search: "" };
    }
    return { pathname: "/", search: "" };
  }
  if (parsed.layer === "kind" && strataKindPathDict[parsed.kind]) {
    return { pathname: strataKindPathDict[parsed.kind], search: "" };
  }
  if (parsed.layer === "channel") {
    if (parsed.kind === lexicon.gameDev.key && parsed.channel === lexicon.gameMenu.key) {
      return { pathname: `/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`, search: "" };
    }
    if (parsed.kind === lexicon.photography.key) {
      return mapPhotoChannelToCatalog(parsed.channel) ?? { pathname: `/${lexicon.photography.key}`, search: "" };
    }
    if (strataKindPathDict[parsed.kind]) {
      return { pathname: strataKindPathDict[parsed.kind], search: "" };
    }
  }
  if (parsed.layer === "detail") {
    const collection = findCollectionByChannel(parsed.channel);
    if (collection) {
      return { pathname: `${collection.detailBase}/${encodeURIComponent(parsed.id)}`, search: "" };
    }
    if (photoDeliverySet.has(parsed.channel)) {
      const work = findPublishedWork(parsed.channel, parsed.id);
      if (work) {
        return splitHref(hrefForPhotoWork(work));
      }
    }
  }
  if (parsed.layer === "legacy-id") {
    const work = findPublishedWorksById(parsed.id)[0];
    const collection = work ? findCollectionByChannel(work.channel) : undefined;
    if (work && collection) {
      return { pathname: `${collection.detailBase}/${work.id}`, search: "" };
    }
  }
  return { pathname: "/", search: "" };
}

function mapStrataDetailToDevelop(pathname: string): ResolvedPath | undefined {
  const parts = pathname.split("/").filter(Boolean);
  if (parts.length !== 3) {
    return undefined;
  }
  if (parts[0] === lexicon.twinAndSim.key || parts[0] === lexicon.landscapeArch.key) {
    return {
      pathname: `${workIndexRoot()}/${parts[0]}/${parts[1]}/${parts[2]}`,
      search: "",
    };
  }
  return undefined;
}

function mapPhotoChannelToCatalog(channel: string): ResolvedPath | undefined {
  const collection = findCollectionByChannel(channel);
  if (!collection || collection.comingSoon) {
    return undefined;
  }
  if (!photoTypeSet.has(channel)) {
    return undefined;
  }
  return splitHref(hrefForPhotoCatalog(channel));
}

function isSharedPhotoPath(pathname: string): boolean {
  const root = `/${lexicon.photography.key}`;
  return pathname === `${root}/${lexicon.photoCatalog.key}` || pathname === `${root}/${lexicon.photoShoots.key}`;
}

function isPhotoDetail(pathname: string): boolean {
  const parts = pathname.split("/").filter(Boolean);
  return (
    parts.length >= 3 &&
    parts[0] === lexicon.photography.key &&
    (photoTypeSet.has(parts[1] ?? "") || photoDeliverySet.has(parts[1] ?? ""))
  );
}

function isGamePlay(pathname: string): boolean {
  const root = `/${lexicon.gameDev.key}`;
  const menu = `${root}/${lexicon.gameMenu.key}`;
  return pathname.startsWith(`${root}/`) && pathname !== menu;
}
