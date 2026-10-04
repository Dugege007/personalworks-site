import assert from "node:assert/strict";
import test from "node:test";
import {
  disabledPhotoFacetValues,
  filterPhotoFrames,
  filterPhotoWorks,
  occupiedPhotoFacetsFromFrames,
  orderPhotoCustomTags,
  photoTagResourceCounts,
  primaryFacetValues,
  type PhotoCatalogQuery,
} from "../src/content/photoFacet.ts";

type SampleMedia = {
  kind: "image";
  label: string;
  src: string;
  themes: string[];
  tags?: string[];
};

type SampleWork = {
  id: string;
  year: string;
  place?: string;
  tags?: string[];
  themes?: string[];
  media: SampleMedia[];
};

const baseQuery: PhotoCatalogQuery = {
  themes: [],
  years: [],
  places: [],
  tags: [],
  untagged: false,
  sort: "desc",
};

function work(partial: SampleWork): SampleWork {
  return partial;
}

function framesOf(records: readonly SampleWork[]) {
  return records.flatMap((item) =>
    item.media
      .filter((media) => media.src)
      .map((media) => ({ work: item, media, src: media.src ?? "" })),
  );
}

const landscape = work({
  id: "landscape",
  year: "2024",
  place: "舟山",
  media: [{ kind: "image", label: "1", src: "a.webp", themes: ["landscape-photo"] }],
});

const both = work({
  id: "both",
  year: "2025",
  place: "南京",
  tags: ["日常", "街拍"],
  media: [
    {
      kind: "image",
      label: "1",
      src: "b.webp",
      themes: ["landscape-photo", "humanist-photo"],
    },
  ],
});

const humanist = work({
  id: "humanist",
  year: "2024",
  place: "上海",
  tags: ["日常"],
  media: [{ kind: "image", label: "1", src: "c.webp", themes: ["humanist-photo"] }],
});

const records = [landscape, humanist, both];
const frames = framesOf(records);

test("静帧按已选类型、年份、地点、标签取交集", () => {
  const bothThemes = filterPhotoFrames(frames, {
    ...baseQuery,
    themes: ["landscape-photo", "humanist-photo"],
  });
  assert.deepEqual(
    bothThemes.map((item) => item.src),
    ["b.webp"],
  );

  const narrowed = filterPhotoFrames(frames, {
    ...baseQuery,
    themes: ["landscape-photo"],
    years: ["2024"],
    places: ["舟山"],
  });
  assert.deepEqual(
    narrowed.map((item) => item.src),
    ["a.webp"],
  );

  const twoYears = filterPhotoFrames(frames, { ...baseQuery, years: ["2024", "2025"] });
  assert.equal(twoYears.length, 0);

  const bothTags = filterPhotoFrames(frames, { ...baseQuery, tags: ["日常", "街拍"] });
  assert.deepEqual(
    bothTags.map((item) => item.src),
    ["b.webp"],
  );
});

test("主题总览按该次外出汇总后的类型取交集", () => {
  const split = work({
    id: "split",
    year: "2026",
    place: "杭州",
    media: [
      { kind: "image", label: "1", src: "d.webp", themes: ["landscape-photo"] },
      { kind: "image", label: "2", src: "e.webp", themes: ["humanist-photo"] },
    ],
  });
  const matched = filterPhotoWorks([landscape, split], {
    ...baseQuery,
    themes: ["landscape-photo", "humanist-photo"],
  });
  assert.deepEqual(
    matched.map((item) => item.id),
    ["split"],
  );
});

test("没有作品的标签置灰，取消选择后恢复", () => {
  const typeValues = ["landscape-photo", "humanist-photo", "portrait-photo"];
  const allOccupied = occupiedPhotoFacetsFromFrames(frames);
  assert.deepEqual(disabledPhotoFacetValues(typeValues, allOccupied.themes, []), ["portrait-photo"]);

  const landscapeOnly = filterPhotoFrames(frames, { ...baseQuery, themes: ["landscape-photo"] });
  const landscapeOccupied = occupiedPhotoFacetsFromFrames(landscapeOnly);
  assert.deepEqual(disabledPhotoFacetValues(["2024", "2021"], landscapeOccupied.years, []), ["2021"]);
  assert.deepEqual(
    disabledPhotoFacetValues(typeValues, landscapeOccupied.themes, ["landscape-photo"]),
    ["portrait-photo"],
  );

  const bothOnly = filterPhotoFrames(frames, {
    ...baseQuery,
    themes: ["landscape-photo", "humanist-photo"],
  });
  const bothOccupied = occupiedPhotoFacetsFromFrames(bothOnly);
  assert.deepEqual(disabledPhotoFacetValues(["2024", "2025"], bothOccupied.years, []), ["2024"]);
  assert.deepEqual(
    disabledPhotoFacetValues(["舟山", "南京", "上海"], bothOccupied.places, []),
    ["舟山", "上海"],
  );

  const cleared = occupiedPhotoFacetsFromFrames(frames);
  assert.deepEqual(disabledPhotoFacetValues(["2024", "2025"], cleared.years, []), []);
  assert.deepEqual(disabledPhotoFacetValues(["2024"], bothOccupied.years, ["2024"]), []);
});

