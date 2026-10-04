import type { SkinRecord } from "../prefs/types";
import { developIa } from "./develop";
import { strataIa } from "./strata";
import type { IaId, IaRecord } from "./types";

const iaRegistry: Record<string, IaRecord> = {
  [strataIa.id]: strataIa,
  [developIa.id]: developIa,
};

/**
 * 按 IA id 取结构；未知 id 回退层境档案树。
 */
export function getIa(id: IaId | undefined): IaRecord {
  if (id && iaRegistry[id]) {
    return iaRegistry[id];
  }
  return strataIa;
}

/**
 * 从皮肤记录读取 IA；兼容旧字段 layout。
 */
export function iaOfSkin(record: SkinRecord & { layout?: string }): IaRecord {
  return getIa(record.ia ?? (record.layout === "develop-editorial" ? "develop-editorial" : undefined));
}
