/**
 * 把内容层的 COS 相对键拼成可请求地址。
 * 本地 `VITE_ASSET_BASE` 为空时走 `/placeholders`；上线指向 CDN。
 */
export function assetUrl(path: string): string {
  const raw = path.trim();
  if (!raw) {
    return "";
  }
  if (/^https?:\/\//i.test(raw)) {
    return raw;
  }
  const relative = raw.replace(/^\/+/, "").replace(/^placeholders\//, "");
  const base = (import.meta.env.VITE_ASSET_BASE ?? "").replace(/\/$/, "");
  if (!base) {
    return `/placeholders/${relative}`;
  }
  return `${base}/${relative}`;
}
