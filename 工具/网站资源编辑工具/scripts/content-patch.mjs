/**
 * 内容层补丁：按 intent.json 去掉 src、album 改为 listed、重排显示标签；含 video 的数组保留视频。
 * 不重命名对象键。失败还原目标文件，不删除意图文件。
 *
 *   node content-patch.mjs --intent <file> --profile <profile.json> [--dry-run|--apply]
 *   node content-patch.mjs --tidy-satellites --profile <profile.json> --keys <keys.json> [--dry-run|--apply]
 */

import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { copyFile, mkdtemp, readFile, readdir, rename, rm, unlink, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const PHOTO_EXIF_CHANNELS = new Set([
  "landscape-photo",
  "humanist-photo",
  "portrait-photo",
  "real-world-photo",
  "game-photo",
  "ai-photo",
]);

/** @type {Map<string, string>} */
let ingestVideoLabelMap = new Map();
/** @type {Map<string, string>} */
let photoStemMap = new Map();
/** @type {Map<string, string>} */
let sortKeyMap = new Map();

function parseArgs(argv) {
  const flags = {
    intent: "",
    profile: "",
    dryRun: false,
    apply: false,
    failAfterWrite: false,
    tidySatellites: false,
    keys: "",
    help: false,
  };
  const tokens = argv.slice(2);
  for (let i = 0; i < tokens.length; i += 1) {
    const token = tokens[i];
    if (token === "--help" || token === "-h") flags.help = true;
    else if (token === "--dry-run") flags.dryRun = true;
    else if (token === "--apply") flags.apply = true;
    else if (token === "--fail-after-write") flags.failAfterWrite = true;
    else if (token === "--tidy-satellites") flags.tidySatellites = true;
    else if (token === "--intent" || token === "--profile" || token === "--keys") {
      const value = tokens[i + 1];
      if (!value || value.startsWith("--")) {
        throw new Error(`${token} 后需要路径`);
      }
      if (token === "--intent") flags.intent = value;
      else if (token === "--keys") flags.keys = value;
      else flags.profile = value;
      i += 1;
    } else if (token.startsWith("--")) {
      throw new Error(`未知参数：${token}`);
    }
  }
  return flags;
}

function stripBom(text) {
  return text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
}

function toRel(value) {
  return String(value || "")
    .replace(/\\/g, "/")
    .replace(/^\/+|\/+$/g, "");
}

function resolveUnderRoot(root, relative) {
  return path.resolve(root, String(relative || "").replace(/[/\\]+/g, path.sep));
}

function inferLabelPrefix(mediaList) {
  for (const item of mediaList) {
    if (isVideo(item)) continue;
    const label = String(item.label || "").trim();
    const match = /^(.*?)(?:\s+)(\d+)$/.exec(label);
    const prefix = match ? match[1].trim() : label;
    if (prefix) return prefix;
  }
  return "效果图";
}

function relabel(mediaList) {
  const prefix = inferLabelPrefix(mediaList.filter((item) => !isVideo(item)));
  let imageIndex = 0;
  return mediaList.map((item) => {
    if (isVideo(item)) return item;
    imageIndex += 1;
    const next = {
      kind: "image",
      label: `${prefix} ${String(imageIndex).padStart(2, "0")}`,
      src: item.src,
    };
    if (Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5) next.stars = item.stars;
    return next;
  });
}

const EFFECT_LABEL = /^效果图\s+\d+$/;

function originalMediaStem(sourceStageRel, stageRel) {
  for (const rel of [sourceStageRel, stageRel]) {
    if (!rel || isPreparedRel(rel)) continue;
    const name = fileStem(rel);
    if (name) return name;
  }
  return "";
}

function collectPhotoStemMap(intent, ledgerDict) {
  const map = new Map();
  for (const [objectKey, record] of Object.entries(ledgerDict || {})) {
    const stem = originalMediaStem(record?.sourceStageRel, record?.stageRel);
    if (stem) map.set(toRel(objectKey), stem);
  }
  for (const item of intent?.items || []) {
    const objectKey = toRel(item.object);
    const stem = originalMediaStem(item.sourceStageRel, item.stageRel);
    if (objectKey && stem) map.set(objectKey, stem);
  }
  return map;
}

function relabelPhoto(mediaList, stemMap) {
  return mediaList.map((item) => {
    if (isVideo(item)) return item;
    const current = String(item.label || "").trim();
    if (current && !EFFECT_LABEL.test(current)) return item;
    const stem = stemMap.get(toRel(item.src)) || "";
    if (!stem) return item;
    return { ...item, label: stem };
  });
}

function hasMediaExtra(item) {
  return Boolean(
    (item.themes && item.themes.length) ||
    (item.tags && item.tags.length) ||
    item.displayName ||
    item.description ||
    (Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5),
  );
}

function formatRichMedia(mediaList) {
  const partList = mediaList.map((item) => formatTaggedMedia(
    item.src,
    item.label,
    item.themes || [],
    item.tags || [],
    {
      kind: item.kind,
      poster: item.poster,
      displayName: item.displayName,
      description: item.description,
      stars: item.stars,
    },
  ));
  return `media: [${partList.join(", ")}]`;
}

function formatListed(mediaList) {
  const lines = mediaList.map((item, index) => {
    const comma = index + 1 < mediaList.length ? "," : "";
    return `      ["${item.src}", "${item.label}"]${comma}`;
  });
  return `listed([\n${lines.join("\n")}\n    ])`;
}

function formatJsonMedia(mediaList) {
  const lines = mediaList.map((item, index) => {
    const comma = index + 1 < mediaList.length ? "," : "";
    const poster = item.poster ? `, "poster": "${item.poster}"` : "";
    const stars = Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5
      ? `, "stars": ${item.stars}`
      : "";
    return `  { "kind": "${item.kind || "image"}", "label": "${item.label}", "src": "${item.src}"${poster}${stars} }${comma}`;
  });
  return `[\n${lines.join("\n")}\n]`;
}

function workMapKey(channel, workId) {
  return channel ? `${channel}::${workId}` : workId;
}

/** @type {Map<string, string[]>} */
let reorderMap = new Map();

function collectReorderMap(intent) {
  reorderMap = new Map();
  for (const item of intent.items || []) {
    if (item.intent !== "media.reorder") continue;
    const objectList = (item.objectList || []).map((key) => toRel(key)).filter(Boolean);
    if (!item.workId || !item.channel || objectList.length === 0) continue;
    reorderMap.set(workMapKey(item.channel, item.workId), objectList);
  }
}

function orderListOf(channel, workId) {
  return reorderMap.get(workMapKey(channel, workId)) || reorderMap.get(workId) || null;
}

function collectChannelOrderLists(channel) {
  const prefix = `${channel}::`;
  const list = [];
  for (const [key, objectList] of reorderMap) {
    if (key === channel || key.startsWith(prefix)) list.push(objectList);
  }
  return list;
}

function srcKeyList(mediaList) {
  return (mediaList || [])
    .filter((item) => item.src && !isPosterObject(item.src))
    .map((item) => toRel(item.src));
}

function sameSrcOrder(leftList, rightList) {
  if (!leftList || !rightList || leftList.length !== rightList.length) {
    return !leftList && !rightList;
  }
  return leftList.every((value, index) => toRel(value) === toRel(rightList[index]));
}

function applyMediaOrder(mediaList, orderList) {
  if (!orderList || orderList.length === 0) return mediaList;
  const remaining = [...mediaList];
  const result = [];
  for (const raw of orderList) {
    const key = toRel(raw);
    const index = remaining.findIndex((item) => toRel(item.src) === key);
    if (index < 0) continue;
    result.push(remaining[index]);
    remaining.splice(index, 1);
  }
  return result.concat(remaining);
}

function applyObjectKeyOrder(objectList, orderList) {
  if (!orderList || orderList.length === 0) return objectList;
  const remaining = [...objectList];
  const result = [];
  for (const raw of orderList) {
    const key = toRel(raw);
    const index = remaining.findIndex((item) => toRel(item) === key);
    if (index < 0) continue;
    result.push(remaining[index]);
    remaining.splice(index, 1);
  }
  return result.concat(remaining);
}

function applyObjectKeyOrderInPlace(objectList, orderList) {
  if (!orderList || orderList.length === 0) return objectList;
  const rank = new Map(orderList.map((key, index) => [toRel(key), index]));
  const targeted = objectList.filter((key) => rank.has(toRel(key)));
  if (targeted.length === 0) return objectList;
  targeted.sort((a, b) => rank.get(toRel(a)) - rank.get(toRel(b)));
  let cursor = 0;
  return objectList.map((key) => (rank.has(toRel(key)) ? targeted[cursor++] : key));
}

function collectHideMap(intent) {
  /** @type {Map<string, Set<string>>} */
  const hideMap = new Map();
  /** @type {Set<string>} */
  const withdrawSet = new Set();
  for (const item of intent.items || []) {
    const code = item.intent;
    if (code !== "site.hide" && code !== "site.withdraw") continue;
    const workId = item.workId;
    const objectKey = toRel(item.object);
    if (!workId || !objectKey) continue;
    const key = workMapKey(item.channel, workId);
    if (!hideMap.has(key)) hideMap.set(key, new Set());
    hideMap.get(key).add(objectKey);
    if (code === "site.withdraw") withdrawSet.add(objectKey);
  }
  return { hideMap, withdrawSet };
}

function isTechnicalKey(workId) {
  return /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/.test(workId || "");
}

const photoFolderChannels = new Set(["real-world-photo", "game-photo", "ai-photo"]);

function isProjectFolderName(workId) {
  const name = String(workId || "");
  if (!name || name !== name.trim() || name.length > 80 || name.startsWith(".")) return false;
  return !/[\\/\r\n"]/.test(name);
}

function acceptsWorkId(channel, workId) {
  if (isTechnicalKey(workId)) return true;
  return photoFolderChannels.has(channel) && isProjectFolderName(workId);
}

function collectRegisterList(intent) {
  return (intent.items || []).filter((item) => item.intent === "work.register");
}

function collectWithdrawWorkList(intent) {
  return (intent.items || [])
    .filter((item) => item.intent === "work.withdraw")
    .map((item) => ({
      channel: String(item.channel || "").trim(),
      workId: String(item.workId || "").trim(),
      objectList: (item.objectList || []).map((key) => toRel(key)).filter(Boolean),
    }));
}

function matchesWithdraw(withdrawList, channel, workId) {
  return withdrawList.some((item) => item.workId === workId && item.channel === channel);
}

function collectUpdateList(intent) {
  return (intent.items || []).filter((item) => item.intent === "work.update");
}

const photoFactChannelSet = new Set([
  "landscape-photo",
  "humanist-photo",
  "portrait-photo",
  "real-world-photo",
]);
const photoThemeKeyList = ["landscape-photo", "humanist-photo", "portrait-photo", "game-photo", "ai-photo"];

function normalizeThemes(list) {
  const picked = new Set((list || []).map((item) => String(item || "").trim()));
  return photoThemeKeyList.filter((key) => picked.has(key));
}

function themesFromPublishedMedia(block) {
  const mediaAt = block.search(/\n[ \t]*media\s*:/);
  const media = mediaAt < 0 ? "" : block.slice(mediaAt);
  const found = new Set();
  const themeRe = /(?:"themes"|themes)\s*:\s*\[([^\]]*)\]/g;
  let match = themeRe.exec(media);
  while (match) {
    for (const key of photoThemeKeyList) {
      if (match[1].includes(`"${key}"`)) found.add(key);
    }
    match = themeRe.exec(media);
  }
  return photoThemeKeyList.filter((key) => found.has(key));
}

function themesFromPublishedJson(mediaList) {
  const found = new Set();
  for (const entry of mediaList || []) {
    const themes = Array.isArray(entry) ? [] : (entry?.themes || []);
    for (const key of normalizeThemes(themes)) found.add(key);
  }
  return photoThemeKeyList.filter((key) => found.has(key));
}

function normalizeTags(list) {
  const out = [];
  const seen = new Set();
  for (const raw of list || []) {
    const tag = String(raw || "").trim();
    if (!tag || seen.has(tag)) continue;
    if (/^\d{4}$/.test(tag)) {
      throw new Error(`年份不是标签：${tag}`);
    }
    if (photoThemeKeyList.includes(tag)) {
      throw new Error(`类型不进自由标签：${tag}`);
    }
    seen.add(tag);
    out.push(tag);
  }
  return out;
}

function resolveWorkMeta(item) {
  const startedOn = String(item.startedOn || "").trim();
  const place = String(item.place || "").trim();
  const fromDate = startedOn ? startedOn.slice(0, 4) : "";
  const explicit = String(item.year || "").trim() || fromDate;
  const year = photoFactChannelSet.has(String(item.channel || ""))
    ? (/^\d{4}$/.test(explicit) ? explicit : "")
    : (explicit || String(new Date().getFullYear()));
  return { startedOn, place, year };
}

function collectTagList(intent) {
  return (intent.items || []).filter((item) => item.intent === "tags.update");
}

function collectCopyList(intent) {
  return (intent.items || []).filter((item) => item.intent === "copy.update");
}

/** 只改已标明栏目与作品的对象。未入编的对象键留在草稿，不进内容补丁。 */
function collectStarList(intent) {
  return (intent.items || []).filter((item) =>
    item.intent === "stars.update" && item.object && item.channel && item.workId);
}

function readStarValue(value) {
  const stars = Number(value);
  if (!Number.isInteger(stars) || stars < 0 || stars > 5) {
    throw new Error(`星级须为 0～5 的整数：${value}`);
  }
  return stars;
}

function setStarsField(media, stars) {
  const re = /(?:"stars"|\bstars)\s*:\s*\d+/;
  if (stars === 0) {
    if (!re.test(media)) return media;
    let next = media.replace(/\s*,\s*(?:"stars"|\bstars)\s*:\s*\d+/, "");
    next = next.replace(/(?:"stars"|\bstars)\s*:\s*\d+\s*,\s*/, "");
    next = next.replace(/(?:"stars"|\bstars)\s*:\s*\d+/, "");
    return next.replace(/,\s*}/g, " }").replace(/{\s*,/g, "{");
  }
  if (re.test(media)) {
    return media.replace(/((?:"stars"|\bstars)\s*:\s*)\d+/, `$1${stars}`);
  }
  const quoted = /"[a-zA-Z]+"\s*:/.test(media);
  const field = quoted ? `"stars": ${stars}` : `stars: ${stars}`;
  return media.replace(/\s*}\s*$/, `, ${field} }`);
}

function patchStarsEntry(block, objectKey, starsValue) {
  const src = toRel(objectKey);
  const stars = readStarValue(starsValue);
  const tupleRe = new RegExp(`\\[\\s*"${escapeRegExp(src)}"\\s*,\\s*"([^"]*)"\\s*\\]`);
  const tupleMatch = tupleRe.exec(block);
  if (tupleMatch) {
    if (stars === 0) return block;
    return block.replace(tupleMatch[0], formatMediaObject(src, tupleMatch[1], "", "", "", stars));
  }
  const srcRe = new RegExp(`src"\\s*:\\s*"${escapeRegExp(src)}"|src:\\s*"${escapeRegExp(src)}"`);
  const srcMatch = srcRe.exec(block);
  if (!srcMatch) {
    const existing = readNamedStarMap(block, "hiddenStars");
    const next = new Map(existing.map);
    if (stars === 0) next.delete(src);
    else next.set(src, stars);
    return writeNamedStarMap(block, "hiddenStars", existing, next);
  }
  const objStart = block.lastIndexOf("{", srcMatch.index);
  if (objStart < 0) {
    throw new Error(`找不到媒体对象：${src}`);
  }
  let depth = 0;
  let objEnd = -1;
  for (let i = objStart; i < block.length; i += 1) {
    if (block[i] === "{") depth += 1;
    else if (block[i] === "}") {
      depth -= 1;
      if (depth === 0) {
        objEnd = i + 1;
        break;
      }
    }
  }
  if (objEnd < 0) {
    throw new Error(`媒体对象括号不配对：${src}`);
  }
  const media = setStarsField(block.slice(objStart, objEnd), stars);
  return block.slice(0, objStart) + media + block.slice(objEnd);
}

