/** 与 index.html 引导脚本共用。alias 为曾用名，legal 为现用名。 */
export const PROFILE_NAME_KEY = "strata.profile-name";

export type ProfileNameMode = "alias" | "legal";

type NameStorage = Pick<Storage, "getItem" | "setItem">;

/**
 * 把本地记录解析成姓名模式。只有 legal 视为现用名，其余回到曾用名。
 */
export function parseProfileNameMode(raw: string | null): ProfileNameMode {
  return raw === "legal" ? "legal" : "alias";
}

/**
 * 浏览器标签页标题。姓名与「个人网站」之间用连字符。
 */
export function siteDocumentTitle(name: string): string {
  return `${name}-个人网站`;
}

/**
 * 在曾用名与现用名之间对调。
 */
export function toggleProfileNameMode(mode: ProfileNameMode): ProfileNameMode {
  return mode === "legal" ? "alias" : "legal";
}

/**
 * 读取本地记住的姓名模式。读不到或值非法时用曾用名。
 */
export function readProfileNameMode(storage: NameStorage | null = browserStorage()): ProfileNameMode {
  try {
    return parseProfileNameMode(storage?.getItem(PROFILE_NAME_KEY) ?? null);
  } catch {
    return "alias";
  }
}

/**
 * 把姓名模式写入本地，供下次进入站点时沿用。
 */
export function writeProfileNameMode(mode: ProfileNameMode, storage: NameStorage | null = browserStorage()): void {
  try {
    storage?.setItem(PROFILE_NAME_KEY, mode);
  } catch {
    // 隐私模式拒绝写入时，本次会话仍使用内存中的选择。
  }
}

/**
 * 把标签页标题改成当前姓名对应的站点名。
 */
export function applyProfileDocumentTitle(name: string): void {
  if (typeof document === "undefined") {
    return;
  }
  document.title = siteDocumentTitle(name);
}

function browserStorage(): NameStorage | null {
  try {
    return typeof localStorage === "undefined" ? null : localStorage;
  } catch {
    return null;
  }
}
