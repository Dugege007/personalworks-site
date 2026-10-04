/**
 * 按对象键扩展名判定媒类。MP4 记为视频并带上约定封面键。
 */
export function toListedMedia(src: string, label: string): {
  kind: "image" | "video";
  src: string;
  label: string;
  poster?: string;
} {
  return /\.mp4$/i.test(src)
    ? { kind: "video", src, poster: src.replace(/\.mp4$/i, ".poster.webp"), label }
    : { kind: "image", src, label };
}
