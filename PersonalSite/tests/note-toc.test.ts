import assert from "node:assert/strict";
import test from "node:test";
import { buildNoteToc, defaultCollapsedTocIds, noteTocSignature, type NoteTocHeading } from "../src/notes/toc.ts";

test("toc nests h2 through h4 and keeps orphan headings", () => {
  const headings: NoteTocHeading[] = [
    { id: "孤三级", text: "孤三级", depth: 3 },
    { id: "概览", text: " 概览 ", depth: 2 },
    { id: "步骤", text: "步骤", depth: 2 },
    { id: "准备", text: "准备", depth: 3 },
    { id: "材料", text: "材料", depth: 4 },
    { id: "空", text: "   ", depth: 2 },
  ];
  const tree = buildNoteToc(headings);
  assert.deepEqual(
    tree.map((node) => node.id),
    ["孤三级", "概览", "步骤"],
  );
  assert.equal(tree[2]?.children[0]?.id, "准备");
  assert.equal(tree[2]?.children[0]?.children[0]?.id, "材料");
  assert.deepEqual(defaultCollapsedTocIds(tree), ["步骤", "准备"]);
  assert.equal(noteTocSignature(tree), noteTocSignature(buildNoteToc(headings)));
});

test("toc is empty when the note has no h2 to h4", () => {
  assert.deepEqual(buildNoteToc([]), []);
  assert.deepEqual(defaultCollapsedTocIds([]), []);
});
