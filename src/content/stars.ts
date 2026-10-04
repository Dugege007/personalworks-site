/**
 * 单条媒体的星级。缺字段、0 与越界都是 0。
 */
export function starsOf(media: { stars?: number } | null | undefined): number {
  const value = media?.stars;
  return value === 1 || value === 2 || value === 3 || value === 4 || value === 5 ? value : 0;
}

/**
 * 随机展示权重。0 星不进入候选。
 */
export function starDrawWeight(stars: number): number {
  if (stars === 5) {
    return 1;
  }
  if (stars === 4) {
    return 0.8;
  }
  if (stars === 3) {
    return 0.6;
  }
  if (stars === 2) {
    return 0.4;
  }
  if (stars === 1) {
    return 0.2;
  }
  return 0;
}

/**
 * 内容层静帧的抽图项。0 星返回空，调用处不得补回。
 */
export function contentDraw(
  src: string | undefined,
  media: { stars?: number } | null | undefined,
): { src: string; weight: number } | null {
  if (!src) {
    return null;
  }
  const weight = starDrawWeight(starsOf(media));
  if (weight <= 0) {
    return null;
  }
  return { src, weight };
}

/**
 * 没有星级槽时权重为 1。对象上写了 stars 之后走星级表，0 星不进候选。
 */
export function slottedDraw(
  src: string | undefined,
  stars: number | undefined,
): { src: string; weight: number } | null {
  if (!src) {
    return null;
  }
  if (stars == null) {
    return { src, weight: 1 };
  }
  return contentDraw(src, { stars });
}
