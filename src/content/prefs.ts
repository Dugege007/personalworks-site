import {
  DEFAULT_LAYOUT_ID,
  DEFAULT_SKIN_ID,
  type SkinRecord,
} from "../prefs/types";

/**
 * 全站皮肤注册表。选择窗只展示 enabled 项，按 order 升序。
 */
export const skinRegistry: SkinRecord[] = [
  {
    id: DEFAULT_SKIN_ID,
    name: { "zh-CN": "层境 STRATA", en: "STRATA" },
    layout: DEFAULT_LAYOUT_ID,
    order: 0,
    enabled: true,
    isDefault: true,
    preview: {
      swatches: ["#0B0D10", "#C4A574", "#7BA3A8", "#E8E6E1"],
    },
  },
];

/**
 * 返回当前启用的皮肤，按 order 升序。
 */
export function listEnabledSkins(): SkinRecord[] {
  return skinRegistry
    .filter((item) => item.enabled)
    .slice()
    .sort((a, b) => a.order - b.order);
}

/**
 * 无偏好访客使用的默认皮肤；表中必须且仅有一条 isDefault。
 */
export function findDefaultSkin(): SkinRecord {
  const enabledList = listEnabledSkins();
  const marked = enabledList.find((item) => item.isDefault);
  if (marked) {
    return marked;
  }
  const fallback = enabledList[0] ?? skinRegistry[0];
  if (!fallback) {
    throw new Error("皮肤注册表为空，无法确定默认皮肤");
  }
  return fallback;
}

/**
 * 按 id 查找仍启用的皮肤；未命中则返回空。
 */
export function findEnabledSkin(id: string): SkinRecord | undefined {
  return listEnabledSkins().find((item) => item.id === id);
}
