export type NoteResource = {
  stars?: number;
  tags?: string[];
  displayName?: string;
  description?: string;
};

export type NoteImageRecord = NoteResource & {
  stageRel?: string;
  objectKey?: string;
  hidden?: boolean;
};

export type NoteRevision = {
  at: string;
  summary?: string;
  source?: string;
};

export type NoteRecord = {
  folder: string;
  slug: string;
  title: string;
  date: string;
  summary: string;
  mainTitle?: string;
  subtitle?: string;
  createdAt?: string;
  revisedAt?: string;
  revisions?: NoteRevision[];
  draft?: boolean;
  hidden?: boolean;
  previewObjects?: string[];
  /** 发布索引里的正文元数据。属性名与 index.json 的 body 一致。 */
  body?: NoteResource;
  images?: NoteImageRecord[];
};

export type NoteDisplayMeta = {
  mainTitle: string;
  subtitle: string;
  createdAt: string;
  revisedAt: string;
  revisions: Array<{ at: string; summary: string; source: string }>;
};

export type NoteIndex = {
  version?: number;
  notes?: NoteRecord[];
};

/**
 * 正文级自由标签。缺字段、空数组或空白项视为没有标签。不读取配图标签。
 */
export function noteBodyTags(record: NoteRecord): string[] {
  const tags = record.body?.tags;
  if (!tags || tags.length === 0) {
    return [];
  }
  return tags.map((tag) => tag.trim()).filter((tag) => tag.length > 0);
}

/**
 * 访客能看见的心得：去掉草稿和隐藏，日期新的在前。
 */
export function visibleNotes(index: NoteIndex): NoteRecord[] {
  return (index.notes ?? [])
    .filter((note) => note.slug && !note.draft && !note.hidden)
    .slice()
    .sort((a, b) => b.date.localeCompare(a.date));
}

/**
 * 详情页用的主副标题与修订。缺字段时主标题回退为 `title`，没有修订时最近修改与创建时间相同。
 */
export function noteDisplayMeta(record: NoteRecord): NoteDisplayMeta {
  const trimmedMain = record.mainTitle?.trim() ?? "";
  const mainTitle = trimmedMain || record.title;
  const subtitle = record.subtitle?.trim() ?? "";
  const createdAt = record.createdAt?.trim() || (record.date ? `${record.date} 00:00:00` : "");
  const revisions = (record.revisions ?? []).map((item) => ({
    at: item.at,
    summary: item.summary?.trim() ? item.summary : "",
    source: item.source ?? "",
  }));
  const latest = [...revisions].reverse().find((item) => item.at)?.at ?? "";
  const revisedAt = record.revisedAt?.trim() || latest || createdAt;
  return { mainTitle, subtitle, createdAt, revisedAt, revisions };
}

/**
 * 存储日期 `YYYY-MM-DD` 显示为间隔点。
 */
export function displayNoteDate(isoDate: string): string {
  return isoDate.replaceAll("-", ".");
}

/**
 * 创建与修订时间显示为间隔点。未知钟点回退成的 `00:00:00` 只保留日期，其余保留到秒。
 */
export function displayNoteTimestamp(value: string): string {
  const shown = value.trim().replaceAll("-", ".");
  return shown.replace(/ 00:00:00$/, "");
}

/**
 * 供 `<time dateTime>` 使用的机器可读时间。只有日期时原样返回。
 */
export function noteTimestampDateTime(value: string): string {
  const trimmed = value.trim();
  const matched = /^(\d{4}-\d{2}-\d{2})(?:\s+(\d{2}:\d{2}:\d{2}))?$/.exec(trimmed);
  if (!matched) {
    return trimmed;
  }
  return matched[2] ? `${matched[1]}T${matched[2]}` : matched[1];
}

/**
 * 去掉正文 frontmatter，供渲染。
 */
export function stripNoteFrontmatter(markdown: string): string {
  const text = markdown.replaceAll("\r\n", "\n");
  if (!text.startsWith("---\n")) {
    return markdown;
  }
  const end = text.indexOf("\n---\n", 4);
  if (end < 0) {
    return markdown;
  }
  return text.slice(end + 5).replace(/^\n/, "");
}
