/**
 * 一组图框共用的换图规则。
 * 一个计时器按固定间隔只换一格，从左到右；每格自己记最近看过的图。
 * 间隔与不重复张数由调用处传入。换张用与头图相同的淡入，不带头图播放时的缓放。
 */
export type ComboRelayOptions = {
  /** 一格换完，到下一格开始换的固定间隔。 */
  intervalMs: number;
  /** 含当前张在内，本格不重复的张数。图池更少时收到池大小减一。 */
  noRepeat: number;
};

/** 首页切片、作品墙四门。 */
export const comboRelay4s = {
  intervalMs: 4000,
  noRepeat: 5,
} as const satisfies ComboRelayOptions;

/** 首页精选、摄影五门。 */
export const comboRelay3s = {
  intervalMs: 3000,
  noRepeat: 5,
} as const satisfies ComboRelayOptions;

/**
 * 到点只换一格，从左到右。
 * 鼠标停着的格、以及 canAdvance 为假的格都跳过。没有可换的格时返回 -1。
 */
export function nextComboIndex(
  from: number,
  skip: number | null,
  count: number,
  canAdvance?: (index: number) => boolean,
): number {
  if (count < 1) {
    return -1;
  }
  let index = ((from % count) + count) % count;
  for (let step = 0; step < count; step += 1) {
    const open = index !== skip && (canAdvance?.(index) ?? true);
    if (open) {
      return index;
    }
    index = (index + 1) % count;
  }
  return -1;
}

/**
 * 记下刚离开的一张，只保留不重复窗口里当前张以外的部分。
 */
export function pushComboRecent(recent: readonly string[], current: string, noRepeat: number): string[] {
  const keep = Math.max(noRepeat, 1) - 1;
  return [...recent, current].filter(Boolean).slice(-keep);
}

/** 一条可抽取的画面。权重小于等于 0 的不进入候选。 */
export type DrawSrc = {
  src: string;
  weight: number;
};

/**
 * 没有星级槽的固定图。缺路径时返回空。
 */
export function fixedDraw(src: string | undefined): DrawSrc | null {
  if (!src) {
    return null;
  }
  return { src, weight: 1 };
}

/**
 * 按 src 去重，同一路径保留较大权重，并丢掉非正权重。
 */
export function uniqueDraws(items: readonly (DrawSrc | null | undefined)[]): DrawSrc[] {
  const weightOf = new Map<string, number>();
  const order: string[] = [];
  for (const item of items) {
    if (!item?.src || item.weight <= 0) {
      continue;
    }
    const prev = weightOf.get(item.src);
    if (prev === undefined) {
      order.push(item.src);
      weightOf.set(item.src, item.weight);
      continue;
    }
    if (item.weight > prev) {
      weightOf.set(item.src, item.weight);
    }
  }
  return order.map((src) => ({ src, weight: weightOf.get(src) ?? 0 }));
}

/**
 * 按权重抽一张。近窗规则与 pickComboSrc 相同；权重为 0 的项先剔除。
 */
export function pickWeightedSrc(
  pool: readonly DrawSrc[],
  recent: readonly string[],
  noRepeat: number,
  random: () => number = Math.random,
): string {
  const open = uniqueDraws(pool);
  if (open.length === 0) {
    return "";
  }
  if (open.length === 1) {
    return open[0]?.src ?? "";
  }
  const limit = Math.min(noRepeat, open.length - 1);
  const forbidden = new Set(recent.filter(Boolean).slice(-limit));
  const candidates = open.filter((item) => !forbidden.has(item.src));
  const last = recent.filter(Boolean).at(-1);
  const relaxed = candidates.length > 0 ? candidates : open.filter((item) => item.src !== last);
  const bag = relaxed.length > 0 ? relaxed : open;
  return takeWeighted(bag, random);
}

/**
 * 在正权重上取一点，沿当前顺序落入对应画面。
 */
function takeWeighted(bag: readonly DrawSrc[], random: () => number): string {
  const total = bag.reduce((sum, item) => sum + item.weight, 0);
  if (total <= 0) {
    return bag[0]?.src ?? "";
  }
  let cursor = random() * total;
  for (const item of bag) {
    cursor -= item.weight;
    if (cursor < 0) {
      return item.src;
    }
  }
  return bag[bag.length - 1]?.src ?? "";
}

/**
 * 从图池随机取一张。recent 末尾若干张（含调用方放进来的当前张）不重复。
 * 候选为空时只避开最近一张。
 */
export function pickComboSrc(pool: readonly string[], recent: readonly string[], noRepeat: number): string {
  if (pool.length === 0) {
    return "";
  }
  if (pool.length === 1) {
    return pool[0] ?? "";
  }
  const limit = Math.min(noRepeat, pool.length - 1);
  const forbidden = new Set(recent.filter(Boolean).slice(-limit));
  const candidates = pool.filter((src) => !forbidden.has(src));
  const last = recent.filter(Boolean).at(-1);
  const bag = candidates.length > 0 ? candidates : pool.filter((src) => src !== last);
  return bag[Math.floor(Math.random() * bag.length)] ?? pool[0] ?? "";
}