function patchTextStars(text, starList) {
  let next = text;
  const changeList = [];
  for (const item of starList) {
    const found = findObjectBlock(next, item.workId, item.channel || "");
    if (!found) continue;
    const block = patchStarsEntry(found.block, item.object, item.stars);
    next = next.slice(0, found.start) + block + next.slice(found.end);
    changeList.push({
      workId: item.workId,
      kindBefore: "stars",
      kindAfter: "stars",
      before: String(item.object),
      after: String(item.stars),
    });
  }
  return { text: next, changeList };
}

function patchJsonStars(catalog, starList) {
  const changeList = [];
  for (const item of starList) {
    const work = (catalog.works || []).find(
      (entry) => entry.id === item.workId && (entry.channel || "") === (item.channel || ""),
    );
    if (!work) throw new Error(`未知作品：${item.channel}/${item.workId}`);
    const src = toRel(item.object);
    const media = (work.media || []).find((entry) => toRel(entry.src) === src);
    if (!media || !media.src) throw new Error(`对象不属于该作品或无 src：${src}`);
    const stars = readStarValue(item.stars);
    const before = media.stars ?? 0;
    if (stars === 0) delete media.stars;
    else media.stars = stars;
    changeList.push({
      workId: work.id,
      kindBefore: "stars",
      kindAfter: "stars",
      before: String(before),
      after: String(stars),
    });
  }
  return changeList;
}

function isCopyFilled(text) {
  const trimmed = String(text ?? "").trim();
  return trimmed.length > 0 && trimmed !== "待填写描述";
}

function copyValue(text) {
  return isCopyFilled(text) ? String(text).trim() : "";
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function replaceStringField(block, prop, value) {
  const next = JSON.stringify(copyValue(value));
  const quoted = new RegExp(`("${escapeRegExp(prop)}"\\s*:\\s*)"[^"]*"`);
  const bare = new RegExp(`(\\b${escapeRegExp(prop)}\\s*:\\s*)"[^"]*"`);
  if (quoted.test(block)) return block.replace(quoted, `$1${next}`);
  if (bare.test(block)) return block.replace(bare, `$1${next}`);
  throw new Error(`找不到字段 ${prop}`);
}

function removeStringField(block, prop) {
  const line = new RegExp(
    `\\s*(?:"${escapeRegExp(prop)}"|\\b${escapeRegExp(prop)})\\s*:\\s*"[^"]*"\\s*,?`,
  );
  return block.replace(line, "");
}

function insertStringFieldAfter(block, afterProp, prop, value) {
  const re = new RegExp(
    `([^\\n]*?)((?:"${escapeRegExp(afterProp)}"|\\b${escapeRegExp(afterProp)})\\s*:\\s*"[^"]*")(,?)`,
  );
  const match = re.exec(block);
  const quoted = /"[a-zA-Z]+"\s*:/.test(block);
  const key = quoted ? `"${prop}"` : prop;
  const line = `${key}: ${JSON.stringify(String(value))}`;
  if (!match) {
    return block.replace(/}$/, `  ${line},\n}`);
  }
  const indent = match[1];
  const next = match[3]
    ? `${match[1]}${match[2]},\n${indent}${line},`
    : `${match[1]}${match[2]},\n${indent}${line}`;
  return `${block.slice(0, match.index)}${next}${block.slice(match.index + match[0].length)}`;
}

function upsertStringField(block, prop, value, afterProp) {
  const next = String(value ?? "").trim();
  if (!next) {
    return removeStringField(block, prop);
  }
  const quoted = new RegExp(`"${escapeRegExp(prop)}"\\s*:`);
  const bare = new RegExp(`\\b${escapeRegExp(prop)}\\s*:`);
  if (quoted.test(block) || bare.test(block)) {
    return replaceStringField(block, prop, next);
  }
  return insertStringFieldAfter(block, afterProp, prop, next);
}

function applyWorkMetaToBlock(block, item, allowSchedule) {
  let next = replaceStringField(block, "title", item.title);
  if (!allowSchedule) {
    return next;
  }
  const meta = resolveWorkMeta(item);
  next = upsertStringField(next, "year", meta.year, "title");
  next = upsertStringField(next, "startedOn", meta.startedOn, "year");
  const placeAfter = meta.startedOn ? "startedOn" : "year";
  return upsertStringField(next, "place", meta.place, placeAfter);
}

function findObjectBlock(text, workId, channel) {
  const markers = [`"id": "${workId}"`, `id: "${workId}"`];
  let from = 0;
  while (from < text.length) {
    let hit = -1;
    let marker = "";
    for (const candidate of markers) {
      const index = text.indexOf(candidate, from);
      if (index >= 0 && (hit < 0 || index < hit)) {
        hit = index;
        marker = candidate;
      }
    }
    if (hit < 0) return null;
    const start = text.lastIndexOf("{", hit);
    if (start < 0) {
      from = hit + marker.length;
      continue;
    }
    let depth = 0;
    let end = -1;
    for (let i = start; i < text.length; i += 1) {
      if (text[i] === "{") depth += 1;
      else if (text[i] === "}") {
        depth -= 1;
        if (depth === 0) {
          end = i + 1;
          break;
        }
      }
    }
    if (end < 0) return null;
    const block = text.slice(start, end);
    const channelOk =
      !channel ||
      block.includes(`"channel": "${channel}"`) ||
      block.includes(`channel: "${channel}"`);
    if (channelOk) {
      return { start, end, block };
    }
    from = hit + marker.length;
  }
  return null;
}

function lexiconNameForKey(lexiconText, channelKey) {
  const matches = [...String(lexiconText || "").matchAll(/(\w+)\s*:\s*\{[^}]*?key\s*:\s*"([^"]+)"/gs)];
  for (const match of matches) {
    if (match[2] === channelKey) return match[1];
  }
  return "";
}

function patchSiteChannelLead(text, channelKey, description, lexiconText) {
  const name = lexiconNameForKey(lexiconText, channelKey);
  if (!name) {
    throw new Error(`找不到栏目 ${channelKey} 的冻结名。`);
  }
  const idRe = new RegExp(`id\\s*:\\s*lexicon\\.${escapeRegExp(name)}\\.key`, "g");
  let sawId = false;
  let idMatch;
  while ((idMatch = idRe.exec(text))) {
    sawId = true;
    // 顶栏只有 id。导语在后面的栏目对象里，窗口与 ChannelCopyReader 相同：id 之后 1200 字。
    const windowEnd = Math.min(text.length, idMatch.index + idMatch[0].length + 1200);
    const windowText = text.slice(idMatch.index, windowEnd);
    if (!/lead\s*:\s*"[^"]*"/.test(windowText)) continue;
    const patchedWindow = windowText.replace(
      /lead\s*:\s*"[^"]*"/,
      `lead: ${JSON.stringify(copyValue(description))}`,
    );
    return text.slice(0, idMatch.index) + patchedWindow + text.slice(windowEnd);
  }
  if (!sawId) {
    throw new Error(`site.ts 没有栏目 ${channelKey}。`);
  }
  throw new Error(`找不到栏目 ${channelKey} 的 lead。`);
}

function formatMediaObject(src, label, title, description, kind, stars) {
  const body = {
    kind: kind || (/\.mp4$/i.test(src) ? "video" : "image"),
    label,
    src,
  };
  if (isCopyFilled(title)) body.displayName = copyValue(title);
  if (isCopyFilled(description)) body.description = copyValue(description);
  if (Number.isInteger(stars) && stars >= 1 && stars <= 5) body.stars = stars;
  return JSON.stringify(body, null, 2);
}

function patchMediaEntry(block, objectKey, title, description) {
  const src = toRel(objectKey);
  const tupleRe = new RegExp(`\\[\\s*"${escapeRegExp(src)}"\\s*,\\s*"([^"]*)"\\s*\\]`);
  const tupleMatch = tupleRe.exec(block);
  if (tupleMatch) {
    if (!isCopyFilled(title) && !isCopyFilled(description)) {
      return block;
    }
    return block.replace(tupleMatch[0], formatMediaObject(src, tupleMatch[1], title, description));
  }

  const srcRe = new RegExp(`src"\\s*:\\s*"${escapeRegExp(src)}"|src:\\s*"${escapeRegExp(src)}"`);
  const srcMatch = srcRe.exec(block);
  if (!srcMatch) {
    throw new Error(`对象不属于该作品或无 src：${src}`);
  }
  const objStart = block.lastIndexOf("{", srcMatch.index);
  if (objStart < 0) {
    throw new Error(`找不到媒体对象：${src}`);
  }
  let depth = 0;
  let objEnd = -1;
  for (let i = objStart; i < block.length; i += 1) {
    if (block[i] === "{") depth += 1;
    else if (block[i] === "}") {
      depth -= 1;
      if (depth === 0) {
        objEnd = i + 1;
        break;
      }
    }
  }
  if (objEnd < 0) {
    throw new Error(`媒体对象括号不配对：${src}`);
  }
  let media = block.slice(objStart, objEnd);
  const labelMatch = /"label"\s*:\s*"([^"]*)"|label\s*:\s*"([^"]*)"/.exec(media);
  const label = labelMatch ? labelMatch[1] || labelMatch[2] : "";
  const kindMatch = /"kind"\s*:\s*"([^"]*)"|kind\s*:\s*"([^"]*)"/.exec(media);
  const kind = kindMatch ? kindMatch[1] || kindMatch[2] : "";
  if (!isCopyFilled(title) && !isCopyFilled(description) && /\[\s*"/.test(block)) {
    return `${block.slice(0, objStart)}["${src}", "${label}"]${block.slice(objEnd)}`;
  }
  if (/"displayName"|"description"|displayName:|description:/.test(media) || isCopyFilled(title) || isCopyFilled(description)) {
    media = /"label"/.test(media)
      ? media
      : media;
    try {
      media = replaceStringField(media, "displayName", title);
    } catch {
      if (isCopyFilled(title)) {
        media = media.replace(/}$/, `, "displayName": ${JSON.stringify(copyValue(title))} }`);
      }
    }
    try {
      media = replaceStringField(media, "description", description);
    } catch {
      if (isCopyFilled(description)) {
        media = media.replace(/}$/, `, "description": ${JSON.stringify(copyValue(description))} }`);
      }
    }
    if (!isCopyFilled(title)) {
      media = media.replace(/,?\s*"displayName"\s*:\s*"[^"]*"/, "").replace(/,?\s*displayName\s*:\s*"[^"]*"/, "");
    }
    if (!isCopyFilled(description)) {
      media = media.replace(/,?\s*"description"\s*:\s*"[^"]*"/, "").replace(/,?\s*description\s*:\s*"[^"]*"/, "");
    }
    return block.slice(0, objStart) + media + block.slice(objEnd);
  }
  return `${block.slice(0, objStart)}${formatMediaObject(src, label, title, description, kind)}${block.slice(objEnd)}`;
}

function patchJsonCopy(catalog, copyList) {
  const changeList = [];
  for (const item of copyList) {
    const target = item.target;
    if (target === "channel") {
      const channel = (catalog.channels || []).find((entry) => entry.key === item.channel);
      if (!channel) throw new Error(`未知频道：${item.channel}`);
      const before = channel.lead || "";
      if (isCopyFilled(item.description)) channel.lead = copyValue(item.description);
      else delete channel.lead;
      changeList.push({
        workId: item.channel,
        kindBefore: "copy",
        kindAfter: "copy",
        before,
        after: channel.lead || "",
      });
      continue;
    }
    const work = (catalog.works || []).find(
      (entry) => entry.id === item.workId && (entry.channel || "") === (item.channel || ""),
    );
    if (!work) throw new Error(`未知作品：${item.channel}/${item.workId}`);
    if (target === "work") {
      const field = item.channel === "game-dev" ? "lead" : "summary";
      const before = work[field] || work.summary || "";
      if (isCopyFilled(item.description)) {
        work[field] = copyValue(item.description);
        if (field === "lead") work.summary = copyValue(item.description);
      } else {
        work[field] = "";
        if (field === "lead") work.summary = "";
      }
      changeList.push({
        workId: work.id,
        kindBefore: "copy",
        kindAfter: "copy",
        before,
        after: work[field] || "",
      });
      continue;
    }
    const src = toRel(item.object);
    const media = (work.media || []).find((entry) => toRel(entry.src) === src);
    if (!media || !media.src) throw new Error(`对象不属于该作品或无 src：${src}`);
    const before = `${media.displayName || ""}|${media.description || ""}`;
    if (isCopyFilled(item.title)) media.displayName = copyValue(item.title);
    else delete media.displayName;
    if (isCopyFilled(item.description)) media.description = copyValue(item.description);
    else delete media.description;
    changeList.push({
      workId: work.id,
      kindBefore: "copy",
      kindAfter: "copy",
      before,
      after: `${media.displayName || ""}|${media.description || ""}`,
    });
  }
  return changeList;
}

function patchTextCopy(text, copyList, options) {
  let next = text;
  const changeList = [];
  const { channelField = "summary", lexiconText = "", matchChannel = true } = options || {};
  for (const item of copyList) {
    if (item.target === "channel") {
      if (!lexiconText) continue;
      const before = next;
      next = patchSiteChannelLead(next, item.channel, item.description, lexiconText);
      if (next !== before) {
        changeList.push({
          workId: item.channel,
          kindBefore: "copy",
          kindAfter: "copy",
          before: "lead",
          after: copyValue(item.description),
        });
      }
      continue;
    }
    const found = findObjectBlock(next, item.workId, matchChannel ? item.channel : "");
    if (!found) continue;
    let block = found.block;
    if (item.target === "work") {
      const field = item.channel === "game-dev" ? "lead" : channelField;
      block = replaceStringField(block, field, item.description);
    } else if (item.target === "media") {
      block = patchMediaEntry(block, item.object, item.title, item.description);
    }
    next = next.slice(0, found.start) + block + next.slice(found.end);
    changeList.push({
      workId: item.workId,
      kindBefore: "copy",
      kindAfter: "copy",
      before: found.block,
      after: block,
    });
  }
  return { text: next, changeList };
}

function formatJsonWork(work) {
  return JSON.stringify(work, null, 2);
}

function formatTaggedMedia(src, label, themes, tags, extra) {
  if (themes.length === 0 && tags.length === 0 && !extra) {
    return `["${src}", "${escapeTsString(label)}"]`;
  }
  const body = {
    kind: extra?.kind || (/\.mp4$/i.test(src) ? "video" : "image"),
    label,
    src,
  };
  if (extra?.poster) body.poster = extra.poster;
  if (extra?.displayName) body.displayName = extra.displayName;
  if (extra?.description) body.description = extra.description;
  if (Number.isInteger(extra?.stars) && extra.stars >= 1 && extra.stars <= 5) body.stars = extra.stars;
  if (themes.length) body.themes = themes;
  if (tags.length) body.tags = tags;
  return JSON.stringify(body);
}

function readQuotedField(media, prop) {
  const match = new RegExp(`(?:"${prop}"|${prop})\\s*:\\s*"([^"]*)"`).exec(media);
  return match ? match[1] : "";
}

function applyFrameTags(block, objectKey, themes, tags) {
  const src = toRel(objectKey);
  const tupleRe = new RegExp(`\\[\\s*"${escapeRegExp(src)}"\\s*,\\s*"([^"]*)"\\s*\\]`);
  const tupleMatch = tupleRe.exec(block);
  if (tupleMatch) {
    return block.replace(tupleMatch[0], formatTaggedMedia(src, tupleMatch[1], themes, tags));
  }
  const srcRe = new RegExp(`(?:"src"|src)\\s*:\\s*"${escapeRegExp(src)}"`);
  const srcMatch = srcRe.exec(block);
  if (!srcMatch) {
    throw new Error(`对象不属于该作品或无 src：${src}`);
  }
  const objStart = block.lastIndexOf("{", srcMatch.index);
  if (objStart < 0) {
    throw new Error(`找不到媒体对象：${src}`);
  }
  let depth = 0;
  let objEnd = -1;
  for (let i = objStart; i < block.length; i += 1) {
    if (block[i] === "{") depth += 1;
    else if (block[i] === "}") {
      depth -= 1;
      if (depth === 0) {
        objEnd = i + 1;
        break;
      }
    }
  }
  if (objEnd < 0) {
    throw new Error(`媒体对象括号不配对：${src}`);
  }
  const media = block.slice(objStart, objEnd);
  const extra = {
    kind: readQuotedField(media, "kind"),
    poster: readQuotedField(media, "poster"),
    displayName: readQuotedField(media, "displayName"),
    description: readQuotedField(media, "description"),
  };
  const next = formatTaggedMedia(src, readQuotedField(media, "label"), themes, tags, extra);
  if (!next.includes(src)) {
    throw new Error(`src 被改写：${src}`);
  }
  return block.slice(0, objStart) + next + block.slice(objEnd);
}

