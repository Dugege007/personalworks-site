import type { Locale, LocalizedString } from "./types";

/**
 * 按当前语言取值；纯字符串视为中文正本，英文缺失时回退中文。
 */
export function tx(value: LocalizedString, locale: Locale): string {
  if (typeof value === "string") {
    return value;
  }
  if (locale === "en" && value.en) {
    return value.en;
  }
  return value["zh-CN"];
}
