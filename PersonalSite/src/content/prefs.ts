import { type SkinRecord } from "../prefs/types";

/**
 * 全站皮肤注册表。默认皮肤与选择窗排序只维护本文件：
 * `isDefault` 全表仅一条 true，供无偏好访客与非法 id 回退；
 * `order` 决定选择窗升序。
 * 不把这两项写入 Cursor 规则或术语冻结表。
 * 改 `isDefault` 后须同步 `index.html` 引导脚本的 FALLBACK 与 `:root` 令牌。
 */
export const skinRegistry: SkinRecord[] = [
  {
    id: "develop",
    name: { "zh-CN": "显影 DEVELOP", en: "DEVELOP" },
    brand: { zh: "显影", deco: "DEVELOP" },
    ia: "develop-editorial",
    order: 0,
    enabled: true,
    isDefault: true,
    preview: {
      swatches: ["#E8EAED", "#2B4C7E", "#2F5D4A", "#16181C"],
    },
  },
  {
    id: "strata",
    name: { "zh-CN": "层境 STRATA", en: "STRATA" },
    brand: { zh: "层境", deco: "STRATA" },
    ia: "strata-archive",
    order: 1,
    enabled: true,
    isDefault: false,
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
