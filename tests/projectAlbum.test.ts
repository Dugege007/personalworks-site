import assert from "node:assert/strict";
import test from "node:test";
import {
  planAlbumPreview,
  toProjectAlbumShots,
  TWIN_PREVIEW_LIMIT,
} from "../src/content/projectAlbum.ts";

test("预览格：不足或正好 7 张不叠层", () => {
  assert.deepEqual(planAlbumPreview(0), { visibleCount: 0, overflow: false });
  assert.deepEqual(planAlbumPreview(6), { visibleCount: 6, overflow: false });
  assert.deepEqual(planAlbumPreview(TWIN_PREVIEW_LIMIT), {
    visibleCount: TWIN_PREVIEW_LIMIT,
    overflow: false,
  });
});

test("预览格：超过 7 条媒体时末格叠层，仍只露 7 格", () => {
  assert.deepEqual(planAlbumPreview(8), { visibleCount: 7, overflow: true });
  assert.deepEqual(planAlbumPreview(12), { visibleCount: 7, overflow: true });
});

test("画册帧丢掉无 src 的槽，并回退资源名", () => {
  const shots = toProjectAlbumShots([
    { kind: "image", label: "总图" },
    { kind: "image", src: "digital-twin/demo/01.webp", label: "01", displayName: "工厂总览" },
    { kind: "video", src: "digital-twin/demo/02.mp4", poster: "digital-twin/demo/02.poster.webp", label: "02" },
  ]);
  assert.equal(shots.length, 2);
  assert.equal(shots[0]?.label, "工厂总览");
  assert.equal(shots[1]?.kind, "video");
  assert.equal(shots[1]?.poster, "digital-twin/demo/02.poster.webp");
});