function upsertWorkArrayField(block, prop, values) {
  const mediaAt = block.search(/\n[ \t]*media\s*:/);
  const head = mediaAt < 0 ? block : block.slice(0, mediaAt);
  const tail = mediaAt < 0 ? "" : block.slice(mediaAt);
  const lineRe = new RegExp(`\\n[ \\t]*${prop}\\s*:\\s*\\[[\\s\\S]*?\\],?`);
  let nextHead = head.replace(lineRe, "");
  if (values.length > 0) {
    const literal = `${prop}: [${values.map((value) => JSON.stringify(value)).join(", ")}],`;
    nextHead = `${nextHead.replace(/\s*$/, "")}\n    ${literal}\n`;
  }
  return nextHead + tail;
}

function patchTextTags(text, tagList) {
  let next = text;
  const changeList = [];
  for (const item of tagList) {
    const channel = String(item.channel || "").trim();
    const workId = String(item.workId || "").trim();
    const found = findObjectBlock(next, workId, channel);
    if (!found) continue;
    const themes = normalizeThemes(item.themes);
    const tags = normalizeTags(item.tags);
    const objectList = (item.objectList || []).map((key) => toRel(key)).filter(Boolean);
    let block = found.block;
    if (objectList.length === 0) {
      block = upsertWorkArrayField(block, "tags", tags);
    } else {
      for (const objectKey of objectList) {
        block = applyFrameTags(block, objectKey, themes, tags);
        if (!block.includes(objectKey) && !block.includes(toRel(objectKey))) {
          throw new Error(`src 被改写：${objectKey}`);
        }
      }
      block = upsertWorkArrayField(block, "themes", themesFromPublishedMedia(block));
    }
    next = next.slice(0, found.start) + block + next.slice(found.end);
    changeList.push({
      workId,
      channel,
      kindBefore: "tags",
      kindAfter: "tags",
      before: found.block,
      after: block,
    });
  }
  return { text: next, changeList };
}

function patchJsonTags(catalog, tagList) {
  const changeList = [];
  catalog.works = catalog.works || [];
  for (const item of tagList) {
    const work = catalog.works.find(
      (row) => row.id === item.workId && (row.channel || "") === (item.channel || ""),
    );
    if (!work) {
      throw new Error(`找不到已入编作品：${item.channel}/${item.workId}`);
    }
    const themes = normalizeThemes(item.themes);
    const tags = normalizeTags(item.tags);
    const objectList = (item.objectList || []).map((key) => toRel(key)).filter(Boolean);
    const before = JSON.stringify(work.media || []);
    if (objectList.length === 0) {
      if (tags.length) work.tags = tags;
      else delete work.tags;
    } else {
      work.media = work.media || [];
      for (const src of objectList) {
        const index = work.media.findIndex((entry) => {
          if (Array.isArray(entry)) return toRel(entry[0] || "") === src;
          return toRel(entry?.src || "") === src;
        });
        if (index < 0) {
          throw new Error(`对象不属于该作品或无 src：${src}`);
        }
        const entry = work.media[index];
        const label = Array.isArray(entry) ? (entry[1] || "") : (entry.label || "");
        const next = Array.isArray(entry)
          ? { kind: /\.mp4$/i.test(src) ? "video" : "image", label, src }
          : entry;
        if (themes.length) next.themes = [...themes];
        else delete next.themes;
        if (tags.length) next.tags = [...tags];
        else delete next.tags;
        next.src = src;
        work.media[index] = next;
      }
      const derived = themesFromPublishedJson(work.media);
      if (derived.length) work.themes = derived;
      else delete work.themes;
    }
    changeList.push({
      workId: work.id,
      channel: work.channel || "",
      kindBefore: "tags",
      kindAfter: "tags",
      before,
      after: JSON.stringify(work.media || []),
    });
  }
  return changeList;
}

function patchJsonRegister(catalog, registerList) {
  const changeList = [];
  catalog.works = catalog.works || [];
  for (const item of registerList) {
    const workId = item.workId || "";
    const channel = item.channel || "";
    const title = (item.title || "").trim();
    const stageFolder = item.stageFolder ? toRel(item.stageFolder) : "";
    if (!workId || !channel || !title) {
      throw new Error("登记条目缺少 channel、id 或 title。");
    }
    if (!acceptsWorkId(channel, workId)) {
      throw new Error(`作品 id 须为小写短横线技术键：${workId}`);
    }
    const exists = catalog.works.some(
      (work) => work.id === workId && (work.channel || "") === channel,
    );
    if (exists) {
      throw new Error(`该栏目已有同 id 作品：${channel}/${workId}`);
    }
    const meta = resolveWorkMeta(item);
    const work = {
      id: workId,
      channel,
      title,
      year: meta.year,
      ...(meta.startedOn ? { startedOn: meta.startedOn } : {}),
      ...(meta.place ? { place: meta.place } : {}),
      ...(stageFolder ? { stageFolder } : {}),
      media: [],
    };
    catalog.works.push(work);
    changeList.push({
      workId,
      kindBefore: "none",
      kindAfter: "json",
      removed: [],
      added: [],
      before: "(无此作品)",
      after: formatJsonWork(work),
    });
  }
  return changeList;
}

function escapeTsString(value) {
  return String(value ?? "").replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function formatTsWork(item) {
  const meta = resolveWorkMeta(item);
  const stageFolder = item.stageFolder ? toRel(item.stageFolder) : "";
  const lineList = [
    "  {",
    `    id: "${escapeTsString(item.workId)}",`,
    `    channel: "${item.channel}",`,
    `    title: "${escapeTsString((item.title || "").trim())}",`,
    `    year: "${meta.year}",`,
  ];
  if (meta.startedOn) {
    lineList.push(`    startedOn: "${escapeTsString(meta.startedOn)}",`);
  }
  if (meta.place) {
    lineList.push(`    place: "${escapeTsString(meta.place)}",`);
  }
  lineList.push(
    `    summary: "",`,
    `    body: "",`,
    `    consent: "pending",`,
  );
  if (stageFolder) {
    lineList.push(`    stageFolder: "${stageFolder}",`);
  }
  lineList.push("    media: [],", "  }");
  return lineList.join("\n");
}

function patchTextWorkUpdate(text, updateList, options) {
  const { titleOnly = false } = options || {};
  let next = text;
  const changeList = [];
  for (const item of updateList) {
    const allowSchedule = !titleOnly
      && item.channel !== "profile"
      && item.channel !== "game-dev";
    const found = findObjectBlock(next, item.workId, item.channel)
      || (titleOnly ? findObjectBlock(next, item.workId, "") : null);
    if (!found) continue;
    const block = applyWorkMetaToBlock(found.block, item, allowSchedule);
    next = next.slice(0, found.start) + block + next.slice(found.end);
    changeList.push({
      workId: item.workId,
      kindBefore: "meta",
      kindAfter: "meta",
      before: found.block,
      after: block,
    });
  }
  return { text: next, changeList };
}

function patchJsonUpdate(catalog, updateList) {
  const changeList = [];
  catalog.works = catalog.works || [];
  for (const item of updateList) {
    const work = catalog.works.find(
      (row) => row.id === item.workId && (row.channel || "") === (item.channel || ""),
    );
    if (!work) {
      throw new Error(`找不到已入编作品：${item.channel}/${item.workId}`);
    }
    const before = formatJsonWork(work);
    work.title = String(item.title || "").trim();
    const meta = resolveWorkMeta(item);
    work.year = meta.year;
    if (meta.startedOn) work.startedOn = meta.startedOn;
    else delete work.startedOn;
    if (meta.place) work.place = meta.place;
    else delete work.place;
    changeList.push({
      workId: item.workId,
      kindBefore: "meta",
      kindAfter: "meta",
      before,
      after: formatJsonWork(work),
    });
  }
  return changeList;
}

function collectWorkKeysFromTs(text) {
  const keyList = [];
  const idRe = /id:\s*"([^"]+)"/g;
  const matchList = [...text.matchAll(idRe)];
  for (let i = 0; i < matchList.length; i += 1) {
    const from = matchList[i].index;
    const to = i + 1 < matchList.length ? matchList[i + 1].index : text.length;
    const block = text.slice(from, to);
    const channelMatch = /channel:\s*"([^"]+)"/.exec(block);
    keyList.push({
      id: matchList[i][1],
      channel: channelMatch ? channelMatch[1] : "",
    });
  }
  return keyList;
}

function findRegisterArrayMarker(text) {
  if (text.includes("export const registeredWorks")) {
    return "export const registeredWorks";
  }
  if (text.includes("const registeredWorks")) {
    return "const registeredWorks";
  }
  if (text.includes("export const placeholderWorks")) {
    return "export const placeholderWorks";
  }
  throw new Error("works.ts 缺少 registeredWorks 或 placeholderWorks，无法登记。");
}

function insertBeforeArrayClose(text, marker, snippet) {
  const start = text.indexOf(marker);
  if (start < 0) {
    throw new Error(`找不到 ${marker}。`);
  }
  const open = findAssignedArrayOpen(text, start + marker.length);
  if (open < 0) {
    throw new Error(`${marker} 不是数组。`);
  }
  const close = matchDelimited(text, open, "[", "]");
  const inner = text.slice(open + 1, close);
  const prefix = inner.trim().length === 0
    ? "\n"
    : inner.trimEnd().endsWith(",")
      ? "\n"
      : ",\n";
  return `${text.slice(0, close)}${prefix}${snippet}\n${text.slice(close)}`;
}

/** 取值数组的 `[`：跳过 `WorkRecord[]`、`Array<[string, string]>` 等类型括号。 */
function findAssignedArrayOpen(text, from) {
  let angle = 0;
  let brace = 0;
  let paren = 0;
  let seenEquals = false;
  let inString = false;
  let quote = "";
  for (let i = from; i < text.length; i += 1) {
    const ch = text[i];
    if (inString) {
      if (ch === "\\") {
        i += 1;
        continue;
      }
      if (ch === quote) inString = false;
      continue;
    }
    if (ch === "\"" || ch === "'" || ch === "`") {
      inString = true;
      quote = ch;
      continue;
    }
    if (ch === "<") {
      angle += 1;
      continue;
    }
    if (ch === ">" && angle > 0) {
      angle -= 1;
      continue;
    }
    if (angle > 0) continue;
    if (ch === "{") {
      brace += 1;
      continue;
    }
    if (ch === "}" && brace > 0) {
      brace -= 1;
      continue;
    }
    if (ch === "(") {
      paren += 1;
      continue;
    }
    if (ch === ")" && paren > 0) {
      paren -= 1;
      continue;
    }
    if (!seenEquals) {
      if (brace === 0 && paren === 0 && ch === "=") seenEquals = true;
      continue;
    }
    if (ch === "[") return i;
  }
  return -1;
}

function formatProfileSession(item) {
  const stageFolder = item.stageFolder ? toRel(item.stageFolder) : "";
  return [
    "  {",
    `    id: "${item.workId}",`,
    `    title: "${escapeTsString((item.title || "").trim())}",`,
    `    stageFolder: "${stageFolder}",`,
    "  }",
  ].join("\n");
}

function formatGameShell(item) {
  const stageFolder = item.stageFolder ? toRel(item.stageFolder) : "";
  const lineList = [
    "  {",
    `    id: "${item.workId}",`,
    `    title: "${escapeTsString((item.title || "").trim())}",`,
    `    titleEn: "",`,
    `    lead: "",`,
    `    playable: false,`,
    `    consent: "pending",`,
  ];
  if (stageFolder) {
    lineList.push(`    stageFolder: "${stageFolder}",`);
  }
  lineList.push("    screenshots: [],", "  }");
  return lineList.join("\n");
}

function findNamedArrayMarker(text, markerList) {
  for (const marker of markerList) {
    if (text.includes(marker)) return marker;
  }
  throw new Error(`找不到 ${markerList.join(" / ")}。`);
}

function patchNamedRegister(text, registerList, markerList, formatFn, kindAfter) {
  const changeList = [];
  let next = text;
  const marker = findNamedArrayMarker(next, markerList);
  for (const item of registerList) {
    const workId = item.workId || "";
    const channel = item.channel || "";
    const title = (item.title || "").trim();
    if (!workId || !channel || !title) {
      throw new Error("登记条目缺少 channel、id 或 title。");
    }
    if (!acceptsWorkId(channel, workId)) {
      throw new Error(`作品 id 须为小写短横线技术键：${workId}`);
    }
    const snippet = formatFn(item);
    next = insertBeforeArrayClose(next, marker, snippet);
    changeList.push({
      workId,
      channel,
      kindBefore: "none",
      kindAfter,
      removed: [],
      added: [],
      before: "(无此作品)",
      after: snippet,
    });
  }
  return { text: next, changeList };
}

function patchTsRegister(text, registerList) {
  const changeList = [];
  let next = text;
  const marker = findRegisterArrayMarker(next);
  for (const item of registerList) {
    const workId = item.workId || "";
    const channel = item.channel || "";
    const title = (item.title || "").trim();
    if (!workId || !channel || !title) {
      throw new Error("登记条目缺少 channel、id 或 title。");
    }
    if (!acceptsWorkId(channel, workId)) {
      throw new Error(`作品 id 须为小写短横线技术键：${workId}`);
    }
    const snippet = formatTsWork(item);
    next = insertBeforeArrayClose(next, marker, snippet);
    changeList.push({
      workId,
      channel,
      kindBefore: "none",
      kindAfter: "works",
      removed: [],
      added: [],
      before: "(无此作品)",
      after: snippet,
    });
  }
  return { text: next, changeList };
}

function isPreparedRel(rel) {
  const normalized = String(rel || "").replace(/\\/g, "/");
  return /(?:^|\/)\.site-ready\//i.test(normalized);
}

function isDigitChar(ch) {
  return ch >= "0" && ch <= "9";
}

function compareLogical(left, right) {
  const a = toRel(left);
  const b = toRel(right);
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    if (isDigitChar(a[i]) && isDigitChar(b[j])) {
      let nextI = i;
      let nextJ = j;
      while (nextI < a.length && isDigitChar(a[nextI])) nextI += 1;
      while (nextJ < b.length && isDigitChar(b[nextJ])) nextJ += 1;
      const valueA = Number(a.slice(i, nextI));
      const valueB = Number(b.slice(j, nextJ));
      if (valueA !== valueB) return valueA < valueB ? -1 : 1;
      i = nextI;
      j = nextJ;
      continue;
    }
    const ca = a[i].toLowerCase();
    const cb = b[j].toLowerCase();
    if (ca !== cb) return ca < cb ? -1 : 1;
    i += 1;
    j += 1;
  }
  return a.length - i - (b.length - j);
}

function preferSource(sourceStageRel, stageRel, objectKey) {
  if (sourceStageRel && !isPreparedRel(sourceStageRel)) return toRel(sourceStageRel);
  if (stageRel && !isPreparedRel(stageRel)) return toRel(stageRel);
  return toRel(objectKey);
}

function sortKeyOf(objectKey) {
  const obj = toRel(objectKey);
  return sortKeyMap.get(obj) || obj;
}

function collectSortKeyMap(intent, ledgerDict) {
  sortKeyMap = new Map();
  for (const record of Object.values(ledgerDict || {})) {
    const objectKey = toRel(record.object);
    const key = preferSource(record.sourceStageRel, record.stageRel, objectKey);
    if (objectKey && key) sortKeyMap.set(objectKey, key);
  }
  for (const item of intent.items || []) {
    const objectKey = toRel(item.object);
    const key = preferSource(item.sourceStageRel, item.stageRel, objectKey);
    if (objectKey && key) sortKeyMap.set(objectKey, key);
  }
}

async function loadLedgerDict(profile, root) {
  const rel = profile.ledgerPath;
  if (!rel) return {};
  try {
    const json = JSON.parse(stripBom(await readFile(resolveUnderRoot(root, rel), "utf8")));
    const dict = {};
    for (const record of json.records || []) {
      if (record.object) dict[toRel(record.object)] = record;
    }
    return dict;
  } catch {
    return {};
  }
}

