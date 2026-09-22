import assert from "node:assert/strict";
import test from "node:test";
import { readCatalogFromState, splitHref } from "../src/ia/photoNav.ts";

test("带查询的总览 href 拆成 pathname 与 search", () => {
  assert.deepEqual(splitHref("/photo/catalog?channel=landscape-photo&sort=asc"), {
    pathname: "/photo/catalog",
    search: "?channel=landscape-photo&sort=asc",
  });
  assert.deepEqual(splitHref("/photo/catalog"), { pathname: "/photo/catalog", search: "" });
});

test("详情只接受本站总览或拍摄列表来源", () => {
  const catalog = "/photo/catalog";
  const shoots = "/photo/shoots";
  assert.equal(
    readCatalogFromState("/photo/catalog?channel=humanist-photo", catalog),
    "/photo/catalog?channel=humanist-photo",
  );
  assert.equal(readCatalogFromState("/photo/shoots?year=2024", [catalog, shoots]), "/photo/shoots?year=2024");
  assert.equal(readCatalogFromState("/photo", catalog), undefined);
  assert.equal(readCatalogFromState("//evil.example/photo/catalog", catalog), undefined);
  assert.equal(readCatalogFromState("https://example.com/photo/catalog", catalog), undefined);
});
