/**
 * 网页文稿共用读取。书写约定见 `PersonalSite/文稿/README.md`。
 * 各页只解释本页字段，不在此重复切开与加粗。
 */

export const MD_SECTION = /^##\s+(.+?)\s*$/;
export const MD_ENTRY = /^###\s+(.+?)\s*$/;
export const MD_FIELD = /^([^\s#：:]{1,24})[：:]\s*(.*)$/;

/** 下一行不是「键：值」时接到该字段后面。 */
export const MD_CONTINUATION_KEYS = ["说明", "英文说明", "描述", "英文描述", "笔记"] as const;

export type MdBlock = {
  title: string;
  body: string;
};

export type MdMark = {
  text: string;
  bold?: boolean;
};

/**
 * 按指定标题正则切开。`#` 下、第一个匹配标题前的说明丢弃。
 */
export function splitByHeading(source: string, pattern: RegExp): MdBlock[] {
  const list: MdBlock[] = [];
  let title = "";
  const lines: string[] = [];

  const flush = () => {
    if (!title) {
      lines.length = 0;
      return;
    }
    list.push({ title, body: lines.join("\n").trim() });
    lines.length = 0;
  };

  for (const line of source.replace(/^\uFEFF/, "").split(/\r?\n/)) {
    const match = line.match(pattern);
    if (match?.[1]) {
      flush();
      title = match[1].trim();
      continue;
    }
    lines.push(line);
  }
  flush();
  return list;
}

/**
 * 取同名节正文；未写则空串。
 */
export function sectionBody(sections: MdBlock[], title: string): string {
  return sections.find((item) => item.title === title)?.body ?? "";
}

/**
 * 抽出「键：值」行。不认识的键仍收入字段表，由各页决定用不用。
 */
export function parseFields(
  body: string,
  continuationKeys: readonly string[] = MD_CONTINUATION_KEYS,
): { fields: Record<string, string>; rest: string } {
  const fields: Record<string, string> = {};
  const rest: string[] = [];
  const continuation = new Set(continuationKeys);
  let lastKey = "";

  for (const line of body.split(/\r?\n/)) {
    const match = line.match(MD_FIELD);
    if (match?.[1]) {
      lastKey = match[1];
      fields[lastKey] = (match[2] ?? "").trim();
      continue;
    }
    if (lastKey && continuation.has(lastKey)) {
      const next = line.trim();
      if (next) {
        fields[lastKey] = [fields[lastKey], next].filter(Boolean).join("\n");
      }
      continue;
    }
    rest.push(line);
  }

  return { fields, rest: rest.join("\n").trim() };
}

/**
 * 短列：每行一项，可带减号。
 */
export function parseList(body: string): string[] {
  return body
    .split(/\r?\n/)
    .map((line) => line.replace(/^[-*]\s+/, "").trim())
    .filter(Boolean);
}

/**
 * 取第一段非空正文，忽略无序列表标记。
 */
export function firstParagraph(body: string): string {
  const chunks = body
    .split(/\n\s*\n/)
    .map((chunk) => chunk.replace(/^[-*]\s+/gm, "").trim())
    .filter(Boolean);
  return chunks[0] ?? "";
}

/**
 * 空串视为未写。
 */
export function emptyToUndef(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

/**
 * `是` / `true` / `1` 为开。
 */
export function parseYesFlag(value: string | undefined): boolean {
  const normalized = value?.trim();
  return normalized === "是" || normalized === "true" || normalized === "1";
}

/**
 * 把 `**加粗**` 收成可渲染片段，其它标记仍作原文。
 */
export function parseInlineMarks(source: string): MdMark[] {
  const list: MdMark[] = [];
  const pattern = /\*\*([^*]+)\*\*/g;
  let cursor = 0;
  let match = pattern.exec(source);
  while (match) {
    if (match.index > cursor) {
      list.push({ text: source.slice(cursor, match.index) });
    }
    list.push({ text: match[1] ?? "", bold: true });
    cursor = match.index + match[0].length;
    match = pattern.exec(source);
  }
  if (cursor < source.length) {
    list.push({ text: source.slice(cursor) });
  }
  return list;
}
