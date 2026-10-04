import type { Locale } from "../prefs/types.ts";
import { MD_ENTRY, MD_SECTION, parseFields, splitByHeading } from "./pageMd.ts";

export type TipPart =
  | { kind: "text"; text: string }
  | { kind: "bold"; text: string }
  | { kind: "link"; text: string; href: string };

export type GlossaryEntry = {
  term: string;
  image?: string;
  zh: TipPart[];
  en?: TipPart[];
};

export type GlossaryKey = {
  text: string;
  entry: GlossaryEntry;
};

export type GlossaryBook = {
  keys: GlossaryKey[];
};

export type TextPiece =
  | { kind: "text"; text: string }
  | { kind: "term"; text: string; entry: GlossaryEntry };

const TIP_CONTINUATION = ["解释", "英文解释"] as const;
const ALIAS_SPLIT = /[、,，]/;
const ASCII_KEY = /^[A-Za-z0-9]+$/;
const ASCII_CHAR = /[A-Za-z0-9]/;
const IMAGE_FILE = /^[^\\/]+\.(webp|png|jpe?g|gif|svg)$/i;
const LINK_MARK = /\[([^\]\n]+)\]\(([^)\s]+)\)/;
const BOLD_MARK = /\*\*([^*]+)\*\*/;

/**
 * 读名词文稿。每个 `##` 只作分组，分组名不参与匹配。缺解释、短于 2 字、重复匹配词按契约丢弃。
 */
export function parseGlossary(source: string): GlossaryBook {
  const sections = splitByHeading(source, MD_SECTION);
  const keys: GlossaryKey[] = [];
  const seen = new Set<string>();

  for (const section of sections) {
    for (const block of splitByHeading(section.body, MD_ENTRY)) {
      const term = block.title.trim();
      const { fields } = parseFields(block.body, TIP_CONTINUATION);
      const zh = parseTip(fields["解释"] ?? "");
      if (!tipHasText(zh)) {
        continue;
      }
      const enSource = (fields["英文解释"] ?? "").trim();
      const entry: GlossaryEntry = {
        term,
        image: imageFile(fields["图片"]),
        zh,
        en: enSource ? parseTip(enSource) : undefined,
      };
      for (const alias of matchKeys(term, fields["别名"])) {
        if (seen.has(alias)) {
          continue;
        }
        seen.add(alias);
        keys.push({ text: alias, entry });
      }
    }
  }

  keys.sort((a, b) => b.text.length - a.text.length);
  return { keys };
}

/**
 * 按名称整词查找名词。名称按传入顺序尝试，命中即返回该条。
 */
export function glossaryEntryFor(book: GlossaryBook, names: readonly string[]): GlossaryEntry | undefined {
  for (const name of names) {
    const trimmed = name.trim();
    if (trimmed.length < 2) {
      continue;
    }
    const hit = book.keys.find((key) => key.text === trimmed);
    if (hit) {
      return hit.entry;
    }
  }
  return undefined;
}

/**
 * 按当前语言取提示正文。英文未写时回退中文。
 */
export function tipParts(entry: GlossaryEntry, locale: Locale): TipPart[] {
  if (locale === "en" && entry.en && tipHasText(entry.en)) {
    return entry.en;
  }
  return entry.zh;
}

/**
 * 把正文拆成普通文字与名词。同一位置只取最长匹配。
 */
export function annotateTerms(text: string, book: GlossaryBook): TextPiece[] {
  const pieces: TextPiece[] = [];
  let index = 0;
  let plainStart = 0;

  const flush = (end: number) => {
    if (end > plainStart) {
      pieces.push({ kind: "text", text: text.slice(plainStart, end) });
    }
  };

  while (index < text.length) {
    const hit = book.keys.find(
      (key) => text.startsWith(key.text, index) && boundaryOk(text, index, key.text),
    );
    if (hit) {
      flush(index);
      pieces.push({ kind: "term", text: text.slice(index, index + hit.text.length), entry: hit.entry });
      index += hit.text.length;
      plainStart = index;
      continue;
    }
    index += 1;
  }
  flush(text.length);
  return pieces;
}

function matchKeys(term: string, aliases: string | undefined): string[] {
  const list = [term, ...(aliases ?? "").split(ALIAS_SPLIT)];
  return list.map((item) => item.trim()).filter((item) => item.length >= 2);
}

function imageFile(value: string | undefined): string | undefined {
  const name = value?.trim() ?? "";
  if (!name || name.includes("..") || !IMAGE_FILE.test(name)) {
    return undefined;
  }
  return name;
}

function tipHasText(parts: TipPart[]): boolean {
  return parts.some((part) => part.text.trim().length > 0);
}

function parseTip(source: string): TipPart[] {
  const parts: TipPart[] = [];
  let cursor = 0;

  while (cursor < source.length) {
    const rest = source.slice(cursor);
    const link = LINK_MARK.exec(rest);
    const bold = BOLD_MARK.exec(rest);
    const linkAt = link?.index ?? -1;
    const boldAt = bold?.index ?? -1;
    const useLink = link && (boldAt < 0 || linkAt <= boldAt);
    const useBold = !useLink && bold && boldAt >= 0;
    if (!useLink && !useBold) {
      parts.push({ kind: "text", text: rest });
      break;
    }
    const at = useLink ? linkAt : boldAt;
    const mark = useLink ? link : bold;
    if (!mark || at < 0) {
      break;
    }
    if (at > 0) {
      parts.push({ kind: "text", text: rest.slice(0, at) });
    }
    if (useLink && link) {
      const href = (link[2] ?? "").trim();
      const label = link[1] ?? "";
      if (isSafeHref(href) && label.trim()) {
        parts.push({ kind: "link", text: label, href });
      } else {
        parts.push({ kind: "text", text: link[0] });
      }
    } else if (bold) {
      parts.push({ kind: "bold", text: bold[1] ?? "" });
    }
    cursor += at + mark[0].length;
  }

  return parts;
}

function isSafeHref(href: string): boolean {
  if (href.startsWith("/") && !href.startsWith("//")) {
    return true;
  }
  return href.startsWith("https://") || href.startsWith("http://");
}

function boundaryOk(text: string, index: number, key: string): boolean {
  if (!ASCII_KEY.test(key)) {
    return true;
  }
  const before = index > 0 ? text[index - 1] : "";
  const after = text[index + key.length] ?? "";
  if (before && ASCII_CHAR.test(before)) {
    return false;
  }
  if (after && ASCII_CHAR.test(after)) {
    return false;
  }
  return true;
}
