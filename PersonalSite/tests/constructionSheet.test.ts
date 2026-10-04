import assert from "node:assert/strict";
import test from "node:test";
import { constructionSheetMark, constructionSheets } from "../src/content/constructionSheet.ts";

test("自动图名改用源文件名，手写图名保持原文", () => {
  assert.equal(
    constructionSheetMark({ label: "效果图 01", src: "landscape-cds/demo/101.webp" }, 0),
    "101",
  );
  assert.equal(
    constructionSheetMark({ label: "通风井详图", src: "landscape-cds/demo/08.webp" }, 1),
    "通风井详图",
  );
  assert.equal(constructionSheetMark({ label: "", src: undefined }, 0), "01");
});

test("图台只收带地址的静帧", () => {
  const sheets = constructionSheets([
    { kind: "image", label: "效果图 01", src: "a.webp" },
    { kind: "image", label: "效果图 02" },
    { kind: "video", label: "视频", src: "a.mp4" },
  ]);
  assert.equal(sheets.length, 1);
  assert.equal(sheets[0]?.src, "a.webp");
});
