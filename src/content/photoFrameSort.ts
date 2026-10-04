/** 照片总览的排序键。星级没有方向。 */
export type PhotoFrameSortKey = "time" | "stars";

export type PhotoFrameSortDir = "asc" | "desc";

export type PhotoFrameSortRule = {
  key: PhotoFrameSortKey;
  dir: PhotoFrameSortDir;
};

const timeDesc: PhotoFrameSortRule = { key: "time", dir: "desc" };

/**
 * 解析总览排序。缺省与旧的 desc 都是时间从后到前；asc 仍是从前到后。
 */
export function parsePhotoFrameSort(raw: string | null): PhotoFrameSortRule[] {
  if (!raw || raw === "desc") {
    return [{ ...timeDesc }];
  }
  if (raw === "asc") {
    return [{ key: "time", dir: "asc" }];
  }
  const rules: PhotoFrameSortRule[] = [];
  const seen = new Set<PhotoFrameSortKey>();
  for (const part of raw.split(",")) {
    const token = part.trim();
    if (!token) {
      continue;
    }
    if (token === "stars" || token === "stars.desc" || token === "stars.asc") {
      if (!seen.has("stars")) {
        seen.add("stars");
        rules.push({ key: "stars", dir: "desc" });
      }
      continue;
    }
    const time = /^time\.(asc|desc)$/.exec(token);
    if (time && !seen.has("time")) {
      seen.add("time");
      rules.push({ key: "time", dir: time[1] === "asc" ? "asc" : "desc" });
    }
  }
  return rules.length > 0 ? rules : [{ ...timeDesc }];
}

/**
 * 写回查询串。缺省时间倒序省略参数；只有时间正序仍写 asc。
 */
export function writePhotoFrameSort(rules: readonly PhotoFrameSortRule[]): string | null {
  if (rules.length === 1 && rules[0]?.key === "time" && rules[0].dir === "desc") {
    return null;
  }
  if (rules.length === 1 && rules[0]?.key === "time" && rules[0].dir === "asc") {
    return "asc";
  }
  return rules.map((rule) => (rule.key === "stars" ? "stars" : `time.${rule.dir}`)).join(",");
}

/**
 * 收起态文案。单键时间沿用原来的「从前到后 / 从后到前」。
 */
export function photoFrameSortCaption(rules: readonly PhotoFrameSortRule[]): string {
  if (rules.length === 1 && rules[0]?.key === "time") {
    return rules[0].dir === "asc" ? "时间：从前到后" : "时间：从后到前";
  }
  if (rules.length === 1 && rules[0]?.key === "stars") {
    return "星级：从大到小";
  }
  const short: Record<PhotoFrameSortKey, string> = { time: "时间", stars: "星级" };
  return rules.map((rule, index) => `${index + 1} ${short[rule.key]}`).join(" · ");
}

/**
 * 点名称：未入列则追加，已入列则移除。去空后回到时间从后到前。
 */
export function togglePhotoFrameSort(
  rules: readonly PhotoFrameSortRule[],
  key: PhotoFrameSortKey,
  timeDir: PhotoFrameSortDir,
): PhotoFrameSortRule[] {
  if (rules.some((rule) => rule.key === key)) {
    const next = rules.filter((rule) => rule.key !== key);
    return next.length > 0 ? [...next] : [{ ...timeDesc }];
  }
  return [...rules, { key, dir: key === "stars" ? "desc" : timeDir }];
}

/**
 * 两条静帧按已入列键比较。星级高的在前。平局交给调用处的标题与原序。
 */
export function comparePhotoFrameFacts(
  left: { stars: number; time: string },
  right: { stars: number; time: string },
  rules: readonly PhotoFrameSortRule[],
): number {
  for (const rule of rules) {
    const diff =
      rule.key === "stars"
        ? right.stars - left.stars
        : rule.dir === "asc"
          ? left.time.localeCompare(right.time)
          : right.time.localeCompare(left.time);
    if (diff !== 0) {
      return diff;
    }
  }
  // 时间未入列时仍按从后到前，避免只按星级时同星落回从前到后的原序。
  if (!rules.some((rule) => rule.key === "time")) {
    const timeDiff = right.time.localeCompare(left.time);
    if (timeDiff !== 0) {
      return timeDiff;
    }
  }
  return 0;
}

/**
 * 只翻转已入列的时间方向。星级调用本函数也不变。
 */
export function flipPhotoFrameTime(rules: readonly PhotoFrameSortRule[]): PhotoFrameSortRule[] {
  return rules.map((rule) =>
    rule.key === "time" ? { key: "time", dir: rule.dir === "asc" ? "desc" : "asc" } : rule,
  );
}