function hasDistinctStagePath(objectKey, sortKey) {
  const obj = toRel(objectKey);
  return Boolean(sortKey) && sortKey.toLowerCase() !== obj.toLowerCase();
}

function orderMediaByToolSort(mediaList) {
  const keyedList = mediaList.map((item, index) => ({
    item,
    index,
    key: sortKeyOf(item.src),
  }));
  if (!keyedList.some((row) => hasDistinctStagePath(row.item.src, row.key))) {
    return mediaList;
  }
  return keyedList
    .sort((a, b) => compareLogical(a.key, b.key) || a.index - b.index)
    .map((row) => row.item);
}

function orderObjectKeysByToolSort(objectList) {
  const keyedList = objectList.map((objectKey, index) => ({
    objectKey,
    index,
    key: sortKeyOf(objectKey),
  }));
  if (!keyedList.some((row) => hasDistinctStagePath(row.objectKey, row.key))) {
    return objectList;
  }
  return keyedList
    .sort((a, b) => compareLogical(a.key, b.key) || a.index - b.index)
    .map((row) => row.objectKey);
}

function fileStem(rel) {
  const name = String(rel || "").replace(/\\/g, "/").split("/").pop() || "";
  const dot = name.lastIndexOf(".");
  return (dot > 0 ? name.slice(0, dot) : name).trim();
}

function defaultVideoLabel(sourceStageRel, stageRel) {
  for (const rel of [sourceStageRel, stageRel]) {
    if (!rel || isPreparedRel(rel)) continue;
    const name = fileStem(rel);
    if (name) return name;
  }
  return "视频";
}

/** @type {Map<string, number>} */
let ingestStarMap = new Map();
let withdrawObjectSet = new Set();

function collectStageStarMap(intent) {
  const map = new Map();
  for (const item of intent.items || []) {
    if (item.intent !== "stars.update" || item.object || !item.stageRel) continue;
    const stars = Number(item.stars);
    if (!Number.isInteger(stars) || stars < 1 || stars > 5) continue;
    map.set(toRel(item.stageRel), stars);
  }
  return map;
}

function collectIngestMap(intent) {
  /** @type {Map<string, string[]>} */
  const ingestMap = new Map();
  ingestVideoLabelMap = new Map();
  ingestStarMap = new Map();
  const stageStars = collectStageStarMap(intent);
  for (const item of intent.items || []) {
    if (item.intent !== "stage.ingest" && item.intent !== "site.restore") continue;
    const workId = item.workId;
    const objectKey = toRel(item.object);
    if (!workId || !objectKey) continue;
    const key = workMapKey(item.channel, workId);
    if (!ingestMap.has(key)) ingestMap.set(key, []);
    const objectList = ingestMap.get(key);
    if (!objectList.includes(objectKey)) {
      objectList.push(objectKey);
      ingestVideoLabelMap.set(objectKey, defaultVideoLabel(item.sourceStageRel, item.stageRel));
      for (const rel of [item.sourceStageRel, item.stageRel]) {
        const stars = rel ? stageStars.get(toRel(rel)) : undefined;
        if (stars) {
          ingestStarMap.set(objectKey, stars);
          break;
        }
      }
    }
  }
  return ingestMap;
}

function resolveWorkMaps(hideMap, ingestMap, block, media) {
  const keyed = workMapKey(block.channel, block.id);
  let objectSet = hideMap.get(keyed);
  let ingestList = ingestMap.get(keyed);
  if (!objectSet && !ingestList) {
    objectSet = hideMap.get(block.id);
    ingestList = ingestMap.get(block.id);
    if (objectSet && media) {
      const hit = [...objectSet].some((objectKey) =>
        media.mediaList.some((item) => toRel(item.src) === objectKey),
      );
      if (!hit) objectSet = undefined;
    }
  }
  return { objectSet, ingestList, orderList: orderListOf(block.channel, block.id) };
}

function countRefs(works) {
  /** @type {Map<string, number>} */
  const refMap = new Map();
  const add = (raw) => {
    const key = toRel(raw);
    if (!key) return;
    refMap.set(key, (refMap.get(key) || 0) + 1);
  };
  for (const work of works) {
    for (const media of work.media || []) {
      add(media.src);
      add(media.poster);
    }
  }
  return refMap;
}

function splitWorkBlocks(text) {
  const marker = "export const placeholderWorks";
  const start = text.indexOf(marker);
  if (start < 0) {
    throw new Error("works.ts 缺少 export const placeholderWorks。");
  }
  const slice = text.slice(start);
  const idRe = /id:\s*"([^"]+)"/g;
  /** @type {{ id: string, channel: string, from: number, to: number, block: string }[]} */
  const blockList = [];
  const matchList = [...slice.matchAll(idRe)];
  for (let i = 0; i < matchList.length; i += 1) {
    const from = matchList[i].index;
    const to = i + 1 < matchList.length ? matchList[i + 1].index : slice.length;
    const block = slice.slice(from, to);
    const channelMatch = /channel:\s*"([^"]+)"/.exec(block);
    blockList.push({
      id: matchList[i][1],
      channel: channelMatch ? channelMatch[1] : "",
      from: start + from,
      to: start + to,
      block,
    });
  }
  return blockList;
}

function parseAlbumMedia(channel, id, count, ext) {
  const mediaList = [];
  for (let i = 1; i <= count; i += 1) {
    const slot = String(i).padStart(2, "0");
    mediaList.push({
      kind: "image",
      label: `效果图 ${slot}`,
      src: `${channel}/${id}/${slot}.${ext}`,
    });
  }
  return mediaList;
}

function parseListedMedia(body) {
  const mediaList = [];
  const entryRe = /\[\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\]/g;
  for (const match of body.matchAll(entryRe)) {
    mediaList.push({
      kind: "image",
      label: match[2],
      src: toRel(match[1]),
    });
  }
  return mediaList;
}

function parseCoveredMedia(channel, id, labelsBody) {
  const labels = [...String(labelsBody || "").matchAll(/"([^"]+)"/g)].map((match) => match[1]);
  return labels.map((label, index) => ({
    kind: "image",
    label,
    src: index === 0 ? `${channel}/${id}/01.webp` : undefined,
  }));
}

function isVideo(item) {
  return String(item.kind || "").toLowerCase() === "video";
}

function readObjectField(body, name) {
  const match = new RegExp(`(?:"${name}"|${name})\\s*:\\s*"([^"]+)"`).exec(body || "");
  return match ? match[1] : undefined;
}

function readObjectStars(body) {
  const match = /(?:"stars"|stars)\s*:\s*(\d+)/.exec(body || "");
  if (!match) return undefined;
  const stars = Number(match[1]);
  return stars >= 1 && stars <= 5 ? stars : undefined;
}

function readObjectStringArray(body, name) {
  const match = new RegExp(`(?:"${name}"|${name})\\s*:\\s*\\[([^\\]]*)\\]`).exec(body || "");
  if (!match) return [];
  return [...match[1].matchAll(/"([^"]+)"/g)].map((item) => item[1]);
}

function formatVideoObject(item) {
  if (!item.src) return `{ kind: "video", label: "${item.label}" }`;
  const poster = item.poster ? `, poster: "${item.poster}"` : "";
  const stars = Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5 ? `, stars: ${item.stars}` : "";
  return `{ kind: "video", src: "${item.src}"${poster}, label: "${item.label}"${stars} }`;
}

function tryParseJsonMedia(inner) {
  const trimmed = String(inner || "").trim();
  if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) return null;
  let value;
  try {
    value = JSON.parse(trimmed.startsWith("[") ? trimmed : `[${trimmed}]`);
  } catch {
    return null;
  }
  if (!Array.isArray(value)) return null;
  return value.map((item) => {
    if (Array.isArray(item)) {
      return { kind: "image", src: toRel(item[0]), label: String(item[1] || "") };
    }
    return {
      kind: item.kind || "image",
      label: item.label || "",
      src: item.src ? toRel(item.src) : undefined,
      poster: item.poster,
      displayName: item.displayName,
      description: item.description,
      themes: Array.isArray(item.themes) ? item.themes : [],
      tags: Array.isArray(item.tags) ? item.tags : [],
      stars: Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5 ? item.stars : undefined,
    };
  });
}