test("项目里已有张写了类型时，其余照片不再沿用项目类型", () => {
  const mixed = work({
    id: "mixed",
    year: "2025",
    place: "安阳",
    themes: ["landscape-photo"],
    media: [
      {
        kind: "image",
        label: "both",
        src: "both.webp",
        themes: ["landscape-photo"],
        tags: ["展馆"],
      },
      { kind: "image", label: "tag-only", src: "tag.webp", themes: [], tags: ["展馆"] },
      { kind: "image", label: "other", src: "other.webp", themes: [], tags: ["随拍"] },
    ],
  });
  const bothTags = filterPhotoFrames(framesOf([mixed]), {
    ...baseQuery,
    themes: ["landscape-photo"],
    tags: ["展馆"],
  });
  assert.deepEqual(
    bothTags.map((item) => item.src),
    ["both.webp"],
  );
  const landscapeOnly = filterPhotoFrames(framesOf([mixed]), {
    ...baseQuery,
    themes: ["landscape-photo"],
  });
  assert.deepEqual(
    landscapeOnly.map((item) => item.src),
    ["both.webp"],
  );
});

test("单张未写类型时沿用该次拍摄的类型", () => {
  const inherited = work({
    id: "inherited",
    year: "2025",
    place: "安阳",
    themes: ["humanist-photo"],
    media: [{ kind: "image", label: "1", src: "f.webp", themes: [] }],
  });
  const matched = filterPhotoFrames(framesOf([inherited, landscape]), {
    ...baseQuery,
    themes: ["humanist-photo"],
  });
  assert.deepEqual(
    matched.map((item) => item.src),
    ["f.webp"],
  );
  const occupied = occupiedPhotoFacetsFromFrames(framesOf([inherited]));
  assert.equal(occupied.themes.has("humanist-photo"), true);

  const explicit = work({
    id: "explicit",
    year: "2025",
    place: "上海",
    themes: ["humanist-photo"],
    media: [{ kind: "image", label: "1", src: "g.webp", themes: ["landscape-photo"] }],
  });
  const kept = filterPhotoFrames(framesOf([explicit]), {
    ...baseQuery,
    themes: ["landscape-photo"],
  });
  assert.deepEqual(
    kept.map((item) => item.src),
    ["g.webp"],
  );
});

test("单张自由标签进入筛选，未打标签的照片可单独筛出", () => {
  const hall = work({
    id: "hall",
    year: "2025",
    place: "上海",
    media: [{ kind: "image", label: "1", src: "h.webp", themes: ["humanist-photo"], tags: ["展馆"] }],
  });
  const bare = work({
    id: "bare",
    year: "2026",
    place: "杭州",
    media: [{ kind: "image", label: "1", src: "i.webp", themes: [] }],
  });
  const snap = work({
    id: "snap",
    year: "2026",
    place: "杭州",
    media: [{ kind: "image", label: "1", src: "j.webp", themes: [], tags: ["随拍", "夜色"] }],
  });
  const set = framesOf([hall, bare, snap]);
  assert.deepEqual(
    filterPhotoFrames(set, { ...baseQuery, tags: ["展馆"] }).map((item) => item.src),
    ["h.webp"],
  );
  assert.deepEqual(
    filterPhotoFrames(set, { ...baseQuery, tags: ["夜色"] }).map((item) => item.src),
    ["j.webp"],
  );
  assert.deepEqual(
    filterPhotoFrames(set, { ...baseQuery, untagged: true }).map((item) => item.src),
    ["i.webp"],
  );
  const occupied = occupiedPhotoFacetsFromFrames(set);
  assert.equal(occupied.tags.has("展馆"), true);
  assert.equal(occupied.tags.has("夜色"), true);
  assert.equal(occupied.untagged, true);
});

test("类型第一行顺序为风光、展馆、人像、人文、随拍、游戏、AI", () => {
  assert.deepEqual(primaryFacetValues(false), [
    "landscape-photo",
    "展馆",
    "portrait-photo",
    "humanist-photo",
    "随拍",
    "game-photo",
    "ai-photo",
  ]);
});

test("类型第二行按筛选前的照片数量从多到少", () => {
  const night = work({
    id: "night",
    year: "2024",
    tags: ["夜景"],
    media: [
      { kind: "image", label: "1", src: "n1.webp", themes: ["landscape-photo"], tags: ["夜景", "城市"] },
      { kind: "image", label: "2", src: "n2.webp", themes: ["landscape-photo"], tags: ["城市"] },
      { kind: "image", label: "3", src: "", themes: ["landscape-photo"], tags: ["城市"] },
    ],
  });
  const city = work({
    id: "city",
    year: "2025",
    media: [{ kind: "image", label: "1", src: "c1.webp", themes: ["landscape-photo"], tags: ["城市", "展馆"] }],
  });
  const counts = photoTagResourceCounts([night, city]);
  assert.equal(counts.get("夜景"), 2);
  assert.equal(counts.get("城市"), 3);
  assert.equal(counts.get("展馆"), 1);
  assert.deepEqual(orderPhotoCustomTags(["城市", "夜景", "展馆", "随拍"], counts), ["城市", "夜景"]);
});
