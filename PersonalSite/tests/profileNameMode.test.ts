import assert from "node:assert/strict";
import test from "node:test";
import {
  PROFILE_NAME_KEY,
  parseProfileNameMode,
  readProfileNameMode,
  siteDocumentTitle,
  toggleProfileNameMode,
  writeProfileNameMode,
} from "../src/content/profileNameMode.ts";

test("标签页标题跟随曾用名与现用名", () => {
  assert.equal(siteDocumentTitle("杜宏博"), "杜宏博-个人网站");
  assert.equal(siteDocumentTitle("杜红勃"), "杜红勃-个人网站");
});

test("姓名模式只认 legal，其余回到曾用名", () => {
  assert.equal(parseProfileNameMode("legal"), "legal");
  assert.equal(parseProfileNameMode("alias"), "alias");
  assert.equal(parseProfileNameMode(null), "alias");
  assert.equal(parseProfileNameMode("杜红勃"), "alias");
  assert.equal(toggleProfileNameMode("alias"), "legal");
  assert.equal(toggleProfileNameMode("legal"), "alias");
});

test("切换后写入本地，再读仍是上次的名字", () => {
  const store = new Map<string, string>();
  const storage = {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => {
      store.set(key, value);
    },
  };

  assert.equal(readProfileNameMode(storage), "alias");
  writeProfileNameMode("legal", storage);
  assert.equal(store.get(PROFILE_NAME_KEY), "legal");
  assert.equal(readProfileNameMode(storage), "legal");
  writeProfileNameMode("alias", storage);
  assert.equal(readProfileNameMode(storage), "alias");
});
