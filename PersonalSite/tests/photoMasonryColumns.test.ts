import assert from "node:assert/strict";
import test from "node:test";
import { photoMasonryColumnCount } from "../src/components/work/photoMasonryColumns.ts";

test("一两项时不论宽度都是两列", () => {
  for (const width of [320, 390, 560, 900, 1200, 1400, 1920]) {
    assert.equal(photoMasonryColumnCount(0, width), 2);
    assert.equal(photoMasonryColumnCount(1, width), 2);
    assert.equal(photoMasonryColumnCount(2, width), 2);
  }
});

test("三项及以上窄屏至少两列，900 与 1400 的分档不变", () => {
  assert.equal(photoMasonryColumnCount(3, 320), 2);
  assert.equal(photoMasonryColumnCount(8, 390), 2);
  assert.equal(photoMasonryColumnCount(8, 560), 2);
  assert.equal(photoMasonryColumnCount(8, 900), 2);
  assert.equal(photoMasonryColumnCount(8, 901), 3);
  assert.equal(photoMasonryColumnCount(8, 1399), 3);
  assert.equal(photoMasonryColumnCount(8, 1400), 4);
  assert.equal(photoMasonryColumnCount(8, 1920), 4);
});
