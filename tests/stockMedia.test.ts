import assert from "node:assert/strict";
import test from "node:test";
import { workCoverSrc } from "../src/content/stockMedia.ts";

test("封面优先静帧，仅视频时用 poster", () => {
  assert.equal(
    workCoverSrc({
      channel: "digital-twin",
      media: [
        { kind: "video", src: "digital-twin/demo/02.mp4", poster: "digital-twin/demo/02.poster.webp" },
        { kind: "image", src: "digital-twin/demo/01.webp" },
      ],
    }),
    "digital-twin/demo/01.webp",
  );
  assert.equal(
    workCoverSrc({
      channel: "digital-twin",
      media: [{ kind: "video", src: "digital-twin/demo/02.mp4", poster: "digital-twin/demo/02.poster.webp" }],
    }),
    "digital-twin/demo/02.poster.webp",
  );
});
