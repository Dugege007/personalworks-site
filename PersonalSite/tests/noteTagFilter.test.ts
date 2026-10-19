import assert from "node:assert/strict";
import test from "node:test";
import {
  disabledNoteTagFacets,
  filterNotesByTags,
  noteTagFacetValues,
  noteTagSearch,
  noteTagsOf,
  noteUntaggedKey,
  occupiedNoteTags,
  orderNoteTags,
  parseNoteTagQuery,
  selectedNoteTagFacets,
  toggleNoteTagParam,
  writeNoteTagQuery,
  type NoteTagQuery,
} from "../src/content/notes/tagFilter.ts";

type SampleNote = {
  slug: string;
  tags?: string[];
};

const emptyQuery: NoteTagQuery = { tags: [], untagged: false };

const notes: SampleNote[] = [
  { slug: "both", tags: ["算法", "时序"] },
  { slug: "algo", tags: ["算法"] },
  { slug: "path", tags: ["路径规划", "算法"] },
  { slug: "blank", tags: ["  ", ""] },
  { slug: "missing" },
];

test("空标签、缺字段与空白项都视为无标签", () => {
  assert.deepEqual(noteTagsOf({ tags: [] }), []);
  assert.deepEqual(noteTagsOf({}), []);
  assert.deepEqual(noteTagsOf({ tags: ["  ", ""] }), []);
  assert.deepEqual(
    filterNotesByTags(notes, { ...emptyQuery, untagged: true }).map((item) => item.slug),
    ["blank", "missing"],
  );
  assert.deepEqual(
    filterNotesByTags(notes, { tags: ["算法"], untagged: true }).map((item) => item.slug),
    [],
  );
});

test("单标签只保留带该词的篇目", () => {
  assert.deepEqual(
    filterNotesByTags(notes, { ...emptyQuery, tags: ["时序"] }).map((item) => item.slug),
    ["both"],
  );
  assert.deepEqual(
    filterNotesByTags(notes, emptyQuery).map((item) => item.slug),
    ["both", "algo", "path", "blank", "missing"],
  );
});

test("多标签按交集筛选", () => {
  assert.deepEqual(
    filterNotesByTags(notes, { ...emptyQuery, tags: ["算法", "路径规划"] }).map((item) => item.slug),
    ["path"],
  );
  assert.deepEqual(
    filterNotesByTags(notes, { ...emptyQuery, tags: ["算法", "时序"] }).map((item) => item.slug),
    ["both"],
  );
  assert.deepEqual(
    filterNotesByTags(notes, { ...emptyQuery, tags: ["算法", "不存在"] }).map((item) => item.slug),
    [],
  );
});

test("候选项只聚合心得里出现过的标签，并按篇数从多到少", () => {
  const counts = new Map([
    ["算法", 3],
    ["时序", 1],
    ["路径规划", 1],
  ]);
  assert.deepEqual(orderNoteTags(["时序", "路径规划", "算法"], counts), ["算法", "路径规划", "时序"]);
  assert.deepEqual(orderNoteTags(["beta", "alpha"], new Map([["beta", 1], ["alpha", 1]])), ["alpha", "beta"]);

  const values = noteTagFacetValues(notes);
  assert.deepEqual(values.slice(0, 1), ["算法"]);
  assert.equal(values.at(-1), noteUntaggedKey);
  assert.equal(values.includes("展馆"), false);
  assert.equal(values.includes("随拍"), false);
  assert.equal(values.includes("landscape-photo"), false);
  assert.equal(values.includes("humanist-photo"), false);
  assert.equal(values.includes("portrait-photo"), false);
  assert.deepEqual(noteTagFacetValues([{ tags: ["算法"] }]), ["算法"]);
});

test("已选标签留下，当前结果对不上的取值置灰", () => {
  const values = noteTagFacetValues(notes);
  const algoOnly = filterNotesByTags(notes, { ...emptyQuery, tags: ["时序"] });
  const occupied = occupiedNoteTags(algoOnly);
  assert.deepEqual(disabledNoteTagFacets(values, occupied, { ...emptyQuery, tags: ["时序"] }), [
    "路径规划",
    noteUntaggedKey,
  ]);
  assert.deepEqual(selectedNoteTagFacets({ tags: ["时序"], untagged: true }), ["时序", noteUntaggedKey]);
});

test("查询参数可往返，无标签不写入 tag", () => {
  const query: NoteTagQuery = { tags: ["算法", "A*"], untagged: true };
  const params = writeNoteTagQuery(query);
  assert.deepEqual(params.getAll("tag"), ["算法", "A*"]);
  assert.equal(params.get("untagged"), "1");
  assert.equal(params.getAll("tag").includes(noteUntaggedKey), false);
  assert.equal(params.getAll("tag").includes("无标签"), false);
  assert.deepEqual(parseNoteTagQuery(new URLSearchParams(params.toString())), query);
  assert.equal(noteTagSearch({ tags: [], untagged: true }), "?untagged=1");
  assert.equal(noteTagSearch(emptyQuery), "");

  const literal = parseNoteTagQuery(new URLSearchParams("tag=untagged"));
  assert.deepEqual(literal, { tags: ["untagged"], untagged: false });
  const semantic = parseNoteTagQuery(new URLSearchParams("untagged=1"));
  assert.deepEqual(semantic, { tags: [], untagged: true });
  assert.deepEqual(parseNoteTagQuery(new URLSearchParams("tag=算法,时序")).tags, ["算法,时序"]);

  const withExtra = new URLSearchParams("from=home&tag=%20&tag=算法&tag=算法");
  const toggled = toggleNoteTagParam(withExtra, "时序");
  assert.equal(toggled.get("from"), "home");
  assert.deepEqual(parseNoteTagQuery(toggled), { tags: ["算法", "时序"], untagged: false });

  const untaggedOn = toggleNoteTagParam(toggled, noteUntaggedKey);
  assert.deepEqual(parseNoteTagQuery(untaggedOn), { tags: ["算法", "时序"], untagged: true });
  assert.equal(untaggedOn.getAll("tag").includes(noteUntaggedKey), false);

  const untaggedOff = toggleNoteTagParam(untaggedOn, noteUntaggedKey);
  assert.equal(parseNoteTagQuery(untaggedOff).untagged, false);

  const removed = toggleNoteTagParam(untaggedOff, "算法");
  assert.deepEqual(parseNoteTagQuery(removed).tags, ["时序"]);
});
