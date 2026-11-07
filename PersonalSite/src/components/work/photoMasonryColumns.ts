/**
 * 与 photo-masonry.css 的列数断点一致。
 * 窄屏至少两列，不收到一列。一两项时仍为两列。
 * 宽度不超过 900 为两列，不低于 1400 为四列，其间为三列。
 */
export function photoMasonryColumnCount(itemCount: number, width: number): number {
  if (itemCount <= 2) {
    return 2;
  }
  if (width <= 900) {
    return 2;
  }
  if (width >= 1400) {
    return 4;
  }
  return 3;
}
