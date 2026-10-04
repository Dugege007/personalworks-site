import assert from "node:assert/strict";
import test from "node:test";
import { compareByStartedOn, comparePhotoTime, effectiveDate, photoTimeKey, startedOnDate, sortByEffectiveDateDescending } from "../src/content/workDates.ts";

test("有效日期按拍摄、项目、年份依次回退", () => {
  assert.equal(
    effectiveDate({
      id: "a",
      title: "A",
      capturedOn: "2026-08-15",
      startedOn: "2020-01",
      year: "2019",
    }),
    "2026-08-15",
  );
  assert.equal(
    effectiveDate({ id: "b", title: "B", startedOn: "2020-06", year: "2019" }),
    "2020-06-00",
  );
  assert.equal(effectiveDate({ id: "c", title: "C", year: "2024" }), "2024-00-00");
  assert.equal(
    effectiveDate({
      id: "d",
      title: "D",
      capturedOn: "2024-02-31",
      startedOn: "2024-02",
    }),
    "2024-02-00",
  );
});

test("开始日期不采用拍摄日", () => {
  const olderStart = {
    id: "start",
    title: "开始更早",
    startedOn: "2024-03-01",
    capturedOn: "2026-08-15",
  };
  const newerStart = {
    id: "later",
    title: "开始更晚",
    startedOn: "2025-01-01",
    capturedOn: "2020-01-01",
  };
  assert.equal(startedOnDate(olderStart), "2024-03-01");
  assert.deepEqual(
    [newerStart, olderStart].sort((a, b) => compareByStartedOn(a, b, "desc")).map((item) => item.id),
    ["later", "start"],
  );
});

test("拍摄时刻优先于开始日期的零点", () => {
  assert.equal(photoTimeKey("2018-04-06T20:14:32", "2018-04-06"), "2018-04-06T20:14:32");
  assert.equal(photoTimeKey("", "2018-04-06"), "2018-04-06T00:00:00");
  assert.ok(comparePhotoTime("2018-04-06T20:26:17", "2018-04-06T20:14:32", "desc") < 0);
});

test("倒序函数使用完整日期并把无日期项目放到末尾", () => {
  const sorted = sortByEffectiveDateDescending([
    { id: "unknown", title: "无日期" },
    { id: "older", title: "较早", capturedOn: "2025-01-01" },
    { id: "newer", title: "较新", capturedOn: "2025-10-10" },
  ]);
  assert.deepEqual(
    sorted.map((item) => item.id),
    ["newer", "older", "unknown"],
  );
});
