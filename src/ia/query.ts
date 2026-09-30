import { lexicon } from "../content/lexicon";
import { placeholderNotes, type NoteCard } from "../content/site";
import { listAllPublishedWorks, photoPoolChannels, type WorkRecord } from "../content/works";
import type { ContentQuery } from "./types";

const kindChannelDict: Record<string, string[]> = {
  [lexicon.twinAndSim.key]: [lexicon.digitalTwin.key, lexicon.lineSimulation.key],
  [lexicon.landscapeArch.key]: [lexicon.landscapeRendering.key, lexicon.landscapeCDs.key],
  [lexicon.photography.key]: [...photoPoolChannels],
  [lexicon.gameDev.key]: [],
};

/**
 * 门类键到作品 channel 列表；全站共用，不按皮肤复制。
 */
export function channelsOfKind(kind: string): string[] {
  return kindChannelDict[kind] ?? [];
}

/**
 * 由作品 channel 反推门类键。
 */
export function kindOfChannel(channel: string): string | undefined {
  for (const [kind, channels] of Object.entries(kindChannelDict)) {
    if (channels.includes(channel)) {
      return kind;
    }
  }
  return undefined;
}

/**
 * 门类中文名，供作品墙与切片使用。
 */
export function kindTitle(kind: string): string {
  if (kind === lexicon.twinAndSim.key) {
    return lexicon.twinAndSim.zh;
  }
  if (kind === lexicon.landscapeArch.key) {
    return lexicon.landscapeArch.zh;
  }
  if (kind === lexicon.photography.key) {
    return lexicon.photography.zh;
  }
  if (kind === lexicon.gameDev.key) {
    return lexicon.gameDev.zh;
  }
  return kind;
}

/**
 * 按查询从内容池取作品；钉选 id 格式为 `channel/id`。
 */
export function queryWorks(query: ContentQuery): WorkRecord[] {
  let list = listAllPublishedWorks();
  const explicitChannels = typeof query.channel === "string" ? [query.channel] : query.channel ?? [];
  const kindChannels = query.kind ? channelsOfKind(query.kind) : [];
  if (query.kind && kindChannels.length === 0 && explicitChannels.length === 0) {
    return [];
  }
  const channels = [...explicitChannels, ...kindChannels];
  if (channels.length > 0) {
    const allowed = new Set(channels);
    list = list.filter((item) => allowed.has(item.channel));
  }
  if (query.featured) {
    list = list.filter((item) => item.featured);
  }
  if (query.ids && query.ids.length > 0) {
    const rank = new Map(query.ids.map((key, index) => [key, index]));
    list = list
      .filter((item) => rank.has(`${item.channel}/${item.id}`))
      .sort((a, b) => (rank.get(`${a.channel}/${a.id}`) ?? 0) - (rank.get(`${b.channel}/${b.id}`) ?? 0));
  }
  if (query.limit !== undefined) {
    list = list.slice(0, query.limit);
  }
  return list;
}

/**
 * 按日期新到旧取心得。
 */
export function queryNotes(query: ContentQuery): NoteCard[] {
  const list = placeholderNotes.slice().sort((a, b) => b.date.localeCompare(a.date));
  if (query.limit !== undefined) {
    return list.slice(0, query.limit);
  }
  return list;
}
