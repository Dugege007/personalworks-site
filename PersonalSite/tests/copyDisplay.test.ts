import assert from "node:assert/strict";
import test from "node:test";
import {
  isCopyFilled,
  resolveLead,
  resolveMediaDescription,
  resolveMediaDisplayName,
} from "../src/content/copyDisplay.ts";

test("未填与占位句都不算已填", () => {
  assert.equal(isCopyFilled(""), false);
  assert.equal(isCopyFilled("  "), false);
  assert.equal(isCopyFilled("待填写描述"), false);
  assert.equal(isCopyFilled("公园项目说明。"), true);
  assert.equal(resolveLead("待填写描述"), undefined);
  assert.equal(resolveLead("公园项目说明。"), "公园项目说明。");
});

test("资源名未填回退原来的 label", () => {
  assert.equal(resolveMediaDisplayName({ label: "效果图 01" }), "效果图 01");
  assert.equal(
    resolveMediaDisplayName({ label: "效果图 01", displayName: "公园入口" }),
    "公园入口",
  );
});

test("同一项目只填一张资源描述时其余回退项目描述", () => {
  const project = "公园项目说明。";
  assert.equal(
    resolveMediaDescription({ description: "仅此张资源说明。" }, project),
    "仅此张资源说明。",
  );
  assert.equal(resolveMediaDescription({}, project), project);
  assert.equal(resolveMediaDescription({ description: "" }, ""), undefined);
});
