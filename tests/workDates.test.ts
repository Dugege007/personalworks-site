import assert from "node:assert/strict";
import test from "node:test";
import { effectiveDate, sortByEffectiveDateDescending } from "../src/content/workDates.ts";

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
