import assert from "node:assert/strict";
import test from "node:test";
import {
  channelShowsStudioAlias,
  resolveStudioAlias,
  studioAliasForDisplay,
} from "../src/content/studios.ts";

test("效果图只有一家公司，年份后不出简称", () => {
  assert.equal(channelShowsStudioAlias("landscape-rendering"), false);
  assert.equal(
    studioAliasForDisplay({
      channel: "landscape-rendering",
      stageFolder: "landscape-rendering/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑",
    }),
    undefined,
  );
});

test("施工图超过一家公司，从投放夹解析简称", () => {
  assert.equal(channelShowsStudioAlias("landscape-cds"), true);
  assert.equal(
    studioAliasForDisplay({
      channel: "landscape-cds",
      stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/201709 世茂北京一渡",
    }),
    "道田景观",
  );
  assert.equal(
    studioAliasForDisplay({
      channel: "landscape-cds",
      stageFolder: "landscape-cds/上海日清景观设计有限公司/20220106 北蔡培花社区",
    }),
    "日清景观",
  );
});

test("手写简称优先于投放夹", () => {
  assert.equal(
    resolveStudioAlias({
      studioAlias: "日清景观",
      stageFolder: "landscape-cds/上海道田景观工程咨询有限公司/demo",
    }),
    "日清景观",
  );
});
