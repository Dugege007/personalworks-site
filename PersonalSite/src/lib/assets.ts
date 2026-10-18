/**
 * 把内容层的 COS 相对键拼成可请求地址。
 * 本地 `VITE_ASSET_BASE` 为空时走 `/placeholders`；上线指向 CDN。
 * `/notes-assets/` 随站点 `public/` 进静态包，保持站点根路径，不改写到对象存储。
 */
export function assetUrl(path: string): string {
  const base = import.meta.env?.VITE_ASSET_BASE;
  return resolveAssetUrl(path, typeof base === "string" ? base : "");
}

/**
 * 纯路径解析，供页面和测试共用。`assetBase` 为 CDN 根；空字符串表示本地占位。
 */
export function resolveAssetUrl(path: string, assetBase: string): string {
  const raw = path.trim();
  if (!raw) {
    return "";
  }
  if (/^https?:\/\//i.test(raw) || raw.startsWith("/notes-assets/")) {
    return raw;
  }
  const relative = raw.replace(/^\/+/, "").replace(/^placeholders\//, "");
  const base = assetBase.trim().replace(/\/$/, "");
  if (!base) {
    return `/placeholders/${relative}`;
  }
  return `${base}/${relative}`;
}
