/**
 * 栏目常驻占位图。路径为 `{key}/stock/01.webp` 起，共三张。
 * 正式作品入库后覆盖 `{key}/{id}/01.webp`，不改动 stock。
 * 缺图或加载失败时回退到本表。
 *
 * 本轮来源：Unsplash、Pexels、Wikimedia Commons、0 A.D. 官网截图。站名单见 `00A` 3.8；单张页面 URL 见 `Docs/参考资料/来路登记.md`。
 */
export function stockPlaceholderSrc(channel: string, slot: number): string {
  const index = ((Math.max(1, slot) - 1) % 3) + 1;
  return `${channel}/stock/${String(index).padStart(2, "0")}.webp`;
}

/**
 * 作品封面：优先静帧 src；仅视频时用伴生 poster。否则按序号取该栏目 stock。
 */
export function workCoverSrc(
  work: { channel: string; media: Array<{ src?: string; kind?: string; poster?: string }> },
  index = 0,
): string {
  const listed = work.media.filter((item) => item.src);
  const image = listed.find((item) => item.kind !== "video");
  if (image?.src) {
    return image.src;
  }
  const video = listed.find((item) => item.kind === "video" && item.poster);
  if (video?.poster) {
    return video.poster;
  }
  return listed[0]?.src ?? stockPlaceholderSrc(work.channel, index + 1);
}

/**
 * 层境首页框景：每段三格对应相关细目的 stock。
 */
export const homeFrameSrcs: Record<string, string[]> = {
  "twin-sim": [
    "digital-twin/akzonobel-shanghai-songjiang/01.webp",
    stockPlaceholderSrc("line-sim", 1),
    "digital-twin/weichai-spark-plug/01.webp",
  ],
  "game-dev": [
    "game-dev/antigravity/01.webp",
    "game-dev/3d-rpg/01.webp",
    "game-dev/classic-games/01.webp",
  ],
  "landscape-arch": [
    "landscape-rendering/harbin-jiangyufu/01.webp",
    stockPlaceholderSrc("landscape-cds", 1),
    "landscape-rendering/maoming/01.webp",
  ],
  photo: [
    "landscape-photo/zhoushan-miaozihu/01.webp",
    stockPlaceholderSrc("humanist-photo", 1),
    stockPlaceholderSrc("portrait-photo", 1),
  ],
};
