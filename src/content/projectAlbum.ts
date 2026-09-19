import { resolveMediaDisplayName } from "./copyDisplay.ts";
import type { WorkMedia } from "./works.ts";

export const TWIN_PREVIEW_LIMIT = 7;

export type ProjectAlbumShot = {
  src: string;
  label: string;
  kind?: "image" | "video";
  poster?: string;
  description?: string;
};

export type AlbumPreviewPlan = {
  visibleCount: number;
  overflow: boolean;
};

/**
 * 孪生 / 仿真列表预览：最多 7 格。超过 7 张静帧时末格改叠层。
 */
export function planAlbumPreview(imageCount: number, limit = TWIN_PREVIEW_LIMIT): AlbumPreviewPlan {
  if (imageCount <= limit) {
    return { visibleCount: imageCount, overflow: false };
  }
  return { visibleCount: limit, overflow: true };
}

/**
 * 把已列入内容池的媒体收成画册帧；无 src 的槽丢掉。
 */
export function toProjectAlbumShots(items: readonly WorkMedia[]): ProjectAlbumShot[] {
  return items.flatMap((item) =>
    item.src
      ? [
          {
            src: item.src,
            label: resolveMediaDisplayName(item),
            kind: item.kind,
            poster: item.poster,
            description: item.description,
          },
        ]
      : [],
  );
}
