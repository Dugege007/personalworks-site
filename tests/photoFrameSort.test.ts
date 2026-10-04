import assert from "node:assert/strict";
import test from "node:test";
import {
  comparePhotoFrameFacts,
  flipPhotoFrameTime,
  parsePhotoFrameSort,
  photoFrameSortCaption,
  togglePhotoFrameSort,
  writePhotoFrameSort,
} from "../src/content/photoFrameSort.ts";

test("旧查询仍是时间方向", () => {
  assert.deepEqual(parsePhotoFrameSort(null), [{ key: "time", dir: "desc" }]);
  assert.deepEqual(parsePhotoFrameSort("asc"), [{ key: "time", dir: "asc" }]);
  assert.equal(writePhotoFrameSort(parsePhotoFrameSort(null)), null);
  assert.equal(writePhotoFrameSort(parsePhotoFrameSort("asc")), "asc");
});

test("只有星级时收起文案与时间同句式", () => {
  assert.equal(photoFrameSortCaption([{ key: "stars", dir: "desc" }]), "星级：从大到小");
});

test("星级在前，时间正序在后", () => {
  const rules = parsePhotoFrameSort("stars,time.asc");
  assert.deepEqual(rules, [
    { key: "stars", dir: "desc" },
    { key: "time", dir: "asc" },
  ]);
  assert.equal(photoFrameSortCaption(rules), "1 星级 · 2 时间");
  assert.equal(writePhotoFrameSort(rules), "stars,time.asc");
  const flipped = flipPhotoFrameTime(rules);
  assert.equal(flipped[1]?.dir, "desc");
  assert.equal(flipped[0]?.dir, "desc");
});

test("去掉最后一键回到时间倒序", () => {
  const onlyStars = togglePhotoFrameSort([{ key: "time", dir: "desc" }], "stars", "desc").filter(
    (rule) => rule.key === "stars",
  );
  assert.deepEqual(togglePhotoFrameSort(onlyStars, "stars", "asc"), [{ key: "time", dir: "desc" }]);
});

test("只有星级时同星从后到前", () => {
  const rules = [{ key: "stars" as const, dir: "desc" as const }];
  const early = { stars: 5, time: "2019-11-25T16:18:17" };
  const late = { stars: 5, time: "2025-04-13T15:30:28" };
  assert.ok(comparePhotoFrameFacts(late, early, rules) < 0);
  assert.ok(comparePhotoFrameFacts(early, late, rules) > 0);
});

test("先星级再时间", () => {
  const rules = [
    { key: "stars" as const, dir: "desc" as const },
    { key: "time" as const, dir: "asc" as const },
  ];
  const early = { stars: 5, time: "2019-11-25T16:18:17" };
  const late = { stars: 5, time: "2025-04-13T15:30:28" };
  const low = { stars: 1, time: "2025-04-13T15:30:28" };
  assert.ok(comparePhotoFrameFacts(early, late, rules) < 0);
  assert.ok(comparePhotoFrameFacts(late, low, rules) < 0);
  assert.ok(comparePhotoFrameFacts(low, early, rules) > 0);
});
