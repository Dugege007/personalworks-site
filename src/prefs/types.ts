export type Locale = "zh-CN" | "en";
export type SkinId = string;
export type LayoutId = string;

export type LocalizedString = string | { "zh-CN": string; en?: string };

export type SkinRecord = {
  id: SkinId;
  name: LocalizedString;
  layout: LayoutId;
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
export const DEFAULT_SKIN_ID: SkinId = "strata";
export const DEFAULT_LOCALE: Locale = "zh-CN";
export const DEFAULT_LAYOUT_ID: LayoutId = "strata-scroll";