function parseCoverArray(inner) {
  const mediaList = [];
  const tokenRe =
    /cover\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)|\.\.\.images\(([^)]*)\)|\{\s*(?:"kind"|kind)\s*:\s*["'](video|image)["']([^}]*)\}/g;
  let found = false;
  for (const match of inner.matchAll(tokenRe)) {
    found = true;
    if (match[1] != null) {
      mediaList.push({
        kind: "image",
        label: match[3],
        src: `${match[1]}/${match[2]}/01.webp`,
      });
    } else if (match[5] != null) {
      mediaList.push({
        kind: match[5],
        label: readObjectField(match[6], "label") || "",
        src: readObjectField(match[6], "src"),
        poster: readObjectField(match[6], "poster"),
        themes: readObjectStringArray(match[6], "themes"),
        tags: readObjectStringArray(match[6], "tags"),
        stars: readObjectStars(match[6]),
      });
    } else {
      const labels = [...String(match[4] || "").matchAll(/"([^"]+)"/g)].map((item) => item[1]);
      for (const label of labels) {
        mediaList.push({ kind: "image", label });
      }
    }
  }
  return found ? mediaList : null;
}

function extractMedia(block) {
  const album = /media:\s*album\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(\d+)(?:\s*,\s*"([^"]+)")?\s*\)/.exec(block);
  if (album) {
    return {
      kind: "album",
      expr: album[0],
      mediaList: parseAlbumMedia(album[1], album[2], Number(album[3]), album[4] || "jpg"),
    };
  }
  const listedStart = block.search(/media:\s*listed\s*\(/);
  if (listedStart >= 0) {
    const open = block.indexOf("(", listedStart);
    const close = matchDelimited(block, open, "(", ")");
    const expr = block.slice(listedStart, close + 1);
    return {
      kind: "listed",
      expr,
      mediaList: parseListedMedia(block.slice(open + 1, close)),
    };
  }
  const coveredStart = block.search(/media:\s*covered\s*\(/);
  if (coveredStart >= 0) {
    const open = block.indexOf("(", coveredStart);
    const close = matchDelimited(block, open, "(", ")");
    const expr = block.slice(coveredStart, close + 1);
    const parsed = /covered\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*\[([\s\S]*)\]\s*\)/.exec(expr);
    if (!parsed) {
      return null;
    }
    return {
      kind: "covered",
      expr,
      mediaList: parseCoveredMedia(parsed[1], parsed[2], parsed[3]),
    };
  }
  const arrayStart = block.search(/media:\s*\[/);
  if (arrayStart >= 0) {
    const open = block.indexOf("[", arrayStart);
    const close = matchDelimited(block, open, "[", "]");
    const expr = block.slice(arrayStart, close + 1);
    const inner = block.slice(open + 1, close);
    const mediaList = parseCoverArray(inner) || tryParseJsonMedia(inner);
    if (!mediaList) {
      return null;
    }
    return {
      kind: "cover-array",
      expr,
      mediaList,
    };
  }
  return null;
}

function matchDelimited(text, openIndex, openChar, closeChar) {
  let depth = 0;
  for (let i = openIndex; i < text.length; i += 1) {
    if (text[i] === openChar) depth += 1;
    else if (text[i] === closeChar) {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  throw new Error("media 括号不配对。");
}

function applyHideToMedia(mediaList, objectSet) {
  const removed = [];
  const kept = [];
  const parked = [];
  for (const item of mediaList) {
    if (isVideo(item)) {
      const src = toRel(item.src);
      const poster = toRel(item.poster);
      if ((src && objectSet.has(src)) || (poster && objectSet.has(poster))) {
        if (src) removed.push(src);
        if (poster) removed.push(poster);
        parked.push(item);
        kept.push({ kind: "video", label: item.label });
      } else {
        kept.push(item);
      }
      continue;
    }
    const src = toRel(item.src);
    if (src && objectSet.has(src)) {
      removed.push(src);
      parked.push(item);
    } else {
      kept.push(item);
    }
  }
  return { removed, afterList: kept, parked };
}

function posterObjectKey(objectKey) {
  return String(objectKey || "").replace(/\.mp4$/i, ".poster.webp");
}

function isFormalVideoObject(objectKey) {
  return /\.mp4$/i.test(objectKey || "") && !/\.poster\.webp$/i.test(objectKey || "");
}

function isPosterObject(objectKey) {
  return /\.poster\.webp$/i.test(objectKey || "");
}

function applyIngestToMedia(mediaList, objectList) {
  const next = [...mediaList];
  const added = [];
  for (const objectKey of objectList) {
    if (isPosterObject(objectKey) || next.some((item) => toRel(item.src) === objectKey)) continue;
    if (isFormalVideoObject(objectKey)) {
      const emptyIndex = next.findIndex((item) => isVideo(item) && !item.src);
      const sourceLabel = ingestVideoLabelMap.get(objectKey) || "视频";
      const video = {
        kind: "video",
        label: emptyIndex >= 0 ? next[emptyIndex].label : sourceLabel,
        src: objectKey,
        poster: posterObjectKey(objectKey),
      };
      if (emptyIndex >= 0) next[emptyIndex] = video;
      else next.push(video);
      const videoStars = ingestStarMap.get(objectKey);
      if (videoStars) video.stars = videoStars;
      added.push(objectKey);
      continue;
    }
    const image = { kind: "image", label: "", src: objectKey };
    const imageStars = ingestStarMap.get(objectKey);
    if (imageStars) image.stars = imageStars;
    next.push(image);
    added.push(objectKey);
  }
  return { added, afterList: next };
}

function planMedia(mediaList, hideSet, ingestList, orderList, channel) {
  const { removed, afterList: keptList, parked } = applyHideToMedia(mediaList, hideSet || new Set());
  const { added, afterList } = applyIngestToMedia(keptList, ingestList || []);
  const ordered = applyMediaOrder(afterList, orderList);
  if (channel === "real-world-photo" || channel === "game-photo" || channel === "ai-photo") {
    return { removed, added, parked, afterList: relabelPhoto(ordered, photoStemMap) };
  }
  const shouldRelabel = ordered.length > 0 && ordered.every((item) => item.src);
  return { removed, added, parked, afterList: shouldRelabel ? relabel(ordered) : ordered };
}

function readNamedStarMap(block, property) {
  const match = new RegExp(`${property}\\s*:\\s*\\{`).exec(block);
  if (!match) return { from: -1, to: -1, map: new Map() };
  const open = block.indexOf("{", match.index);
  const close = matchDelimited(block, open, "{", "}");
  const map = new Map();
  const body = block.slice(open + 1, close);
  for (const item of body.matchAll(/"([^"]+)"\s*:\s*(\d+)/g)) {
    const stars = Number(item[2]);
    if (stars >= 1 && stars <= 5) map.set(toRel(item[1]), stars);
  }
  return { from: match.index, to: close + 1, map };
}

function formatNamedStarMap(property, starMap) {
  const lineList = [...starMap.entries()].map(([src, stars]) => `    "${src}": ${stars},`);
  return `${property}: {\n${lineList.join("\n")}\n  }`;
}

function removeNamedStarProperty(block, from, to) {
  let start = from;
  let end = to;
  if (block[end] === ",") end += 1;
  while (start > 0 && (block[start - 1] === " " || block[start - 1] === "\t")) start -= 1;
  if (start > 0 && block[start - 1] === "\n") start -= 1;
  return block.slice(0, start) + block.slice(end);
}

function writeNamedStarMap(block, property, existing, starMap) {
  if (starMap.size === 0) {
    return existing.from < 0 ? block : removeNamedStarProperty(block, existing.from, existing.to);
  }
  const formatted = formatNamedStarMap(property, starMap);
  if (existing.from >= 0) {
    return block.slice(0, existing.from) + formatted + block.slice(existing.to);
  }
  const close = block.lastIndexOf("}");
  if (close < 0) return block;
  let head = block.slice(0, close).replace(/\s*$/, "");
  if (!head.endsWith(",") && !head.endsWith("{")) head += ",";
  return `${head}\n  ${formatted},\n${block.slice(close)}`;
}

function absorbHiddenStars(afterList, starMap) {
  for (const item of afterList || []) {
    const src = toRel(item.src);
    if (!src || item.stars) continue;
    const stars = starMap.get(src);
    if (stars) item.stars = stars;
  }
}

function commitHiddenStars(block, parked, addedSrcList) {
  const existing = readNamedStarMap(block, "hiddenStars");
  const next = new Map(existing.map);
  for (const item of parked || []) {
    const src = toRel(item.src);
    if (!src) continue;
    if (withdrawObjectSet.has(src)) {
      next.delete(src);
      continue;
    }
    if (Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5) next.set(src, item.stars);
    else next.delete(src);
  }
  for (const src of addedSrcList || []) next.delete(toRel(src));
  return writeNamedStarMap(block, "hiddenStars", existing, next);
}

function mediaHasSrc(mediaList) {
  return (mediaList || []).some((item) => item.src);
}

/**
 * 人像授权不随上页自动公开；其余栏目上页 / 恢复写出 src 后把 pending 改为 granted。
 */
function shouldGrantVisitorConsent(channel) {
  return channel !== "portrait-photo";
}

/**
 * 上页或恢复补上 src 后，非人像空壳从 pending 改为 granted，访客列表才能见到。
 * 仅登记空壳、人像授权、只隐藏不追加时不改。
 */
function applyVisitorConsentAfterPublish(block, channel, afterList, addedCount) {
  if (addedCount <= 0 || !shouldGrantVisitorConsent(channel) || !mediaHasSrc(afterList)) {
    return block;
  }
  return block.replace(/consent:\s*"pending"/, 'consent: "granted"');
}

function flushPendingImages(partList, pendingLabelList) {
  if (pendingLabelList.length === 0) return;
  partList.push(`...images(${pendingLabelList.map((label) => `"${label}"`).join(", ")})`);
  pendingLabelList.length = 0;
}

function parseCoverSrc(src) {
  const match = /^([^/]+)\/([^/]+)\/01\.webp$/.exec(src);
  return match ? { channel: match[1], id: match[2] } : null;
}

function formatCoverArray(afterList) {
  const partList = [];
  const pendingLabelList = [];
  for (const item of afterList) {
    if (isVideo(item)) {
      flushPendingImages(partList, pendingLabelList);
      partList.push(formatVideoObject(item));
      continue;
    }
    if (!item.src) {
      pendingLabelList.push(item.label);
      continue;
    }
    flushPendingImages(partList, pendingLabelList);
    const cover = parseCoverSrc(item.src);
    if (cover) {
      partList.push(`cover("${cover.channel}", "${cover.id}", "${item.label}")`);
    } else {
      partList.push(`{ kind: "image", label: "${item.label}", src: "${item.src}" }`);
    }
  }
  flushPendingImages(partList, pendingLabelList);
  return `media: [${partList.join(", ")}]`;
}

function canRewriteAfter(afterList) {
  const imageList = afterList.filter((item) => !isVideo(item));
  if (imageList.length === 0 || imageList.every((item) => item.src)) return true;
  return imageList.every((item) => !item.src);
}

function formatAfterExpr(afterList) {
  if (afterList.some(isVideo)) {
    return formatCoverArray(afterList);
  }
  if (afterList.length === 0) {
    return "media: []";
  }
  const allHaveSrc = afterList.every((item) => item.src);
  if (allHaveSrc) {
    if (afterList.some((item) => hasMediaExtra(item))) {
      return formatRichMedia(afterList);
    }
    return `media: ${formatListed(afterList)}`;
  }
  if (afterList.some((item) => item.src)) {
    throw new Error("剩余媒体 src 与占位槽混排，本轮不改写。");
  }
  return `media: images(${afterList.map((item) => `"${item.label}"`).join(", ")})`;
}

function afterKind(afterList) {
  if (afterList.some(isVideo)) return "cover-array";
  if (afterList.length === 0) return "empty";
  if (afterList.every((item) => item.src)) return "listed";
  return "images";
}

async function loadProfile(profilePath) {
  const fullPath = path.resolve(profilePath);
  const profile = JSON.parse(stripBom(await readFile(fullPath, "utf8")));
  const profileDir = path.dirname(fullPath);
  const root = profile.root
    ? path.resolve(profileDir, profile.root)
    : profileDir;
  return { profile, root, profilePath: fullPath };
}

async function loadIntent(intentPath) {
  return JSON.parse(stripBom(await readFile(path.resolve(intentPath), "utf8")));
}

function patchJsonCatalog(catalog, hideMap, ingestMap) {
  const changeList = [];
  const works = catalog.works || [];
  for (const work of works) {
    const media = { mediaList: work.media || [] };
    const { objectSet, ingestList, orderList } = resolveWorkMaps(
      hideMap,
      ingestMap,
      { id: work.id, channel: work.channel || "" },
      media,
    );
    if (!objectSet && !ingestList && !orderList) continue;
    const before = work.media || [];
    const parkedMap = new Map(
      Object.entries(work.hiddenStars || {}).map(([key, stars]) => [toRel(key), Number(stars)]),
    );
    const { removed, added, afterList, parked } = planMedia(before, objectSet, ingestList, orderList, work.channel || "");
    if (removed.length === 0 && added.length === 0 && sameSrcOrder(srcKeyList(before), srcKeyList(afterList))) continue;
    absorbHiddenStars(afterList, parkedMap);
    work.media = afterList;
    const nextHidden = {};
    for (const [key, stars] of parkedMap) {
      if (stars >= 1 && stars <= 5) nextHidden[key] = stars;
    }
    for (const item of parked || []) {
      const src = toRel(item.src);
      if (!src) continue;
      if (withdrawObjectSet.has(src) || !(Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5)) delete nextHidden[src];
      else nextHidden[src] = item.stars;
    }
    for (const src of added) delete nextHidden[toRel(src)];
    if (Object.keys(nextHidden).length === 0) delete work.hiddenStars;
    else work.hiddenStars = nextHidden;
    if (
      added.length > 0 &&
      shouldGrantVisitorConsent(work.channel || "") &&
      mediaHasSrc(afterList) &&
      work.consent === "pending"
    ) {
      work.consent = "granted";
    }
    changeList.push({
      workId: work.id,
      kindBefore: "json",
      kindAfter: "json",
      removed,
      added,
      before: formatJsonMedia(before),
      after: formatJsonMedia(afterList),
    });
  }
  return changeList;
}

function patchWorksTs(text, hideMap, ingestMap) {
  const blockList = splitWorkBlocks(text);
  const changeList = [];
  let next = text;
  for (const item of [...blockList].reverse()) {
    const media = extractMedia(item.block);
    const { objectSet, ingestList, orderList } = resolveWorkMaps(hideMap, ingestMap, item, media);
    if (!objectSet && !ingestList && !orderList) continue;
    if (!media) {
      throw new Error(`作品 ${item.channel || "?"}/${item.id} 不是 album / listed / cover / covered，不改写。`);
    }
    const parkedMap = readNamedStarMap(item.block, "hiddenStars").map;
    const { removed, added, afterList, parked } = planMedia(media.mediaList, objectSet, ingestList, orderList, item.channel || "");
    if (removed.length === 0 && added.length === 0 && sameSrcOrder(srcKeyList(media.mediaList), srcKeyList(afterList))) continue;
    if (!canRewriteAfter(afterList)) continue;
    absorbHiddenStars(afterList, parkedMap);
    const afterExpr = formatAfterExpr(afterList);
    const patchedBlock = commitHiddenStars(
      applyVisitorConsentAfterPublish(
        item.block.replace(media.expr, afterExpr),
        item.channel,
        afterList,
        added.length,
      ),
      parked,
      added,
    );
    next = next.slice(0, item.from) + patchedBlock + next.slice(item.to);
    changeList.unshift({
      workId: item.id,
      channel: item.channel,
      kindBefore: media.kind,
      kindAfter: afterKind(afterList),
      removed,
      added,
      before: media.expr,
      after: afterExpr,
    });
  }
  return { text: next, changeList };
}

function collectWorksFromTs(text) {
  const blockList = splitWorkBlocks(text);
  return blockList.map((item) => {
    const media = extractMedia(item.block);
    return { id: item.id, channel: item.channel, media: media ? media.mediaList : [] };
  });
}

function parseStringProperty(block, property) {
  const match = new RegExp(`["']?${property}["']?\\s*:\\s*"([^"]+)"`).exec(block);
  return match ? toRel(match[1]) : "";
}

function findArrayProperty(block, property) {
  const match = new RegExp(`["']?${property}["']?\\s*:\\s*\\[`).exec(block);
  if (!match) return null;
  const open = block.indexOf("[", match.index);
  const close = matchDelimited(block, open, "[", "]");
  return {
    property,
    from: open,
    to: close + 1,
    expr: block.slice(open, close + 1),
    values: [...block.slice(open + 1, close).matchAll(/"([^"]+)"/g)].map((item) => toRel(item[1])),
  };
}

function findStringProperty(block, property) {
  const match = new RegExp(`["']?${property}["']?\\s*:\\s*"([^"]+)"`).exec(block);
  if (!match) return null;
  const valueFrom = match.index + match[0].indexOf('"');
  return {
    property,
    from: valueFrom,
    to: match.index + match[0].length,
    expr: block.slice(valueFrom, match.index + match[0].length),
    value: toRel(match[1]),
  };
}

function findAssignedBlock(text, marker, openChar, closeChar) {
  const markerIndex = text.indexOf(marker);
  if (markerIndex < 0) return null;
  const from = markerIndex + marker.length;
  const open = openChar === "["
    ? findAssignedArrayOpen(text, from)
    : text.indexOf(openChar, from);
  if (open < 0) return null;
  const close = matchDelimited(text, open, openChar, closeChar);
  return { from: open, to: close + 1, block: text.slice(open, close + 1) };
}

function splitTopLevelObjects(arrayBlock) {
  const blockList = [];
  for (let i = 0; i < arrayBlock.length; i += 1) {
    if (arrayBlock[i] !== "{") continue;
    const close = matchDelimited(arrayBlock, i, "{", "}");
    blockList.push({ from: i, to: close + 1, block: arrayBlock.slice(i, close + 1) });
    i = close;
  }
  return blockList;
}

function formatStringArray(valueList, indent = "    ") {
  if (valueList.length === 0) return "[]";
  return `[\n${valueList.map((value) => `${indent}"${value}",`).join("\n")}\n  ]`;
}

function distinctValues(valueList) {
  return [...new Set(valueList.filter(Boolean).map(toRel))];
}

function collectChannelIntent(hideMap, ingestMap, channel) {
  const hideSet = new Set();
  const ingestList = [];
  const prefix = `${channel}::`;
  for (const [key, set] of hideMap) {
    if (key === channel || key.startsWith(prefix)) {
      for (const value of set) hideSet.add(value);
    }
  }
  for (const [key, list] of ingestMap) {
    if (key === channel || key.startsWith(prefix)) {
      for (const value of list) {
        if (!ingestList.includes(value)) ingestList.push(value);
      }
    }
  }
  return { hideSet, ingestList };
}

function mappedIntent(hideMap, ingestMap, channel, workId) {
  const keyed = workMapKey(channel, workId);
  return {
    hideSet: hideMap.get(keyed) || hideMap.get(workId) || new Set(),
    ingestList: ingestMap.get(keyed) || ingestMap.get(workId) || [],
  };
}

/** 形象照字符串没有 media 槽，星级写在 profile.portraitStars，不改 portraitSrcs 的字符串形态。 */
function readPortraitStarMap(block) {
  const match = /portraitStars\s*:\s*\{/.exec(block);
  if (!match) return { from: -1, to: -1, map: new Map() };
  const open = block.indexOf("{", match.index);
  const close = matchDelimited(block, open, "{", "}");
  const map = new Map();
  const body = block.slice(open + 1, close);
  for (const item of body.matchAll(/"([^"]+)"\s*:\s*(\d+)/g)) {
    const stars = Number(item[2]);
    if (stars >= 1 && stars <= 5) map.set(toRel(item[1]), stars);
  }
  return { from: match.index, to: close + 1, map };
}

function formatPortraitStars(starMap) {
  const lineList = [...starMap.entries()].map(([src, stars]) => `    "${src}": ${stars},`);
  return `portraitStars: {\n${lineList.join("\n")}\n  }`;
}

function removePortraitStarProperty(block, from, to) {
  let start = from;
  let end = to;
  if (block[end] === ",") end += 1;
  while (start > 0 && (block[start - 1] === " " || block[start - 1] === "\t")) start -= 1;
  if (start > 0 && block[start - 1] === "\n") start -= 1;
  return block.slice(0, start) + block.slice(end);
}

function writePortraitStarMap(block, existing, starMap, array) {
  if (starMap.size === 0) {
    return existing.from < 0 ? block : removePortraitStarProperty(block, existing.from, existing.to);
  }
  const formatted = formatPortraitStars(starMap);
  if (existing.from >= 0) {
    return block.slice(0, existing.from) + formatted + block.slice(existing.to);
  }
  let insertAt = array.to;
  if (block[insertAt] === ",") insertAt += 1;
  return `${block.slice(0, insertAt)}\n  ${formatted},${block.slice(insertAt)}`;
}

function patchProfileStars(text, starList) {
  const itemList = starList.filter((item) => item.channel === "profile");
  if (itemList.length === 0) return { text, changeList: [] };
  const assigned = findAssignedBlock(text, "export const profile", "{", "}");
  if (!assigned) {
    throw new Error("形象星级须写入 site.ts 的 profile。");
  }
  const array = findArrayProperty(assigned.block, "portraitSrcs");
  const primary = findStringProperty(assigned.block, "portraitSrc");
  if (!array || !primary) {
    throw new Error("site.ts 的 profile 必须同时声明 portraitSrcs 与 portraitSrc。");
  }
  const existing = readPortraitStarMap(assigned.block);
  const nextMap = new Map(existing.map);
  const changeList = [];
  for (const item of itemList) {
    const src = toRel(item.object);
    const stars = readStarValue(item.stars);
    if (stars === 0) nextMap.delete(src);
    else nextMap.set(src, stars);
    changeList.push({
      workId: item.workId,
      channel: "profile",
      kindBefore: "stars",
      kindAfter: "stars",
      before: String(item.object),
      after: String(stars),
    });
  }
  const nextBlock = writePortraitStarMap(assigned.block, existing, nextMap, array);
  return {
    text: text.slice(0, assigned.from) + nextBlock + text.slice(assigned.to),
    changeList,
  };
}

function patchProfileSite(text, hideMap, ingestMap) {
  const assigned = findAssignedBlock(text, "export const profile", "{", "}");
  if (!assigned) return { text, changeList: [] };
  const array = findArrayProperty(assigned.block, "portraitSrcs");
  const primary = findStringProperty(assigned.block, "portraitSrc");
  if (!array || !primary) {
    throw new Error("site.ts 的 profile 必须同时声明 portraitSrcs 与 portraitSrc。");
  }

  const { hideSet, ingestList } = collectChannelIntent(hideMap, ingestMap, "profile");
  const orderLists = collectChannelOrderLists("profile");
  const beforeList = distinctValues([...array.values, primary.value]);
  const removed = beforeList.filter((value) => hideSet.has(value));
  const added = ingestList.filter((value) => !beforeList.includes(value));
  if (removed.length === 0 && added.length === 0 && orderLists.length === 0) return { text, changeList: [] };
  let afterList = distinctValues([
    ...beforeList.filter((value) => !hideSet.has(value)),
    ...ingestList,
  ]);
  for (const orderList of orderLists) {
    afterList = applyObjectKeyOrderInPlace(afterList, orderList);
  }
  if (afterList.length === 0) {
    throw new Error("profile 主图隐藏后没有剩余图片，拒绝留下断引用。");
  }
  const nextPrimary = afterList[0];
  const replacementList = [
    { from: array.from, to: array.to, value: formatStringArray(afterList) },
    { from: primary.from, to: primary.to, value: `"${nextPrimary}"` },
  ].sort((a, b) => b.from - a.from);
  let nextBlock = assigned.block;
  for (const replacement of replacementList) {
    nextBlock =
      nextBlock.slice(0, replacement.from) + replacement.value + nextBlock.slice(replacement.to);
  }
  return {
    text: text.slice(0, assigned.from) + nextBlock + text.slice(assigned.to),
    changeList: [{
      workId: "portrait",
      channel: "profile",
      kindBefore: "site-profile",
      kindAfter: "site-profile",
      removed,
      added,
      before: `portraitSrc: "${primary.value}"\nportraitSrcs: ${array.expr}`,
      after: `portraitSrc: "${nextPrimary}"\nportraitSrcs: ${formatStringArray(afterList)}`,
    }],
  };
}

function patchGameBlock(block, hideSet, ingestList, gameId, orderList) {
  const array =
    findArrayProperty(block, "gallerySrcs") ||
    findArrayProperty(block, "coverSrcs") ||
    findArrayProperty(block, "imageSrcs");
  const primary = findStringProperty(block, "coverSrc");
  const beforeList = distinctValues([...(array?.values || []), primary?.value || ""]);
  const removed = beforeList.filter((value) => hideSet.has(value));
  const added = ingestList.filter((value) => !beforeList.includes(value));
  if (removed.length === 0 && added.length === 0 && !orderList) return null;
  const afterList = applyObjectKeyOrder(
    distinctValues([
      ...beforeList.filter((value) => !hideSet.has(value)),
      ...ingestList,
    ]),
    orderList,
  );
  if (afterList.length === 0) {
    throw new Error(`游戏项目 ${gameId} 删除后没有剩余主图，拒绝留下断引用。`);
  }
  if (!array && afterList.length > 1) {
    throw new Error(`游戏项目 ${gameId} 只有 coverSrc 单图字段，不能安全追加图库图片。`);
  }

  const nextPrimary = afterList[0];
  const replacementList = [];
  if (array) {
    replacementList.push({ from: array.from, to: array.to, value: formatStringArray(afterList, "      ") });
  }
  if (primary) {
    replacementList.push({ from: primary.from, to: primary.to, value: `"${nextPrimary}"` });
  }
  replacementList.sort((a, b) => b.from - a.from);
  let nextBlock = block;
  for (const replacement of replacementList) {
    nextBlock =
      nextBlock.slice(0, replacement.from) + replacement.value + nextBlock.slice(replacement.to);
  }
  return {
    block: nextBlock,
    change: {
      workId: gameId,
      channel: "game-dev",
      kindBefore: array ? "site-game-gallery" : "site-game-cover",
      kindAfter: array ? "site-game-gallery" : "site-game-cover",
      removed,
      added,
      before: array?.expr || primary?.expr || "",
      after: array ? formatStringArray(afterList, "      ") : `"${nextPrimary}"`,
    },
  };
}

function patchGamesSite(text, hideMap, ingestMap) {
  const assigned = findAssignedBlock(text, "export const placeholderGames", "[", "]");
  if (!assigned) return { text, changeList: [] };
  const blockList = splitTopLevelObjects(assigned.block);
  const changeList = [];
  let nextArray = assigned.block;
  for (const item of [...blockList].reverse()) {
    const gameId = parseStringProperty(item.block, "id");
    if (!gameId) continue;
    const { hideSet, ingestList } = mappedIntent(hideMap, ingestMap, "game-dev", gameId);
    const orderList = orderListOf("game-dev", gameId);
    if (hideSet.size === 0 && ingestList.length === 0 && !orderList) continue;
    const patched = patchGameBlock(item.block, hideSet, ingestList, gameId, orderList);
    if (!patched) continue;
    nextArray = nextArray.slice(0, item.from) + patched.block + nextArray.slice(item.to);
    changeList.unshift(patched.change);
  }
  return {
    text: text.slice(0, assigned.from) + nextArray + text.slice(assigned.to),
    changeList,
  };
}

function patchSiteTs(text, hideMap, ingestMap) {
  const profilePatched = patchProfileSite(text, hideMap, ingestMap);
  const gamesPatched = patchGamesSite(profilePatched.text, hideMap, ingestMap);
  return {
    text: gamesPatched.text,
    changeList: [...profilePatched.changeList, ...gamesPatched.changeList],
  };
}

function collectRegisteredKeys(text, marker, channel) {
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) return [];
  return splitTopLevelObjects(assigned.block)
    .map((item) => ({
      id: parseStringProperty(item.block, "id"),
      channel,
    }))
    .filter((work) => work.id);
}

function collectSiteWorks(text) {
  const workList = [];
  const profile = findAssignedBlock(text, "export const profile", "{", "}");
  if (profile) {
    const array = findArrayProperty(profile.block, "portraitSrcs");
    const primary = findStringProperty(profile.block, "portraitSrc");
    workList.push({
      id: "portrait",
      channel: "profile",
      media: [...(array?.values || []), ...(primary ? [primary.value] : [])].map((src) => ({ src })),
    });
    const sessions = findAssignedBlock(text, "export const registeredProfileSessions", "[", "]")
      || findAssignedBlock(text, "const registeredProfileSessions", "[", "]");
    if (sessions) {
      for (const item of splitTopLevelObjects(sessions.block)) {
        const id = parseStringProperty(item.block, "id");
        if (id) workList.push({ id, channel: "profile", media: [] });
      }
    }
  }
  const games = findAssignedBlock(text, "export const placeholderGames", "[", "]");
  if (games) {
    for (const item of splitTopLevelObjects(games.block)) {
      const id = parseStringProperty(item.block, "id");
      const array =
        findArrayProperty(item.block, "gallerySrcs") ||
        findArrayProperty(item.block, "coverSrcs") ||
        findArrayProperty(item.block, "imageSrcs");
      const primary = findStringProperty(item.block, "coverSrc");
      workList.push({
        id,
        channel: "game-dev",
        media: [...(array?.values || []), ...(primary ? [primary.value] : [])].map((src) => ({ src })),
      });
    }
  }
  workList.push(...collectRegisteredKeys(text, "export const registeredProfileSessions", "profile"));
  workList.push(...collectRegisteredKeys(text, "const registeredProfileSessions", "profile"));
  workList.push(...collectRegisteredKeys(text, "export const registeredGames", "game-dev"));
  workList.push(...collectRegisteredKeys(text, "const registeredGames", "game-dev"));
  return workList;
}

function assertWithdrawAfterPatch(works, withdrawSet) {
  const refMap = countRefs(works);
  const blocked = [...withdrawSet].filter((objectKey) => (refMap.get(objectKey) || 0) > 0);
  if (blocked.length > 0) {
    throw new Error(`批次含撤下，但下列对象仍被未纳入批次的作品引用（含站点级内容）：${blocked.join("、")}`);
  }
}

function matchBraceEnd(text, openIndex) {
  let depth = 0;
  for (let i = openIndex; i < text.length; i += 1) {
    const ch = text[i];
    if (ch === "{") depth += 1;
    else if (ch === "}") {
      depth -= 1;
      if (depth === 0) return i + 1;
    }
  }
  return -1;
}

function stripObjectKeyProperty(text, objectKey) {
  const needle = `${JSON.stringify(objectKey)}:`;
  let next = text;
  let index = next.indexOf(needle);
  while (index >= 0) {
    let valueStart = index + needle.length;
    while (valueStart < next.length && /\s/.test(next[valueStart])) valueStart += 1;
    if (valueStart >= next.length) break;
    let valueEnd = -1;
    if (next[valueStart] === "{") {
      valueEnd = matchBraceEnd(next, valueStart);
    } else {
      const comma = next.indexOf(",", valueStart);
      valueEnd = comma < 0 ? next.length : comma;
    }
    if (valueEnd < 0) break;
    const cut = expandObjectRemoval(next, index, valueEnd);
    next = next.slice(0, cut.from) + next.slice(cut.to);
    index = next.indexOf(needle);
  }
  return next;
}

async function stripSatelliteMaps(dir, objectKeys) {
  const fileList = [];
  for (const name of ["photoSizes.ts", "photoExif.ts"]) {
    const targetPath = path.join(dir, name);
    let original;
    try {
      original = stripBom(await readFile(targetPath, "utf8"));
    } catch (error) {
      if (error && error.code === "ENOENT") continue;
      throw error;
    }
    let next = original;
    for (const key of objectKeys) {
      next = stripObjectKeyProperty(next, key);
    }
    if (next !== original) {
      fileList.push({ targetPath, nextText: next });
    }
  }
  return fileList;
}

function formatTupleMedia(mediaList, indent = "    ") {
  const lines = mediaList.map((item, index) => {
    const comma = index + 1 < mediaList.length ? "," : "";
    const rich = hasMediaExtra(item) || item.poster || isVideo(item);
    const body = rich
      ? formatTaggedMedia(item.src || "", item.label || "", item.themes || [], item.tags || [], {
        kind: item.kind,
        poster: item.poster,
        displayName: item.displayName,
        description: item.description,
        stars: item.stars,
      })
      : `[\n${indent}  "${item.src}",\n${indent}  "${item.label}"\n${indent}]`;
    return `${indent}${body}${comma}`;
  });
  return `[\n${lines.join("\n")}\n  ]`;
}

/**
 * 截图数组按原文顺序读二元组与对象。只扫二元组会在改写时丢掉对象上的星级。
 */
function parseMixedArray(expr) {
  const open = expr.indexOf("[");
  const close = matchDelimited(expr, open, "[", "]");
  const inner = expr.slice(open + 1, close);
  const mediaList = [];
  let index = 0;
  while (index < inner.length) {
    const ch = inner[index];
    if (ch === " " || ch === "\n" || ch === "\r" || ch === "\t" || ch === ",") {
      index += 1;
      continue;
    }
    if (ch === "[") {
      const end = matchDelimited(inner, index, "[", "]");
      const tuple = /\[\s*"([^"]+)"\s*,\s*"([^"]*)"\s*\]/.exec(inner.slice(index, end + 1));
      if (tuple) {
        mediaList.push({ kind: "image", src: toRel(tuple[1]), label: tuple[2] });
      }
      index = end + 1;
      continue;
    }
    if (ch === "{") {
      const end = matchDelimited(inner, index, "{", "}");
      const body = inner.slice(index + 1, end);
      const src = readObjectField(body, "src");
      mediaList.push({
        kind: readObjectField(body, "kind") || (src && /\.mp4$/i.test(src) ? "video" : "image"),
        label: readObjectField(body, "label") || "",
        src: src ? toRel(src) : undefined,
        poster: readObjectField(body, "poster"),
        displayName: readObjectField(body, "displayName"),
        description: readObjectField(body, "description"),
        themes: readObjectStringArray(body, "themes"),
        tags: readObjectStringArray(body, "tags"),
        stars: readObjectStars(body),
      });
      index = end + 1;
      continue;
    }
    index += 1;
  }
  return mediaList;
}

function extractTupleMedia(block, property) {
  const array = findArrayProperty(block, property);
  if (!array) return null;
  return { ...array, mediaList: parseMixedArray(array.expr) };
}

function patchTupleCatalogIfPresent(text, markerList, property, defaultChannel, hideMap, ingestMap) {
  const marker = markerList.find((item) => findAssignedBlock(text, item, "[", "]"));
  if (!marker) return { text, changeList: [] };
  return patchTupleCatalog(text, marker, property, defaultChannel, hideMap, ingestMap);
}

function patchTupleCatalog(text, marker, property, defaultChannel, hideMap, ingestMap) {
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) throw new Error(`内容源缺少 ${marker}。`);
  const blockList = splitTopLevelObjects(assigned.block);
  const changeList = [];
  let nextArray = assigned.block;
  for (const item of [...blockList].reverse()) {
    const id = parseStringProperty(item.block, "id");
    const channel = parseStringProperty(item.block, "channel") || defaultChannel;
    const media = extractTupleMedia(item.block, property);
    if (!id || !channel || !media) continue;
    const { hideSet, ingestList } = mappedIntent(hideMap, ingestMap, channel, id);
    const orderList = orderListOf(channel, id);
    if (hideSet.size === 0 && ingestList.length === 0 && !orderList) continue;
    const parkedMap = readNamedStarMap(item.block, "hiddenStars").map;
    const { removed, added, afterList, parked } = planMedia(media.mediaList, hideSet, ingestList, orderList, channel);
    if (removed.length === 0 && added.length === 0 && sameSrcOrder(srcKeyList(media.mediaList), srcKeyList(afterList))) continue;
    if (afterList.length === 0 && defaultChannel === "game-dev") {
      throw new Error(`游戏项目 ${id} 删除后没有剩余主图，拒绝留下断引用。`);
    }
    absorbHiddenStars(afterList, parkedMap);
    const afterExpr = formatTupleMedia(afterList);
    const nextBlock = commitHiddenStars(
      item.block.slice(0, media.from) + afterExpr + item.block.slice(media.to),
      parked,
      added,
    );
    nextArray = nextArray.slice(0, item.from) + nextBlock + nextArray.slice(item.to);
    changeList.unshift({
      workId: id,
      channel,
      kindBefore: property,
      kindAfter: property,
      removed,
      added,
      before: media.expr,
      after: afterExpr,
    });
  }
  return {
    text: text.slice(0, assigned.from) + nextArray + text.slice(assigned.to),
    changeList,
  };
}

function collectTupleWorks(text, marker, property, defaultChannel) {
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) return [];
  return splitTopLevelObjects(assigned.block).map((item) => {
    const media = extractTupleMedia(item.block, property);
    return {
      id: parseStringProperty(item.block, "id"),
      channel: parseStringProperty(item.block, "channel") || defaultChannel,
      media: media?.mediaList || [],
    };
  });
}

