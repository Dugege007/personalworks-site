import assert from "node:assert/strict";
import test from "node:test";
import {
  displayNoteDate,
  displayNoteTimestamp,
  noteBodyTags,
  noteDisplayMeta,
  noteTimestampDateTime,
  stripNoteFrontmatter,
  visibleNotes,
} from "../src/content/notes/select.ts";

test("visible notes skip drafts and hidden entries", () => {
  const list = visibleNotes({
    notes: [
      { folder: "a", slug: "old", title: "旧", date: "2026-01-01", summary: "", draft: false },
      { folder: "b", slug: "new", title: "新", date: "2026-10-05", summary: "", draft: false },
      { folder: "c", slug: "hide", title: "藏", date: "2026-10-06", summary: "", hidden: true },
      { folder: "d", slug: "draft", title: "稿", date: "2026-10-07", summary: "", draft: true },
    ],
  });
  assert.deepEqual(
    list.map((item) => item.slug),
    ["new", "old"],
  );
});

test("note meta falls back when titles and revisions are absent", () => {
  const meta = noteDisplayMeta({
    folder: "20261005_021341",
    slug: "n-20261005-021341",
    title: "只有旧标题",
    date: "2026-10-05",
    summary: "",
  });
  assert.equal(meta.mainTitle, "只有旧标题");
  assert.equal(meta.subtitle, "");
  assert.equal(meta.createdAt, "2026-10-05 00:00:00");
  assert.equal(meta.revisedAt, "2026-10-05 00:00:00");
  assert.deepEqual(meta.revisions, []);
});

test("note meta keeps subtitle and treats latest revision as the modified time", () => {
  const meta = noteDisplayMeta({
    folder: "甲——乙",
    slug: "n-20261005-125603",
    title: "甲——乙",
    date: "2026-10-05",
    summary: "",
    mainTitle: "甲",
    subtitle: "乙",
    createdAt: "2026-10-05 12:56:03",
    revisions: [
      { at: "2026-10-06 01:02:03", summary: "", source: "publish" },
      { at: "2026-10-07 08:00:00", summary: "补了一段", source: "manual" },
    ],
  });
  assert.equal(meta.mainTitle, "甲");
  assert.equal(meta.subtitle, "乙");
  assert.equal(meta.createdAt, "2026-10-05 12:56:03");
  assert.equal(meta.revisedAt, "2026-10-07 08:00:00");
  assert.equal(meta.revisions[0].summary, "");
  assert.equal(meta.revisions[1].summary, "补了一段");
});

test("body tags are read from the published body field", () => {
  assert.deepEqual(
    noteBodyTags({
      folder: "20261007_015206",
      slug: "n-20261007-015206",
      title: "控制图",
      date: "2026-10-07",
      summary: "",
      body: { tags: [" 算法 ", "", "统计过程控制"] },
    }),
    ["算法", "统计过程控制"],
  );
  assert.deepEqual(
    noteBodyTags({
      folder: "empty",
      slug: "empty",
      title: "空",
      date: "2026-10-07",
      summary: "",
    }),
    [],
  );
});

test("display date uses dots and frontmatter is stripped", () => {
  assert.equal(displayNoteDate("2026-10-05"), "2026.10.05");
  assert.equal(stripNoteFrontmatter("---\ntitle: x\n---\n\n正文\n"), "正文\n");
});

test("detail timestamps keep seconds and expose a machine-readable value", () => {
  assert.equal(displayNoteTimestamp("2026-10-05 12:56:03"), "2026.10.05 12:56:03");
  assert.equal(displayNoteTimestamp("2026-10-05 00:00:00"), "2026.10.05");
  assert.equal(displayNoteTimestamp("2026-10-07 08:00:00"), "2026.10.07 08:00:00");
  assert.equal(displayNoteTimestamp(" 2026-10-07 "), "2026.10.07");
  assert.equal(noteTimestampDateTime("2026-10-05 12:56:03"), "2026-10-05T12:56:03");
  assert.equal(noteTimestampDateTime("2026-10-05"), "2026-10-05");
});
