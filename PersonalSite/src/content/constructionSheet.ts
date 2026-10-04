import type { WorkMedia } from "./works";

const GENERIC_SHEET_LABEL = /^效果图\s*\d+$/;

/**
 * 图纸目录上的短标。自动生成的「效果图 NN」改用源文件名；已手写的图名保持原文。
 */
export function constructionSheetMark(item: Pick<WorkMedia, "label" | "src">, index: number): string {
  const label = item.label.trim();
  if (label && !GENERIC_SHEET_LABEL.test(label)) {
    return label;
  }
  const file = item.src?.split("/").pop() ?? "";
  const stem = file.replace(/\.[^.]+$/, "");
  if (stem) {
    return stem;
  }
  return String(index + 1).padStart(2, "0");
}

/**
 * 一套图纸里可挂到图台上的静帧。
 */
export function constructionSheets(media: readonly WorkMedia[]): WorkMedia[] {
  return media.filter((item) => item.kind !== "video" && Boolean(item.src));
}
