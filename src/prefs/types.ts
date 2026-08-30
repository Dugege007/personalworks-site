export type Locale = "zh-CN" | "en";
export type SkinId = string;
export type IaId = string;

export type LocalizedString = string | { "zh-CN": string; en?: string };

export type SkinRecord = {
  id: SkinId;
  name: LocalizedString;
  /** 顶栏中文名与装饰短写，随当前皮肤切换。 */
  brand: { zh: string; deco: string };
  ia: IaId;
  order: number;
  enabled: boolean;
  isDefault: boolean;
  preview: {
    swatches: string[];
    cover?: string;
  };
};

export type PrefsV1 = {
  v: 1;
  skin: SkinId;
  locale: Locale;
};

export const PREFS_KEY = "strata.prefs";
export const PREFS_VERSION = 1;
export const DEFAULT_LOCALE: Locale = "zh-CN";
