export type PhotoOrientation = "landscape" | "portrait" | "square";

/**
 * 按像素判定照片构图。宽高相等视为 1:1。
 */
export function classifyPhotoOrientation(width: number, height: number): PhotoOrientation {
  if (width === height) {
    return "square";
  }
  return width > height ? "landscape" : "portrait";
}

/**
 * 视口构图：宽大于高为横屏，高大于宽为竖屏，相等视为方屏。
 */
export function readViewportOrientation(width: number, height: number): PhotoOrientation {
  return classifyPhotoOrientation(width, height);
}

/**
 * 1:1 照片或方屏视口不限制；其余须与视口同向。
 */
export function photoFitsViewport(photo: PhotoOrientation, viewport: PhotoOrientation): boolean {
  if (photo === "square" || viewport === "square") {
    return true;
  }
  return photo === viewport;
}

export function heroFrameFitsViewport(
  frame: { width?: number; height?: number },
  viewport: PhotoOrientation,
): boolean {
  if (!frame.width || !frame.height) {
    return false;
  }
  return photoFitsViewport(classifyPhotoOrientation(frame.width, frame.height), viewport);
}
