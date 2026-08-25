import { findDefaultSkin, findEnabledSkin } from "../content/prefs";
import {
  DEFAULT_LOCALE,
  PREFS_KEY,
  PREFS_VERSION,
  type Locale,
  type PrefsV1,
  type SkinId,
} from "./types";

const LOCALES: Locale[] = ["zh-CN", "en"];

/**
 * 将皮肤 id 写到 html[data-skin]，供 CSS 选择器立即生效。
 */
export function applySkinToDom(skin: SkinId): void {
  document.documentElement.dataset.skin = skin;
}

/**
 * 解析本地偏好；缺键、损坏或字段不合法时返回空。
 */
export function readPrefs(): PrefsV1 | null {
  try {
    const raw = localStorage.getItem(PREFS_KEY);
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (!isPrefsV1(parsed)) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

/**
 * 写入 PrefsV1。皮肤切换与后续语言切换共用同一存储键。
 */
export function writePrefs(prefs: PrefsV1): void {
  localStorage.setItem(PREFS_KEY, JSON.stringify(prefs));
}

/**
 * 校验皮肤 id 是否仍启用；非法则回退默认皮肤。
 */
export function resolveSkinId(skin: string | undefined): SkinId {
  if (skin && findEnabledSkin(skin)) {
    return skin;
  }
  return findDefaultSkin().id;
}

/**
 * 校验语言码；非法则回退中文。本阶段不改界面语言，仅保持字段合法。
 */
export function resolveLocale(locale: string | undefined): Locale {
  if (locale && LOCALES.includes(locale as Locale)) {
    return locale as Locale;
  }
  return DEFAULT_LOCALE;
}

/**
 * 读取并校准偏好。无记录不写本地；损坏或非法 id 则回退默认并回写。
 */
export function loadResolvedPrefs(): PrefsV1 {
  const raw = localStorage.getItem(PREFS_KEY);
  const stored = readPrefs();
  const prefs: PrefsV1 = {
    v: PREFS_VERSION,
    skin: resolveSkinId(stored?.skin),
    locale: resolveLocale(stored?.locale),
  };
  if (raw && (!stored || stored.skin !== prefs.skin || stored.v !== PREFS_VERSION)) {
    writePrefs(prefs);
  }
  return prefs;
}

function isPrefsV1(value: unknown): value is PrefsV1 {
  if (typeof value !== "object" || value === null) {
    return false;
  }
  const record = value as Record<string, unknown>;
  return record.v === PREFS_VERSION && typeof record.skin === "string" && typeof record.locale === "string";
}
