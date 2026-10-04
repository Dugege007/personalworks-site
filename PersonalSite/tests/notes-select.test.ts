import assert from "node:assert/strict";
import test from "node:test";
import { displayNoteDate, stripNoteFrontmatter, visibleNotes } from "../src/content/notes/select.ts";

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

test("display date uses dots and frontmatter is stripped", () => {
  assert.equal(displayNoteDate("2026-10-05"), "2026.10.05");
  assert.equal(stripNoteFrontmatter("---\ntitle: x\n---\n\n正文\n"), "正文\n");
});
