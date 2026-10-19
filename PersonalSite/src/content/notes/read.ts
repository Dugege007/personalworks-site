import indexJson from "./index.json";
import { lexicon } from "../lexicon";
import type { NoteCard } from "../site";
import { stripNoteFrontmatter, toCard, visibleNotes, type NoteIndex } from "./select";

const bodies = import.meta.glob("./*.md", {
  query: "?raw",
  import: "default",
  eager: true,
}) as Record<string, string>;

const index = indexJson as NoteIndex;

/**
 * 按 slug 取已发布正文。草稿、隐藏和缺文件都视为没有这篇。
 */
export function loadNote(slug: string | undefined): NoteCard | null {
  if (!slug) {
    return null;
  }
  const record = visibleNotes(index).find((item) => item.slug === slug);
  if (!record) {
    return null;
  }
  const markdown = bodies[`./${record.slug}.md`];
  if (typeof markdown !== "string") {
    return null;
  }
  return toCard(record, stripNoteFrontmatter(markdown));
}

/**
 * 列表、简历笔记栏和显影摘要共用。不读索引文件路径以外的来源。
 */
export function listNotes(): NoteCard[] {
  return visibleNotes(index).map((record) => toCard(record, ""));
}

export function noteTheme(slug: string | undefined): string {
  return slug === lexicon.sketching.key ? lexicon.sketching.key : lexicon.notes.key;
}
