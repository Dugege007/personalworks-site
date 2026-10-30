import type { NoteCard } from "../content/site";

/** 置顶文之外，再列日期最新的篇数。与简历页、打印稿共用。 */
export const RESUME_LATEST_NOTE_COUNT = 2;

/** 简历笔记栏置顶的站内文：PLS_RTE 产线仿真平台。 */
export const RESUME_PINNED_NOTE_SLUG = "n-20261027-010100";

/**
 * 简历笔记栏：置顶一篇，再接最新若干篇。草稿不进入。
 */
export function selectResumeNotes(notes: NoteCard[]): NoteCard[] {
  const published = notes.filter((note) => !note.draft);
  const pinned = published.find((note) => note.slug === RESUME_PINNED_NOTE_SLUG);
  const latest = published
    .filter((note) => note.slug !== RESUME_PINNED_NOTE_SLUG)
    .slice(0, RESUME_LATEST_NOTE_COUNT);
  return [...(pinned ? [pinned] : []), ...latest];
}

/**
 * 简历笔记栏只出主标题。缺主标题时回退全文标题。
 */
export function resumeNoteLabel(note: NoteCard): string {
  const main = note.mainTitle?.trim();
  return main || note.title;
}