function extractObjectMedia(block) {
  const array = findArrayProperty(block, "media");
  if (!array) return null;
  const mediaList = [];
  for (const item of splitTopLevelObjects(array.expr)) {
    const kind = parseStringProperty(item.block, "kind");
    const label = parseStringProperty(item.block, "label");
    const src = parseStringProperty(item.block, "src");
    const poster = parseStringProperty(item.block, "poster");
    if (!kind) continue;
    const frame = { kind, label, src: src || undefined, poster: poster || undefined };
    const themes = readObjectStringArray(item.block, "themes");
    const tags = readObjectStringArray(item.block, "tags");
    const displayName = readQuotedField(item.block, "displayName");
    const description = readQuotedField(item.block, "description");
    if (themes.length) frame.themes = themes;
    if (tags.length) frame.tags = tags;
    if (displayName) frame.displayName = displayName;
    if (description) frame.description = description;
    const stars = readObjectStars(item.block);
    if (stars) frame.stars = stars;
    mediaList.push(frame);
  }
  return { ...array, mediaList };
}

/**
 * 帧上已写的类型、自由标签、显示名、说明与星级。没有则空串，避免无标签帧改变写法。
 */
function formatFrameExtras(item) {
  const partList = [];
  if (item.themes?.length) {
    partList.push(`themes: [${item.themes.map((value) => `"${escapeTsString(value)}"`).join(", ")}]`);
  }
  if (item.tags?.length) {
    partList.push(`tags: [${item.tags.map((value) => `"${escapeTsString(value)}"`).join(", ")}]`);
  }
  if (item.displayName) partList.push(`displayName: "${escapeTsString(item.displayName)}"`);
  if (item.description) partList.push(`description: "${escapeTsString(item.description)}"`);
  if (Number.isInteger(item.stars) && item.stars >= 1 && item.stars <= 5) {
    partList.push(`stars: ${item.stars}`);
  }
  return partList.length ? `, ${partList.join(", ")}` : "";
}

function formatObjectMedia(mediaList) {
  return `[${mediaList.map((item) => {
    if (isVideo(item)) return formatVideoObject(item);
    const src = item.src ? `, src: "${item.src}"` : "";
    return `{ kind: "${item.kind || "image"}", label: "${item.label}"${src}${formatFrameExtras(item)} }`;
  }).join(", ")}]`;
}

