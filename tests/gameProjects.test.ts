import assert from "node:assert/strict";
import test from "node:test";
import { toListedMedia } from "../src/content/listedMedia.ts";
import { workCoverSrc } from "../src/content/stockMedia.ts";

test("MP4 截图记为视频并带约定封面键", () => {
  const video = toListedMedia("game-dev/demo/01.mp4", "演示");
  assert.equal(video.kind, "video");
  assert.equal(video.poster, "game-dev/demo/01.poster.webp");
  const image = toListedMedia("game-dev/demo/01.webp", "截图");
  assert.equal(image.kind, "image");
  assert.equal(image.poster, undefined);
});

test("游戏仅视频时封面走 poster，有静帧时仍用静帧", () => {
  assert.equal(
    workCoverSrc({
      channel: "game-dev",
      media: [toListedMedia("game-dev/squirmeal/01.mp4", "蛄蛹者_参赛视频")],
    }),
    "game-dev/squirmeal/01.poster.webp",
  );
  assert.equal(
    workCoverSrc({
      channel: "game-dev",
      media: [
        toListedMedia("game-dev/antigravity/01.webp", "帮助界面"),
        toListedMedia("game-dev/demo/01.mp4", "演示"),
      ],
    }),
    "game-dev/antigravity/01.webp",
  );
});
