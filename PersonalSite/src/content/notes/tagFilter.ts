/** 筛选语义标识。只出现在查询参数与筛选项，不得写入内容层。 */
export const noteUntaggedKey = "untagged";

/** 筛选项上「无标签」的可见文案。 */
export const noteUntaggedLabel = "无标签";

export type NoteTagQuery = {
  tags: string[];
  untagged: boolean;
};

export type NoteTagCarrier = {
  tags?: readonly string[] | null;
};

/**
 * 读卡片上的正文标签。空白项去掉，同篇内去重。没有标签时返回空数组。
 */
export function noteTagsOf(note: NoteTagCarrier): string[] {
  if (!note.tags || note.tags.length === 0) {
    return [];
  }
  const picked: string[] = [];
  for (const tag of note.tags) {
    const text = tag.trim();
    if (!text || picked.includes(text)) {
      continue;
    }
    picked.push(text);
  }
  return picked;
}

/**
 * 读取心得列表的标签查询。`tag` 可重复，多值之间是交集；`untagged=1` 表示无标签，不把该标识当成自由标签。
 */
export function parseNoteTagQuery(params: URLSearchParams): NoteTagQuery {
  const tags: string[] = [];
  for (const raw of params.getAll("tag")) {
    const text = raw.trim();
    if (!text || tags.includes(text)) {
      continue;
    }
    tags.push(text);
  }
  return {
    tags,
    untagged: params.get("untagged") === "1",
  };
}

/**
 * 写成可分享的查询参数。无标签只写 `untagged=1`，不写入 `tag`。
 */
export function writeNoteTagQuery(query: NoteTagQuery): URLSearchParams {
  const params = new URLSearchParams();
  for (const tag of query.tags) {
    const text = tag.trim();
    if (!text || params.getAll("tag").includes(text)) {
      continue;
    }
    params.append("tag", text);
  }
  if (query.untagged) {
    params.set("untagged", "1");
  }
  return params;
}

/**
 * 列表路径上的查询串。没有筛选时为空串。
 */
export function noteTagSearch(query: NoteTagQuery): string {
  const text = writeNoteTagQuery(query).toString();
  return text ? `?${text}` : "";
}

/**
 * 切换一个筛选项，并保留查询里与标签无关的其它参数。
 * 语义项 `untagged` 只翻转 `untagged=1`。
 */
export function toggleNoteTagParam(params: URLSearchParams, value: string): URLSearchParams {
  const query = parseNoteTagQuery(params);
  if (value === noteUntaggedKey) {
    query.untagged = !query.untagged;
  } else {
    const text = value.trim();
    if (text) {
      query.tags = query.tags.includes(text)
        ? query.tags.filter((item) => item !== text)
        : [...query.tags, text];
    }
  }
  const next = new URLSearchParams(params);
  next.delete("tag");
  next.delete("untagged");
  const written = writeNoteTagQuery(query);
  for (const tag of written.getAll("tag")) {
    next.append("tag", tag);
  }
  const untagged = written.get("untagged");
  if (untagged) {
    next.set("untagged", untagged);
  }
  return next;
}

function matchesEvery(actual: readonly string[], selected: readonly string[]): boolean {
  if (selected.length === 0) {
    return true;
  }
  return selected.every((value) => actual.includes(value));
}

/**
 * 按已选自由标签取交集。未选标签时不限制；勾选无标签时只保留没有任何正文标签的篇目。
 */
export function filterNotesByTags<T extends NoteTagCarrier>(
  notes: readonly T[],
  query: NoteTagQuery,
): T[] {
  return notes.filter((note) => {
    const tags = noteTagsOf(note);
    if (!matchesEvery(tags, query.tags)) {
      return false;
    }
    if (query.untagged && tags.length > 0) {
      return false;
    }
    return true;
  });
}

/**
 * 每篇对同一标签只计一次。
 */
export function noteTagCounts(notes: readonly NoteTagCarrier[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const note of notes) {
    for (const tag of noteTagsOf(note)) {
      counts.set(tag, (counts.get(tag) ?? 0) + 1);
    }
  }
  return counts;
}

/**
 * 按使用篇数从多到少。篇数相同再按中文排序。
 */
export function orderNoteTags(
  tags: readonly string[],
  counts: ReadonlyMap<string, number>,
): string[] {
  const unique: string[] = [];
  for (const tag of tags) {
    const text = tag.trim();
    if (!text || unique.includes(text)) {
      continue;
    }
    unique.push(text);
  }
  return unique.sort((a, b) => {
    const diff = (counts.get(b) ?? 0) - (counts.get(a) ?? 0);
    return diff !== 0 ? diff : a.localeCompare(b, "zh-CN");
  });
}

export type NoteTagOccupancy = {
  tags: ReadonlySet<string>;
  untagged: boolean;
};

/**
 * 统计这批心得里实际出现的正文标签，以及是否存在无标签篇目。
 */
export function occupiedNoteTags(notes: readonly NoteTagCarrier[]): NoteTagOccupancy {
  const tags = new Set<string>();
  let untagged = false;
  for (const note of notes) {
    const values = noteTagsOf(note);
    if (values.length === 0) {
      untagged = true;
      continue;
    }
    for (const tag of values) {
      tags.add(tag);
    }
  }
  return { tags, untagged };
}

/**
 * 筛选条取值。只聚合传入心得里出现过的自由标签；存在无标签篇目时，末尾加上语义项。
 */
export function noteTagFacetValues(notes: readonly NoteTagCarrier[]): string[] {
  const counts = noteTagCounts(notes);
  const ordered = orderNoteTags([...counts.keys()], counts);
  return occupiedNoteTags(notes).untagged ? [...ordered, noteUntaggedKey] : ordered;
}

/**
 * 筛选条上当前点中的取值。无标签用语义标识，不并进自由标签数组。
 */
export function selectedNoteTagFacets(query: NoteTagQuery): string[] {
  return query.untagged ? [...query.tags, noteUntaggedKey] : [...query.tags];
}

/**
 * 未点中、且当前结果里对不上的取值。已点中的留下，便于取消。
 */
export function disabledNoteTagFacets(
  values: readonly string[],
  occupied: NoteTagOccupancy,
  query: NoteTagQuery,
): string[] {
  const selected = new Set(selectedNoteTagFacets(query));
  return values.filter((value) => {
    if (selected.has(value)) {
      return false;
    }
    if (value === noteUntaggedKey) {
      return !occupied.untagged;
    }
    return !occupied.tags.has(value);
  });
}