function patchInlineWorks(text, hideMap, ingestMap, marker = "const lineSimulationWorks") {
  if (!marker) return { text, changeList: [] };
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) return { text, changeList: [] };
  const blockList = splitTopLevelObjects(assigned.block);
  const changeList = [];
  let nextArray = assigned.block;
  for (const item of [...blockList].reverse()) {
    const id = parseStringProperty(item.block, "id");
    const media = extractObjectMedia(item.block);
    const channel =
      parseStringProperty(item.block, "channel") ||
      media?.mediaList.find((entry) => entry.src)?.src?.split("/")[0] ||
      "";
    if (!id || !channel || !media) continue;
    const { hideSet, ingestList } = mappedIntent(hideMap, ingestMap, channel, id);
    const orderList = orderListOf(channel, id);
    if (hideSet.size === 0 && ingestList.length === 0 && !orderList) continue;
    const parkedMap = readNamedStarMap(item.block, "hiddenStars").map;
    const { removed, added, afterList, parked } = planMedia(media.mediaList, hideSet, ingestList, orderList, channel);
    if (removed.length === 0 && added.length === 0 && sameSrcOrder(srcKeyList(media.mediaList), srcKeyList(afterList))) continue;
    absorbHiddenStars(afterList, parkedMap);
    const afterExpr = formatObjectMedia(afterList);
    const nextBlock = commitHiddenStars(
      applyVisitorConsentAfterPublish(
        item.block.slice(0, media.from) + afterExpr + item.block.slice(media.to),
        channel,
        afterList,
        added.length,
      ),
      parked,
      added,
    );
    nextArray = nextArray.slice(0, item.from) + nextBlock + nextArray.slice(item.to);
    changeList.unshift({
      workId: id,
      channel,
      kindBefore: "media-array",
      kindAfter: "media-array",
      removed,
      added,
      before: media.expr,
      after: afterExpr,
    });
  }
  return {
    text: text.slice(0, assigned.from) + nextArray + text.slice(assigned.to),
    changeList,
  };
}

function collectInlineWorks(text, marker = "const lineSimulationWorks") {
  if (!marker) return [];
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) return [];
  return splitTopLevelObjects(assigned.block).map((item) => {
    const media = extractObjectMedia(item.block);
    return {
      id: parseStringProperty(item.block, "id"),
      channel:
        parseStringProperty(item.block, "channel") ||
        media?.mediaList.find((entry) => entry.src)?.src?.split("/")[0] ||
        "",
      media: media?.mediaList || [],
    };
  });
}

async function writeAllAtomically(fileList, failAfterWrite = false) {
  const transactionId = `${process.pid}-${Date.now()}`;
  const stateList = fileList.map((item) => ({
    ...item,
    tempPath: `${item.targetPath}.tmp-sitemedia-${transactionId}`,
    backupPath: `${item.targetPath}.bak-sitemedia-${transactionId}`,
    committed: false,
  }));
  try {
    for (const state of stateList) {
      await writeFile(state.tempPath, state.nextText, "utf8");
      await copyFile(state.targetPath, state.backupPath);
    }
    for (let i = 0; i < stateList.length; i += 1) {
      const state = stateList[i];
      await unlink(state.targetPath);
      await rename(state.tempPath, state.targetPath);
      state.committed = true;
      if (failAfterWrite && i === 0) {
        throw new Error("回归注入：首个文件写盘后失败。");
      }
    }
    for (const state of stateList) {
      await unlink(state.backupPath);
    }
    return { restored: false };
  } catch (error) {
    let restored = true;
    for (const state of stateList) {
      try {
        await copyFile(state.backupPath, state.targetPath);
      } catch {
        restored = false;
      }
      await unlink(state.tempPath).catch(() => {});
      if (restored) await unlink(state.backupPath).catch(() => {});
    }
    const reason = error instanceof Error ? error.message : String(error);
    throw new Error(`写盘失败，${restored ? "已还原" : "未能完整还原"}内容层：${reason}`);
  }
}

function collectKeysFromMediaList(mediaList) {
  const keys = [];
  const seen = new Set();
  const add = (raw) => {
    const rel = toRel(raw);
    if (!rel || seen.has(rel)) return;
    seen.add(rel);
    keys.push(rel);
  };
  for (const item of mediaList || []) {
    add(item.src);
    add(item.poster);
    if (!item.poster && isFormalVideoObject(item.src)) add(posterObjectKey(item.src));
  }
  return keys;
}

function collectKeysFromWorkBlock(block) {
  const objectMedia = extractObjectMedia(block);
  if (objectMedia && objectMedia.mediaList.length > 0) {
    return collectKeysFromMediaList(objectMedia.mediaList);
  }
  const tuple = extractTupleMedia(block, "media");
  if (tuple && tuple.mediaList.length > 0) {
    return collectKeysFromMediaList(tuple.mediaList);
  }
  try {
    const media = extractMedia(block);
    if (media) return collectKeysFromMediaList(media.mediaList);
  } catch {
    // 括号不配对时仍删除记录，对象键留空。
  }
  return [];
}

function readWorkIdentity(block) {
  const id = parseStringProperty(block, "id");
  let channel = parseStringProperty(block, "channel");
  if (!channel) {
    const media = extractTupleMedia(block, "media") || extractObjectMedia(block);
    const src = media?.mediaList.find((item) => item.src)?.src || "";
    channel = src.split("/")[0] || "";
  }
  return { id, channel };
}

function expandObjectRemoval(text, from, to) {
  let end = to;
  while (end < text.length && /\s/.test(text[end])) end += 1;
  if (text[end] === ",") return { from, to: end + 1 };
  let start = from;
  while (start > 0 && /\s/.test(text[start - 1])) start -= 1;
  if (start > 0 && text[start - 1] === ",") return { from: start - 1, to };
  return { from, to };
}

function removeWithdrawObjectsAtMarker(text, marker, withdrawList) {
  if (!text.includes(marker)) return { text, changeList: [] };
  if (marker === "const registeredWorks" && text.includes("export const registeredWorks")) {
    const exportIndex = text.indexOf("export const registeredWorks");
    const bareIndex = text.indexOf("const registeredWorks");
    if (exportIndex >= 0 && bareIndex === exportIndex + "export ".length
      && text.indexOf("const registeredWorks", bareIndex + 1) < 0) {
      return { text, changeList: [] };
    }
  }
  const assigned = findAssignedBlock(text, marker, "[", "]");
  if (!assigned) return { text, changeList: [] };
  const blockList = splitTopLevelObjects(assigned.block);
  const changeList = [];
  let nextArray = assigned.block;
  for (const item of [...blockList].reverse()) {
    const identity = readWorkIdentity(item.block);
    if (!identity.id || !matchesWithdraw(withdrawList, identity.channel, identity.id)) continue;
    const removed = collectKeysFromWorkBlock(item.block);
    const cut = expandObjectRemoval(nextArray, item.from, item.to);
    nextArray = nextArray.slice(0, cut.from) + nextArray.slice(cut.to);
    changeList.unshift({
      workId: identity.id,
      channel: identity.channel,
      kindBefore: "work-record",
      kindAfter: "deleted",
      removed,
      added: [],
      before: item.block.trim(),
      after: "",
    });
  }
  if (changeList.length === 0) return { text, changeList: [] };
  return {
    text: text.slice(0, assigned.from) + nextArray + text.slice(assigned.to),
    changeList,
  };
}

function applyWorkWithdraw(text, withdrawList) {
  const markerList = [
    "export const placeholderWorks",
    "export const initialWorkProjects",
    "const lineSimulationWorks",
    "export const registeredWorks",
    "const registeredWorks",
  ];
  let next = text;
  const changeList = [];
  for (const marker of markerList) {
    const removed = removeWithdrawObjectsAtMarker(next, marker, withdrawList);
    next = removed.text;
    changeList.push(...removed.changeList);
  }
  return { text: next, changeList };
}

function patchJsonWithdrawWorks(catalog, withdrawList) {
  const changeList = [];
  const kept = [];
  for (const work of catalog.works || []) {
    const channel = work.channel || "";
    const id = work.id || "";
    if (!matchesWithdraw(withdrawList, channel, id)) {
      kept.push(work);
      continue;
    }
    changeList.push({
      workId: id,
      channel,
      kindBefore: "work-record",
      kindAfter: "deleted",
      removed: collectKeysFromMediaList(work.media || []),
      added: [],
      before: JSON.stringify(work),
      after: "",
    });
  }
  catalog.works = kept;
  return changeList;
}

function assertWithdrawWorksFound(withdrawList, changeList) {
  const missing = withdrawList.filter((item) => !changeList.some((change) =>
    change.kindAfter === "deleted"
    && change.workId === item.workId
    && (change.channel || "") === item.channel));
  if (missing.length > 0) {
    throw new Error(`找不到已入编作品：${missing.map((item) => `${item.channel}/${item.workId}`).join("、")}`);
  }
}

function collectPhotoExifPairs(intent) {
  const pairList = [];
  const seen = new Set();
  for (const item of intent.items || []) {
    if (item.intent !== "stage.ingest") continue;
    if (!PHOTO_EXIF_CHANNELS.has(item.channel)) continue;
    const objectKey = toRel(item.object);
    const source = toRel(item.sourceStageRel);
    if (!objectKey || !source || seen.has(objectKey)) continue;
    seen.add(objectKey);
    pairList.push({ object: objectKey, sourceStageRel: source });
  }
  return pairList;
}

function resolvePillowPython() {
  const venv = path.resolve(SCRIPT_DIR, "..", "..", "PDF转JPG_施工图用", ".venv", "Scripts", "python.exe");
  const candidateList = [];
  if (existsSync(venv)) candidateList.push({ cmd: venv, args: [] });
  candidateList.push({ cmd: "py", args: ["-3"] }, { cmd: "python", args: [] });
  for (const candidate of candidateList) {
    const probe = spawnSync(candidate.cmd, [...candidate.args, "-c", "import PIL"], {
      encoding: "utf8",
      windowsHide: true,
    });
    if (probe.status === 0) return candidate;
  }
  return null;
}

/** 摄影上页按正式对象键把曝光参数与拍摄时间写入 photoExif.ts。原片缺失时抛出，内容补丁本身不回滚。 */
async function upsertPhotoExif(profile, root, intent) {
  const pairList = collectPhotoExifPairs(intent);
  if (pairList.length === 0) return;
  const scriptPath = path.resolve(SCRIPT_DIR, "..", "..", "站点媒体生命周期", "extract-photo-exif.py");
  if (!existsSync(scriptPath)) {
    throw new Error("摄影参数未写入：找不到 extract-photo-exif.py。");
  }
  const python = resolvePillowPython();
  if (!python) {
    throw new Error("摄影参数未写入：找不到已安装 Pillow 的 Python。");
  }
  const stageRoot = resolveUnderRoot(root, profile.stageRoot || "作品中转站");
  const outPath = resolveUnderRoot(root, "PersonalSite/src/content/photoExif.ts");
  const tempDir = await mkdtemp(path.join(tmpdir(), "photo-exif-"));
  const pairPath = path.join(tempDir, "pairs.json");
  await writeFile(pairPath, JSON.stringify(pairList), "utf8");
  try {
    const result = spawnSync(
      python.cmd,
      [
        ...python.args,
        "-u",
        scriptPath,
        "--pairs",
        pairPath,
        "--stage-root",
        stageRoot,
        "--out",
        outPath,
        "--merge",
        "--apply",
      ],
      {
        encoding: "utf8",
        windowsHide: true,
        timeout: 180_000,
        env: { ...process.env, PYTHONIOENCODING: "utf-8", PYTHONUNBUFFERED: "1" },
      },
    );
    const log = `${result.stdout || ""}${result.stderr || ""}`.trim();
    if (result.status !== 0) {
      throw new Error(log ? `摄影参数未写入：${log}` : "摄影参数未写入。");
    }
  } finally {
    await rm(tempDir, { recursive: true, force: true });
  }
}

const SATELLITE_CONTENT_NAMES = new Set(["photoExif.ts", "photoSizes.ts"]);
const CONTENT_SCAN_EXT = new Set([".ts", ".tsx", ".js", ".jsx", ".json"]);

function isSatelliteContentFile(rel) {
  return SATELLITE_CONTENT_NAMES.has(path.posix.basename(String(rel || "").replace(/\\/g, "/")));
}

function resolveScanRoot(profile, root, catalogPath) {
  const ledgerRel = profile.ledgerPath || "";
  if (ledgerRel) {
    const ledgerDir = path.dirname(resolveUnderRoot(root, ledgerRel));
    const srcDir = path.dirname(ledgerDir);
    if (srcDir && srcDir !== ledgerDir) return srcDir;
  }
  return path.dirname(catalogPath);
}

async function collectLiteralContentHits(contentRoot, objectKeys) {
  const hits = [];
  const keyList = [...objectKeys];
  if (keyList.length === 0) return hits;
  async function walk(current) {
    let entries = [];
    try {
      entries = await readdir(current, { withFileTypes: true });
    } catch (error) {
      if (error && error.code === "ENOENT") return;
      throw error;
    }
    for (const entry of entries) {
      const abs = path.join(current, entry.name);
      if (entry.isDirectory()) {
        if (entry.name === "node_modules" || entry.name === "dist") continue;
        await walk(abs);
        continue;
      }
      if (entry.name === "media-ledger.json") continue;
      if (!CONTENT_SCAN_EXT.has(path.extname(entry.name).toLowerCase())) continue;
      const text = stripBom(await readFile(abs, "utf8"));
      const rel = path.relative(contentRoot, abs).split(path.sep).join("/");
      const lines = text.split(/\r?\n/);
      lines.forEach((line, index) => {
        for (const key of keyList) {
          if (line.includes(key)) {
            hits.push({ object: key, file: rel, line: index + 1 });
          }
        }
      });
    }
  }
  await walk(contentRoot);
  return hits;
}

async function readSatelliteTextMap(dir) {
  const map = new Map();
  for (const name of SATELLITE_CONTENT_NAMES) {
    try {
      map.set(name, stripBom(await readFile(path.join(dir, name), "utf8")));
    } catch (error) {
      if (error && error.code === "ENOENT") continue;
      throw error;
    }
  }
  return map;
}

function satelliteTextHasKey(textMap, objectKey) {
  const needle = `${JSON.stringify(objectKey)}:`;
  for (const text of textMap.values()) {
    if (text.includes(needle)) return true;
  }
  return false;
}

function formatBlockingHits(hitList) {
  const lines = [];
  const seen = new Set();
  for (const hit of hitList) {
    const line = `- ${hit.object}（${hit.file}:${hit.line}）`;
    if (seen.has(line)) continue;
    seen.add(line);
    lines.push(line);
  }
  return lines.join("\n");
}

async function tidySatellitesMain(flags) {
  if (!flags.profile || !flags.keys) {
    throw new Error("核对撤下引用需要 --profile 与 --keys。");
  }
  if (flags.apply === flags.dryRun) {
    throw new Error("必须且只能指定 --dry-run 或 --apply 之一。");
  }

  const { profile, root } = await loadProfile(flags.profile);
  const raw = JSON.parse(stripBom(await readFile(path.resolve(flags.keys), "utf8")));
  if (!Array.isArray(raw)) {
    throw new Error("--keys 须是对象键数组。");
  }
  const objectKeys = [...new Set(raw.map((key) => toRel(key)).filter(Boolean))];
  const kind = (profile.siteCatalog?.kind || "json").toLowerCase();
  const catalogRel = profile.siteCatalog?.path || "content/catalog.json";
  const catalogPath = resolveUnderRoot(root, catalogRel);
  const satelliteDir = path.dirname(catalogPath);
  const scanRoot = resolveScanRoot(profile, root, catalogPath);
  if (kind !== "personalworks-ts") {
    printReport({ ok: true, stripped: [], changes: [] });
    return;
  }

  const hits = await collectLiteralContentHits(scanRoot, objectKeys);
  const blocking = [];
  const satelliteOnly = [];
  for (const key of objectKeys) {
    const bodyHits = hits.filter((hit) => hit.object === key && !isSatelliteContentFile(hit.file));
    if (bodyHits.length > 0) blocking.push(...bodyHits);
    else satelliteOnly.push(key);
  }

  const beforeMap = await readSatelliteTextMap(satelliteDir);
  const stripped = satelliteOnly.filter((key) => satelliteTextHasKey(beforeMap, key));
  const satelliteList = stripped.length > 0
    ? await stripSatelliteMaps(satelliteDir, stripped)
    : [];
  if (flags.apply && satelliteList.length > 0) {
    await writeAllAtomically(satelliteList, flags.failAfterWrite);
  }

  if (blocking.length > 0) {
    const cleaned = flags.apply && satelliteList.length > 0
      ? "\n只出现在尺寸表或 Exif 中的旧键已从表里去掉。"
      : "";
    printReport({
      ok: false,
      error: `作品正文仍引用待撤对象，发布停在本步，正式文件仍保留：\n${formatBlockingHits(blocking)}${cleaned}`,
      stripped,
      catalogPath,
      changes: [],
    });
    process.exitCode = 1;
    return;
  }

  printReport({
    ok: true,
    dryRun: flags.dryRun,
    stripped,
    catalogPath,
    catalogPaths: satelliteList.map((item) => item.targetPath),
    changes: [],
  });
}

