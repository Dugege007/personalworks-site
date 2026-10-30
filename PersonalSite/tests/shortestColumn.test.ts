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

test("已失败且高度为 0 的项不吃中位数，尚未加载的项仍用中位数", () => {
  assert.deepEqual(
    masonryHeightsForPlacement([200, 0, 80], 240, [false, true, false]),
    [200, 0, 80],
  );
  assert.deepEqual(masonryHeightsForPlacement([200, 0, 80], 240), [200, 80, 80]);
  assert.deepEqual(
    masonryHeightsForPlacement([0, 0, 0], 240, [true, false, false]),
    [0, 240, 240],
  );
  assert.deepEqual(
    masonryHeightsForPlacement([0, 0, 0], 240, [true, true, true]),
    [0, 0, 0],
  );
  const placed = masonryHeightsForPlacement([120, 0, 90, 0], 200, [false, true, false, false]);
  assert.deepEqual(placed, [120, 0, 90, 90]);
  const layout = placeShortestColumns(placed, 2, 14);
  const visible = placeShortestColumns([120, 90, 90], 2, 14);
  assert.equal(layout.containerHeight, visible.containerHeight);
  assert.equal(layout.placements[2]?.column, visible.placements[1]?.column);
  assert.equal(layout.placements[2]?.top, visible.placements[1]?.top);
  assert.equal(layout.placements[3]?.column, visible.placements[2]?.column);
  assert.equal(layout.placements[3]?.top, visible.placements[2]?.top);
});

test("高度为 0 的项不占槽，后面的卡片按没有它来排", () => {
  const withGap = placeShortestColumns([100, 0, 80], 2, 14);
  const without = placeShortestColumns([100, 80], 2, 14);
  assert.equal(withGap.containerHeight, without.containerHeight);
  assert.equal(withGap.placements[0]?.column, without.placements[0]?.column);
  assert.equal(withGap.placements[0]?.top, without.placements[0]?.top);
  assert.equal(withGap.placements[2]?.column, without.placements[1]?.column);
  assert.equal(withGap.placements[2]?.top, without.placements[1]?.top);
  assert.equal(withGap.columnHeights[0], 100);
  assert.equal(withGap.columnHeights[1], 80);
});
