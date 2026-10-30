/** 与 photo-masonry.css 的 `--photo-masonry-gap` 一致，样式尚未读到时用这个值。 */
export const PHOTO_MASONRY_GAP = 14;

export type ShortestColumnPlacement = {
  /** 从 0 起的列号。并列时取较小列号，即最左列。 */
  column: number;
  /** 卡片顶边距容器顶边的像素，已含此前各张与列间距。 */
  top: number;
};

export type ShortestColumnLayout = {
  placements: ShortestColumnPlacement[];
  /** 各列内容高度，不含列末多算的那一段间距。 */
  columnHeights: number[];
  containerHeight: number;
};

/**
 * 最短列贪心。下一项放入当前高度最小的列；多列同高时取最左列。
 * `itemHeights` 是卡片内容高度，不含列间距。放置后把间距计入列高，便于和下一张比较。
 * 高度不是正数时不占槽：仍记下坐标，但不推进列高，也不把间距算进去。
 */
export function placeShortestColumns(
  itemHeights: readonly number[],
  columnCount: number,
  gap = PHOTO_MASONRY_GAP,
): ShortestColumnLayout {
  const columns = Math.max(1, Math.floor(columnCount) || 1);
  const safeGap = gap > 0 && Number.isFinite(gap) ? gap : 0;
  const running = Array.from({ length: columns }, () => 0);
  const placements: ShortestColumnPlacement[] = [];

  for (const rawHeight of itemHeights) {
    let column = 0;
    for (let index = 1; index < columns; index += 1) {
      if (running[index] < running[column]) {
        column = index;
      }
    }
    const top = running[column];
    const itemHeight = rawHeight > 0 && Number.isFinite(rawHeight) ? rawHeight : 0;
    placements.push({ column, top });
    if (itemHeight === 0) {
      continue;
    }
    running[column] = top + itemHeight + safeGap;
  }

  const columnHeights = running.map((height) => (height > 0 ? height - safeGap : 0));
  const containerHeight = columnHeights.reduce((max, height) => Math.max(max, height), 0);
  return { placements, columnHeights, containerHeight };
}

/**
 * 还没测到高度、且尚未失败的卡片用已知高度的中位数占位，避免在高度仍为 0 时全部堆进最左列。
 * 一张都还没测到时，用 `unknownFallback`（通常是列宽）让各列先按并列规则从左向右铺开。
 * `failed` 与 `measured` 对齐。已失败且高度仍非正数时保留 0，不吃中位数，调用方据此跳过占槽。
 * 中位数只来自已经测到的正高度；尚未加载的 0 才用它。
 */
export function masonryHeightsForPlacement(
  measured: readonly number[],
  unknownFallback = 1,
  failed?: readonly boolean[],
): number[] {
  const known: number[] = [];
  for (const height of measured) {
    if (height > 0 && Number.isFinite(height)) {
      known.push(height);
    }
  }
  let fallback = unknownFallback > 0 && Number.isFinite(unknownFallback) ? unknownFallback : 1;
  if (known.length > 0) {
    known.sort((left, right) => left - right);
    fallback = known[Math.floor((known.length - 1) / 2)];
  }
  return measured.map((height, index) => {
    if (height > 0 && Number.isFinite(height)) {
      return height;
    }
    if (failed?.[index]) {
      return 0;
    }
    return fallback;
  });
}