async function main() {
  const flags = parseArgs(process.argv);
  if (flags.tidySatellites) {
    if (flags.help) {
      console.log("用法：node content-patch.mjs --tidy-satellites --profile <profile.json> --keys <keys.json> [--dry-run|--apply]");
      return;
    }
    await tidySatellitesMain(flags);
    return;
  }
  if (flags.help || !flags.intent || !flags.profile) {
    console.log("用法：node content-patch.mjs --intent <file> --profile <profile.json> [--dry-run|--apply]");
    process.exit(flags.help ? 0 : 2);
  }
  if (flags.apply === flags.dryRun) {
    throw new Error("必须且只能指定 --dry-run 或 --apply 之一。");
  }

  const intent = await loadIntent(flags.intent);
  const { profile, root } = await loadProfile(flags.profile);
  const { hideMap, withdrawSet } = collectHideMap(intent);
  withdrawObjectSet = withdrawSet;
  const ingestMap = collectIngestMap(intent);
  const registerList = collectRegisterList(intent);
  const updateList = collectUpdateList(intent);
  const copyList = collectCopyList(intent);
  const starList = collectStarList(intent);
  const tagList = collectTagList(intent);
  const withdrawWorkList = collectWithdrawWorkList(intent);
  collectReorderMap(intent);
  const ledgerDict = await loadLedgerDict(profile, root);
  photoStemMap = collectPhotoStemMap(intent, ledgerDict);
  collectSortKeyMap(intent, ledgerDict);
  if (hideMap.size === 0 && ingestMap.size === 0 && registerList.length === 0 && updateList.length === 0 && copyList.length === 0 && tagList.length === 0 && starList.length === 0 && reorderMap.size === 0 && withdrawWorkList.length === 0) {
    printReport({ ok: true, changes: [], catalogPath: "" });
    return;
  }

  const kind = (profile.siteCatalog?.kind || "json").toLowerCase();
  const catalogRel = profile.siteCatalog?.path || "content/catalog.json";
  const catalogPath = resolveUnderRoot(root, catalogRel);
  const original = stripBom(await readFile(catalogPath, "utf8"));
  const siteRel = profile.siteCatalog?.sitePath || "";
  const sitePath = siteRel ? resolveUnderRoot(root, siteRel) : "";
  const siteOriginal = sitePath ? stripBom(await readFile(sitePath, "utf8")) : "";
  const workDataRel = profile.siteCatalog?.workDataPath || "";
  const workDataPath = workDataRel ? resolveUnderRoot(root, workDataRel) : "";
  const workDataOriginal = workDataPath ? stripBom(await readFile(workDataPath, "utf8")) : "";
  const gameDataRel = profile.siteCatalog?.gameDataPath || "";
  const gameDataPath = gameDataRel ? resolveUnderRoot(root, gameDataRel) : "";
  const gameDataOriginal = gameDataPath ? stripBom(await readFile(gameDataPath, "utf8")) : "";
  const lexiconPath = resolveUnderRoot(root, "PersonalSite/src/content/lexicon.ts");
  let lexiconText = "";
  try {
    lexiconText = stripBom(await readFile(lexiconPath, "utf8"));
  } catch {
    lexiconText = "";
  }

  let changeList = [];
  let nextText = original;
  let siteNextText = siteOriginal;
  let workDataNextText = workDataOriginal;
  let gameDataNextText = gameDataOriginal;
  let allWorks = [];
  if (kind === "personalworks-ts") {
    if (original.includes("export const placeholderWorks")) {
      const patched = patchWorksTs(original, hideMap, ingestMap);
      changeList = patched.changeList;
      nextText = patched.text;
      allWorks = collectWorksFromTs(nextText);
    } else {
      const linePatched = patchInlineWorks(original, hideMap, ingestMap, "const lineSimulationWorks");
      const registeredMarker = original.includes("export const registeredWorks")
        ? "export const registeredWorks"
        : original.includes("const registeredWorks")
          ? "const registeredWorks"
          : "";
      const registeredPatched = patchInlineWorks(
        linePatched.text,
        hideMap,
        ingestMap,
        registeredMarker,
      );
      changeList = [...linePatched.changeList, ...registeredPatched.changeList];
      nextText = registeredPatched.text;
      allWorks = [
        ...collectInlineWorks(nextText, "const lineSimulationWorks"),
        ...collectInlineWorks(nextText, registeredMarker),
      ];
    }
    if (workDataPath) {
      const workDataPatched = patchTupleCatalog(
        workDataOriginal,
        "export const initialWorkProjects",
        "media",
        "",
        hideMap,
        ingestMap,
      );
      workDataNextText = workDataPatched.text;
      changeList.push(...workDataPatched.changeList);
      allWorks.push(...collectTupleWorks(
        workDataNextText,
        "export const initialWorkProjects",
        "media",
        "",
      ));
    }
    if (sitePath) {
      const sitePatched = patchSiteTs(siteOriginal, hideMap, ingestMap);
      siteNextText = sitePatched.text;
      changeList.push(...sitePatched.changeList);
      allWorks.push(...collectSiteWorks(siteNextText));
    }
    if (gameDataPath) {
      const gameDataPatched = patchTupleCatalog(
        gameDataOriginal,
        "export const initialGameProjects",
        "screenshots",
        "game-dev",
        hideMap,
        ingestMap,
      );
      gameDataNextText = gameDataPatched.text;
      changeList.push(...gameDataPatched.changeList);
      allWorks.push(...collectTupleWorks(
        gameDataNextText,
        "export const initialGameProjects",
        "screenshots",
        "game-dev",
      ));
      const registeredPatched = patchTupleCatalogIfPresent(
        gameDataNextText,
        ["export const registeredGames", "const registeredGames"],
        "screenshots",
        "game-dev",
        hideMap,
        ingestMap,
      );
      gameDataNextText = registeredPatched.text;
      changeList.push(...registeredPatched.changeList);
      allWorks.push(...collectTupleWorks(
        gameDataNextText,
        "export const registeredGames",
        "screenshots",
        "game-dev",
      ));
      allWorks.push(...collectTupleWorks(
        gameDataNextText,
        "const registeredGames",
        "screenshots",
        "game-dev",
      ));
    }
    if (registerList.length > 0) {
      const existingList = [...allWorks, ...collectWorkKeysFromTs(nextText)];
      for (const item of registerList) {
        const workId = item.workId || "";
        const channel = item.channel || "";
        const exists = existingList.some((work) => work.id === workId && (work.channel || "") === channel);
        if (exists) {
          throw new Error(`该栏目已有同 id 作品：${channel}/${workId}`);
        }
      }
      const poolList = registerList.filter((item) => item.channel !== "profile" && item.channel !== "game-dev");
      const profileList = registerList.filter((item) => item.channel === "profile");
      const gameList = registerList.filter((item) => item.channel === "game-dev");
      if (poolList.length > 0) {
        const registered = patchTsRegister(nextText, poolList);
        nextText = registered.text;
        changeList.push(...registered.changeList);
      }
      if (profileList.length > 0) {
        if (!sitePath) {
          throw new Error("形象登记须配置 sitePath。");
        }
        const registered = patchNamedRegister(
          siteNextText,
          profileList,
          ["export const registeredProfileSessions", "const registeredProfileSessions"],
          formatProfileSession,
          "site-profile-session",
        );
        siteNextText = registered.text;
        changeList.push(...registered.changeList);
      }
      if (gameList.length > 0) {
        if (!gameDataPath) {
          throw new Error("游戏登记须配置 gameDataPath。");
        }
        const registered = patchNamedRegister(
          gameDataNextText,
          gameList,
          ["export const registeredGames", "const registeredGames"],
          formatGameShell,
          "site-game-shell",
        );
        gameDataNextText = registered.text;
        changeList.push(...registered.changeList);
      }
    }
    if (updateList.length > 0) {
      if (workDataPath) {
        const updated = patchTextWorkUpdate(workDataNextText, updateList);
        workDataNextText = updated.text;
        changeList.push(...updated.changeList);
      }
      const worksUpdated = patchTextWorkUpdate(nextText, updateList);
      nextText = worksUpdated.text;
      changeList.push(...worksUpdated.changeList);
      if (sitePath) {
        const siteUpdated = patchTextWorkUpdate(siteNextText, updateList.filter((item) => item.channel === "profile"), {
          titleOnly: true,
        });
        siteNextText = siteUpdated.text;
        changeList.push(...siteUpdated.changeList);
      }
      if (gameDataPath) {
        const gameUpdated = patchTextWorkUpdate(gameDataNextText, updateList.filter((item) => item.channel === "game-dev"), {
          titleOnly: true,
        });
        gameDataNextText = gameUpdated.text;
        changeList.push(...gameUpdated.changeList);
      }
      for (const item of updateList) {
        const applied = changeList.some((change) =>
          change.kindAfter === "meta" && change.workId === item.workId);
        if (!applied) {
          throw new Error(`找不到已入编作品：${item.channel}/${item.workId}`);
        }
      }
    }
    if (copyList.length > 0) {
      if (workDataPath) {
        const copied = patchTextCopy(workDataNextText, copyList, { channelField: "summary" });
        workDataNextText = copied.text;
        changeList.push(...copied.changeList);
      }
      const worksCopied = patchTextCopy(nextText, copyList, { channelField: "summary" });
      nextText = worksCopied.text;
      changeList.push(...worksCopied.changeList);
      if (sitePath) {
        const siteCopied = patchTextCopy(siteNextText, copyList, { lexiconText });
        siteNextText = siteCopied.text;
        changeList.push(...siteCopied.changeList);
      }
      if (gameDataPath) {
        const gameCopied = patchTextCopy(gameDataNextText, copyList.filter((item) => item.channel === "game-dev" || item.target === "media"), {
          channelField: "lead",
          // 游戏项目对象不写 channel 字段，按 id 配对。
          matchChannel: false,
        });
        gameDataNextText = gameCopied.text;
        changeList.push(...gameCopied.changeList);
      }
    }
    if (starList.length > 0) {
      const poolStarList = starList.filter((item) => item.channel !== "profile");
      if (workDataPath) {
        const stared = patchTextStars(workDataNextText, poolStarList);
        workDataNextText = stared.text;
        changeList.push(...stared.changeList);
      }
      const worksStared = patchTextStars(nextText, poolStarList);
      nextText = worksStared.text;
      changeList.push(...worksStared.changeList);
      if (gameDataPath) {
        const gameStared = patchTextStars(
          gameDataNextText,
          poolStarList
            .filter((item) => item.channel === "game-dev")
            .map((item) => ({ ...item, channel: "" })),
        );
        gameDataNextText = gameStared.text;
        changeList.push(...gameStared.changeList);
      }
      if (sitePath) {
        const profileStared = patchProfileStars(siteNextText, starList);
        siteNextText = profileStared.text;
        changeList.push(...profileStared.changeList);
      }
      for (const item of starList) {
        const applied = changeList.some((change) =>
          change.kindAfter === "stars" && change.workId === item.workId && change.before === String(item.object));
        if (!applied) {
          throw new Error(`找不到已入编作品：${item.channel}/${item.workId}`);
        }
      }
    }
    if (tagList.length > 0) {
      if (workDataPath) {
        const tagged = patchTextTags(workDataNextText, tagList);
        workDataNextText = tagged.text;
        changeList.push(...tagged.changeList);
      }
      const worksTagged = patchTextTags(nextText, tagList);
      nextText = worksTagged.text;
      changeList.push(...worksTagged.changeList);
      for (const item of tagList) {
        const applied = changeList.some((change) =>
          change.kindAfter === "tags" && change.workId === item.workId);
        if (!applied) {
          throw new Error(`找不到已入编作品：${item.channel}/${item.workId}`);
        }
      }
    }
  } else {
    const catalog = JSON.parse(original);
    changeList = patchJsonCatalog(catalog, hideMap, ingestMap);
    changeList.push(...patchJsonRegister(catalog, registerList));
    changeList.push(...patchJsonUpdate(catalog, updateList));
    changeList.push(...patchJsonCopy(catalog, copyList));
    changeList.push(...patchJsonStars(catalog, starList));
    changeList.push(...patchJsonTags(catalog, tagList));
    nextText = `${JSON.stringify(catalog, null, 2)}\n`;
    allWorks = catalog.works || [];
  }
  if (withdrawWorkList.length > 0) {
    for (const item of withdrawWorkList) {
      if (!item.channel || !item.workId) {
        throw new Error("整项撤下须指定栏目与作品。");
      }
    }
    if (kind === "personalworks-ts") {
      const worksRemoved = applyWorkWithdraw(nextText, withdrawWorkList);
      nextText = worksRemoved.text;
      changeList.push(...worksRemoved.changeList);
      if (workDataPath) {
        const dataRemoved = applyWorkWithdraw(workDataNextText, withdrawWorkList);
        workDataNextText = dataRemoved.text;
        changeList.push(...dataRemoved.changeList);
      }
      allWorks = allWorks.filter((work) => !matchesWithdraw(withdrawWorkList, work.channel || "", work.id));
    } else {
      const catalog = JSON.parse(nextText);
      changeList.push(...patchJsonWithdrawWorks(catalog, withdrawWorkList));
      nextText = `${JSON.stringify(catalog, null, 2)}\n`;
      allWorks = catalog.works || [];
    }
    assertWithdrawWorksFound(withdrawWorkList, changeList);
    for (const change of changeList) {
      if (change.kindAfter !== "deleted") continue;
      for (const key of change.removed || []) withdrawSet.add(toRel(key));
    }
    for (const item of withdrawWorkList) {
      for (const key of item.objectList || []) withdrawSet.add(key);
    }
  }
  assertWithdrawAfterPatch(allWorks, withdrawSet);

  let satelliteList = [];
  if (kind === "personalworks-ts" && withdrawSet.size > 0) {
    const satelliteDir = path.dirname(workDataPath || catalogPath);
    satelliteList = await stripSatelliteMaps(satelliteDir, withdrawSet);
  }

  if (flags.dryRun) {
    printReport({
      ok: true,
      dryRun: true,
      catalogPath,
      catalogPaths: [
        catalogPath,
        ...(sitePath ? [sitePath] : []),
        ...(workDataPath ? [workDataPath] : []),
        ...(gameDataPath ? [gameDataPath] : []),
        ...satelliteList.map((item) => item.targetPath),
      ],
      changes: changeList,
    });
    return;
  }

  if (changeList.length === 0) {
    if (satelliteList.length > 0) {
      try {
        await writeAllAtomically(satelliteList, flags.failAfterWrite);
        await upsertPhotoExif(profile, root, intent);
        printReport({
          ok: true,
          catalogPath,
          catalogPaths: satelliteList.map((item) => item.targetPath),
          changes: [],
        });
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        printReport({
          ok: false,
          error: message,
          catalogPath,
          changes: [],
          restored: message.includes("已还原"),
        });
        process.exitCode = 1;
      }
      return;
    }
    await upsertPhotoExif(profile, root, intent);
    printReport({ ok: true, catalogPath, changes: [] });
    return;
  }

  try {
    const fileList = [];
    if (nextText !== original) fileList.push({ targetPath: catalogPath, nextText });
    if (sitePath && siteNextText !== siteOriginal) {
      fileList.push({ targetPath: sitePath, nextText: siteNextText });
    }
    if (workDataPath && workDataNextText !== workDataOriginal) {
      fileList.push({ targetPath: workDataPath, nextText: workDataNextText });
    }
    if (gameDataPath && gameDataNextText !== gameDataOriginal) {
      fileList.push({ targetPath: gameDataPath, nextText: gameDataNextText });
    }
    fileList.push(...satelliteList);
    await writeAllAtomically(fileList, flags.failAfterWrite);
    await upsertPhotoExif(profile, root, intent);
    printReport({
      ok: true,
      catalogPath,
      catalogPaths: fileList.map((item) => item.targetPath),
      changes: changeList,
      restored: false,
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    printReport({
      ok: false,
      error: message,
      catalogPath,
      changes: changeList,
      restored: message.includes("已还原"),
    });
    process.exitCode = 1;
  }
}

function printReport(report) {
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

try {
  await main();
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  printReport({ ok: false, error: message, changes: [] });
  process.exitCode = 1;
}
