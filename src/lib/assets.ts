/** 资源前缀：本地为空时走站点内占位；上线指向 CDN。 */
export function assetUrl(path: string): string {
  const base = import.meta.env.VITE_ASSET_BASE ?? "";
  if (!base) {
    return path;
  }
  const normalizedBase = base.replace(/\/$/, "");
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}
