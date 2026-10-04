import assert from "node:assert/strict";
import test from "node:test";
import { photoSizes } from "../src/content/photoSizes.ts";
import {
  classifyPhotoOrientation,
  heroFrameFitsViewport,
  photoFitsViewport,
  readViewportOrientation,
} from "../src/content/photoOrientation.ts";

test("宽高相等为 1:1，其余按长短边", () => {
  assert.equal(classifyPhotoOrientation(2560, 2560), "square");
  assert.equal(classifyPhotoOrientation(2560, 1440), "landscape");
  assert.equal(classifyPhotoOrientation(1707, 2560), "portrait");
});

test("视口构图与照片构图同一套判定", () => {
  assert.equal(readViewportOrientation(1440, 900), "landscape");
  assert.equal(readViewportOrientation(390, 844), "portrait");
  assert.equal(readViewportOrientation(800, 800), "square");
});

test("1:1 照片与方屏视口都不限制方向", () => {
  assert.equal(photoFitsViewport("square", "landscape"), true);
  assert.equal(photoFitsViewport("square", "portrait"), true);
  assert.equal(photoFitsViewport("landscape", "square"), true);
  assert.equal(photoFitsViewport("portrait", "square"), true);
  assert.equal(photoFitsViewport("landscape", "landscape"), true);
  assert.equal(photoFitsViewport("portrait", "portrait"), true);
  assert.equal(photoFitsViewport("landscape", "portrait"), false);
  assert.equal(photoFitsViewport("portrait", "landscape"), false);
});

test("缺尺寸的画面不进入随机池", () => {
  assert.equal(heroFrameFitsViewport({}, "landscape"), false);
  assert.equal(heroFrameFitsViewport({ width: 2560, height: 1440 }, "landscape"), true);
  assert.equal(heroFrameFitsViewport({ width: 1707, height: 2560 }, "landscape"), false);
});

test("风光头图尺寸表横竖两边都有可抽画面", () => {
  const frames = Object.values(photoSizes);
  const landscape = frames.filter((frame) => heroFrameFitsViewport(frame, "landscape"));
  const portrait = frames.filter((frame) => heroFrameFitsViewport(frame, "portrait"));
  assert.ok(frames.length > 0);
  assert.ok(landscape.length > 0);
  assert.ok(portrait.length > 0);
  assert.ok(landscape.every((frame) => frame.width >= frame.height));
  assert.ok(portrait.every((frame) => frame.height >= frame.width));
});
