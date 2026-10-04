/** 与 index.html 引导脚本的文件名一致。层境用原来的 favicon.svg，显影用同构图的另一份文件。 */
const FAVICON_HREF: Record<string, string> = {
  develop: "/favicon-develop.svg",
  strata: "/favicon.svg",
};

const DEFAULT_FAVICON_HREF = FAVICON_HREF.develop;

/**
 * 当前皮肤的标签页图标文件。未知皮肤回显影。
 */
export function faviconHrefForSkin(skin: string): string {
  return FAVICON_HREF[skin] ?? DEFAULT_FAVICON_HREF;
}

/**
 * 把标签页图标换成当前皮肤的文件。换节点，浏览器才会丢掉上一张。
 */
export function applySkinFavicon(skin: string): void {
  if (typeof document === "undefined") {
    return;
  }
  const href = faviconHrefForSkin(skin);
  document.querySelectorAll("link[rel='icon'], link[rel='shortcut icon']").forEach((node) => node.remove());
  const link = document.createElement("link");
  link.rel = "icon";
  link.type = "image/svg+xml";
  link.href = href;
  document.head.append(link);
}
