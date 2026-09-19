import assert from "node:assert/strict";
import test from "node:test";
import { initialWorkProjects } from "../src/content/initialWorkProjects.ts";

test("景观效果图保留方案简介、尺度与风格标签", () => {
  const works = initialWorkProjects.filter((work) => work.channel === "landscape-rendering");

  assert.equal(works.length, 17);
  for (const work of works) {
    assert.ok(work.summary && !work.summary.includes("共收录"), work.id);
    assert.ok(work.body && !work.body.includes("共收录"), work.id);
    assert.ok(work.siteType, work.id);
    assert.ok(work.tags && work.tags.length > 0, work.id);
  }
});
