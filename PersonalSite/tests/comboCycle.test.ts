import assert from "node:assert/strict";
import test from "node:test";
import { contentDraw, slottedDraw } from "../src/content/stars.ts";
import { nextComboIndex, pickComboSrc, pickWeightedSrc } from "../src/lib/comboCycle.ts";

test("停着的格跳过，从左到右继续", () => {
  assert.equal(nextComboIndex(1, 1, 5), 2);
  assert.equal(nextComboIndex(4, 4, 5), 0);
  assert.equal(nextComboIndex(0, null, 4), 0);
});

test("不可换的格不占这一轮", () => {
  assert.equal(nextComboIndex(0, null, 4, (index) => index !== 0), 1);
  assert.equal(nextComboIndex(0, 1, 2, (index) => index === 1), -1);
});

test("近5含当前张，窗外一张必中", () => {
  const pool = ["a", "b", "c", "d", "e", "f"];
  const recent = ["a", "b", "c", "d", "e"];
  assert.equal(recent.length, 5);
  assert.equal(pickComboSrc(pool, recent, 5), "f");
});

test("池张数等于不重复上限时，下一张是最早的一张", () => {
  const pool = ["a", "b", "c", "d", "e"].map((src) => ({ src, weight: 1 }));
  const noRepeat = 5;
  let recent = ["a", "b", "c", "d"];
  let cur = "e";
  const seen = [cur];
  for (let step = 0; step < 8; step += 1) {
    const next = pickWeightedSrc(pool, [...recent, cur], noRepeat, () => 0);
    assert.notEqual(next, cur);
    recent = [...recent, cur].slice(-(noRepeat - 1));
    cur = next;
    seen.push(cur);
  }
  assert.deepEqual(seen, ["e", "a", "b", "c", "d", "e", "a", "b", "c"]);
});

test("0 星不进候选，高权重区间抽中 5 星", () => {
  const pool = [
    { src: "five", weight: 1 },
    { src: "one", weight: 0.2 },
    { src: "zero", weight: 0 },
  ];
  assert.equal(pickWeightedSrc(pool, [], 5, () => 0), "five");
  assert.equal(pickWeightedSrc(pool, [], 5, () => 0.99), "one");
});

test("没有星级槽权重为 1，写了星级走表", () => {
  assert.deepEqual(slottedDraw("a", undefined), { src: "a", weight: 1 });
  assert.equal(slottedDraw("a", 0), null);
  assert.deepEqual(slottedDraw("a", 1), { src: "a", weight: 0.2 });
  assert.deepEqual(slottedDraw("a", 5), { src: "a", weight: 1 });
});

test("内容层缺星级与 0 星都不进候选", () => {
  assert.equal(contentDraw("a", {}), null);
  assert.equal(contentDraw("a", { stars: 0 }), null);
  assert.equal(contentDraw("a", undefined), null);
  assert.deepEqual(contentDraw("a", { stars: 4 }), { src: "a", weight: 0.8 });
});

test("近窗剔掉高星后抽到余下的一张", () => {
  const pool = [
    { src: "five", weight: 1 },
    { src: "one", weight: 0.2 },
  ];
  assert.equal(pickWeightedSrc(pool, ["five"], 2, () => 0), "one");
});
