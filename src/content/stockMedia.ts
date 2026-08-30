/**
 * 栏目常驻占位图。路径为 `{key}/stock/01.webp` 起，共三张。
 * 正式作品入库后覆盖 `{key}/{id}/01.webp`，不改动 stock。
 * 缺图或加载失败时回退到本表。
 *
 * 本轮来源：Unsplash、Pexels、Wikimedia Commons、0 A.D. 官网截图。详见 `00A` 3.8。
 */
export function stockPlaceholderSrc(channel: string, slot: number): string {
  const index = ((Math.max(1, slot) - 1) % 3) + 1;
  return `${channel}/stock/${String(index).padStart(2, "0")}.webp`;
}

/**
 * 作品封面：优先用内容层 src，否则按序号取该栏目 stock。
 */
export function workCoverSrc(
  work: { channel: string; media: Array<{ src?: string }> },
  index = 0,
): string {
  return work.media.find((item) => item.src)?.src ?? stockPlaceholderSrc(work.channel, index + 1);
}

/**
 * 层境首页框景：每段三格对应相关细目的 stock。
 */
export const homeFrameSrcs: Record<string, string[]> = {
  "twin-sim": [
    stockPlaceholderSrc("digital-twin", 1),
    stockPlaceholderSrc("line-sim", 1),
    stockPlaceholderSrc("digital-twin", 3),
  ],
  "game-dev": [
    stockPlaceholderSrc("game-dev", 1),
    stockPlaceholderSrc("game-dev", 2),
    stockPlaceholderSrc("game-dev", 3),
  ],
  "landscape-arch": [
    stockPlaceholderSrc("landscape-rendering", 1),
    stockPlaceholderSrc("landscape-cds", 1),
    stockPlaceholderSrc("landscape-rendering", 3),
  ],
  photo: [
    stockPlaceholderSrc("landscape-photo", 1),
    stockPlaceholderSrc("humanist-photo", 1),
    stockPlaceholderSrc("portrait-photo", 1),
  ],
};
