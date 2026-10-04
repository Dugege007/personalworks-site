import assert from "node:assert/strict";
import test from "node:test";
import { faviconHrefForSkin } from "../src/prefs/skinFavicon.ts";

test("标签页图标按皮肤取文件，未知皮肤回显影", () => {
  assert.equal(faviconHrefForSkin("strata"), "/favicon.svg");
  assert.equal(faviconHrefForSkin("develop"), "/favicon-develop.svg");
  assert.equal(faviconHrefForSkin("missing"), "/favicon-develop.svg");
});
