import { lexicon } from "../content/lexicon";
import { findCollectionByChannel } from "../content/site";
import { photoWorkChannels, type WorkRecord } from "../content/works";
import { strataKindPathDict } from "./strata";
import type { IaId } from "./types";
import { hrefForDevelopKind, hrefForDevelopWork, hrefForPhotoCatalog, hrefForPhotoShoots } from "./workTree";

export { hrefForPhotoCatalog, hrefForPhotoShoots };

/**
 * 作品在当前 IA 下的详情地址。摄影始终走既有单帧路径。
 */
export function hrefForWork(work: WorkRecord, iaId: IaId): string {
  if ((photoWorkChannels as readonly string[]).includes(work.channel)) {
    return `/${lexicon.photography.key}/${work.channel}/${work.id}`;
  }
  if (iaId === "develop-editorial") {
    return hrefForDevelopWork(work);
  }
  const collection = findCollectionByChannel(work.channel);
  if (collection) {
    return `${collection.detailBase}/${work.id}`;
  }
  return `/${lexicon.workIndex.key}`;
}

/**
 * 门类在当前 IA 下的入口：显影走作品墙，层境走分类室。
 */
export function hrefForKind(kind: string, iaId: IaId): string {
  if (iaId === "develop-editorial") {
    return hrefForDevelopKind(kind);
  }
  return strataKindPathDict[kind] ?? `/${kind}`;
}

/**
 * 作品类型中文名。
 */
export function channelTitleZh(channel: string): string {
  const table: Record<string, string> = {
    [lexicon.digitalTwin.key]: lexicon.digitalTwin.zh,
    [lexicon.lineSimulation.key]: lexicon.lineSimulation.zh,
    [lexicon.landscapeRendering.key]: lexicon.landscapeRendering.zh,
    [lexicon.landscapeCDs.key]: lexicon.landscapeCDs.zh,
    [lexicon.landscapePhoto.key]: lexicon.landscapePhoto.zh,
    [lexicon.humanistPhoto.key]: lexicon.humanistPhoto.zh,
    [lexicon.portraitPhoto.key]: lexicon.portraitPhoto.zh,
    [lexicon.gamePhoto.key]: lexicon.gamePhoto.zh,
    [lexicon.aiPhoto.key]: lexicon.aiPhoto.zh,
  };
  return table[channel] ?? channel;
}
