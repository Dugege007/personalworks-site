import assert from "node:assert/strict";
import test from "node:test";
import {
  masonryHeightsForPlacement,
  placeShortestColumns,
} from "../src/components/work/shortestColumn.ts";

function columnsOf(heights: readonly number[], columnCount: number, gap = 0): number[] {
  return placeShortestColumns(heights, columnCount, gap).placements.map((item) => item.column);
}

function roundRobin(count: number, columnCount: number): number[] {
  return Array.from({ length: count }, (_, index) => index % columnCount);
}

test("等高时并列取最左列，逐列从左向右铺", () => {
  assert.deepEqual(columnsOf([10, 10, 10, 10], 3), [0, 1, 2, 0]);
  const layout = placeShortestColumns([10, 10, 10, 10], 3, 0);
  assert.deepEqual(
    layout.placements.map((item) => item.top),
    [0, 0, 0, 10],
  );
});

test("下一张进入当前最短列，而不是按下标取余", () => {
  const heights = [300, 100, 100, 100, 100];
  const assigned = columnsOf(heights, 3);
  assert.deepEqual(assigned, [0, 1, 2, 1, 2]);
  assert.notDeepEqual(assigned, roundRobin(heights.length, 3));
});

test("列间距计入列高，避免少算间距时选错列", () => {
  assert.deepEqual(columnsOf([30, 10, 10, 10], 2, 14), [0, 1, 1, 0]);
  const layout = placeShortestColumns([100, 40], 1, 14);
  assert.equal(layout.placements[1]?.top, 114);
  assert.equal(layout.containerHeight, 154);
  assert.equal(placeShortestColumns([100, 40], 2, 14).containerHeight, 100);
});

test("同高并列仍取最左列", () => {
  assert.deepEqual(columnsOf([10, 10, 10], 2), [0, 1, 0]);
});

test("尚未测高的项用中位数占位，不会全部堆进第 0 列", () => {
  assert.deepEqual(masonryHeightsForPlacement([0, 0, 0], 240), [240, 240, 240]);
  assert.deepEqual(masonryHeightsForPlacement([200, 0, 80], 240), [200, 80, 80]);
  assert.deepEqual(columnsOf(masonryHeightsForPlacement([0, 0, 0, 0], 100), 3), [0, 1, 2, 0]);
});
