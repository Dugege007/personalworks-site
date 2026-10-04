export type PathAndSearch = {
  pathname: string;
  search: string;
};

/**
 * 把带查询的 href 拆成 pathname 与 search（search 含问号）。
 */
export function splitHref(href: string): PathAndSearch {
  const q = href.indexOf("?");
  if (q < 0) {
    return { pathname: href, search: "" };
  }
  return { pathname: href.slice(0, q), search: href.slice(q) };
}

/**
 * 只接受本站总览或拍摄列表路径，避免把外来 state 当返回地址。
 */
export function readCatalogFromState(
  from: string | undefined,
  catalogPath: string | readonly string[],
): string | undefined {
  if (!from || !from.startsWith("/") || from.startsWith("//")) {
    return undefined;
  }
  const pathOnly = from.split("?")[0] ?? "";
  const allowed = typeof catalogPath === "string" ? [catalogPath] : catalogPath;
  return allowed.includes(pathOnly) ? from : undefined;
}
