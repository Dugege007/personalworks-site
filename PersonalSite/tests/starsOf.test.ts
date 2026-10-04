import assert from "node:assert/strict";
import test from "node:test";
import { starsOf } from "../src/content/stars.ts";

test("缺字段与 0 都是 0 星，1～5 原样返回", () => {
  assert.equal(starsOf(undefined), 0);
  assert.equal(starsOf({}), 0);
  assert.equal(starsOf({ stars: 0 }), 0);
  assert.equal(starsOf({ stars: 4 }), 4);
  assert.equal(starsOf({ stars: 9 }), 0);
});
