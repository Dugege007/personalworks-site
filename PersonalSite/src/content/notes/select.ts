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

export type NoteRecord = {
  folder: string;
  slug: string;
  title: string;
  date: string;
  summary: string;
  draft?: boolean;
  hidden?: boolean;
  previewObjects?: string[];
  bodyMeta?: NoteResource;
  images?: NoteImageRecord[];
};

export type NoteIndex = {
  version?: number;
  notes?: NoteRecord[];
};

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
 * 存储日期 `YYYY-MM-DD` 显示为间隔点。
 */
export function displayNoteDate(isoDate: string): string {
  return isoDate.replaceAll("-", ".");
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
