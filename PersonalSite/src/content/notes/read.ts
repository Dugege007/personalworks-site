import indexJson from "./index.json";
import { lexicon } from "../lexicon";
import type { NoteCard } from "../site";
import {
  displayNoteDate,
  stripNoteFrontmatter,
  visibleNotes,
  type NoteIndex,
  type NoteRecord,
} from "./select";

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

function toCard(record: NoteRecord, body: string): NoteCard {
  return {
    slug: record.slug,
    title: record.title,
    date: displayNoteDate(record.date),
    summary: record.summary || "待填写描述",
    body,
    previewImages: (record.previewObjects ?? []).slice(0, 3),
    draft: false,
  };
}

export function noteTheme(slug: string | undefined): string {
  return slug === lexicon.sketching.key ? lexicon.sketching.key : lexicon.notes.key;
}
