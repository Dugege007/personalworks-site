export type DatedRecord = {
  id: string;
  title: string;
  capturedOn?: string;
  startedOn?: string;
  year?: string;
};

const DATE_PATTERN = /^(\d{4})(?:-(\d{2})(?:-(\d{2}))?)?$/;

/**
 * 把日期收成可比较的 `YYYY-MM-DD`。缺月日记为 00。非法片段跳过。
 */
function comparableDate(candidates: readonly (string | undefined)[]): string {
  for (const candidate of candidates) {
    if (!candidate || !DATE_PATTERN.test(candidate)) {
      continue;
    }
    const [, year, month, day] = DATE_PATTERN.exec(candidate) ?? [];
    if (!month) {
      return `${year}-00-00`;
    }
    const monthNum = Number(month);
    if (monthNum < 1 || monthNum > 12) {
      continue;
    }
    if (!day) {
      return `${year}-${month}-00`;
    }
    const dayNum = Number(day);
    const daysInMonth = new Date(Date.UTC(Number(year), monthNum, 0)).getUTCDate();
    if (dayNum < 1 || dayNum > daysInMonth) {
      continue;
    }
    return `${year}-${month}-${day}`;
  }
  return "";
}

/**
 * 取得可比较的有效日期。完整拍摄日期优先，其次为项目日期与年份。
 */
export function effectiveDate(value: DatedRecord): string {
  return comparableDate([value.capturedOn, value.startedOn, value.year]);
}

/**
 * 作品开始日期。只认 `startedOn`，其次年份。不使用拍摄日 `capturedOn`。
 */
export function startedOnDate(value: DatedRecord): string {
  return comparableDate([value.startedOn, value.year]);
}

/**
 * 排序用的时刻。有拍摄时刻用拍摄时刻，否则把开始日期放到当天零点。
 */
export function photoTimeKey(takenAt: string, startedOn: string): string {
  if (takenAt) {
    return takenAt;
  }
  return startedOn ? `${startedOn}T00:00:00` : "";
}

/**
 * 按时刻比较。空时刻在倒序时排在末尾。
 */
export function comparePhotoTime(left: string, right: string, direction: "asc" | "desc"): number {
  return direction === "asc" ? left.localeCompare(right) : right.localeCompare(left);
}

/**
 * 按开始日期比较。同日按标题与 id 稳定排序。无日期在倒序时排在末尾。
 */
export function compareByStartedOn<T extends DatedRecord>(
  a: T,
  b: T,
  direction: "asc" | "desc",
): number {
  const left = startedOnDate(a);
  const right = startedOnDate(b);
  const dateDiff = direction === "asc" ? left.localeCompare(right) : right.localeCompare(left);
  if (dateDiff !== 0) {
    return dateDiff;
  }
  const titleDiff = a.title.localeCompare(b.title, "zh-CN");
  return titleDiff !== 0 ? titleDiff : a.id.localeCompare(b.id);
}

/**
 * 按有效日期倒序；缺失日期排在末尾，同日按标题与 id 稳定排序。
 */
export function sortByEffectiveDateDescending<T extends DatedRecord>(values: readonly T[]): T[] {
  return [...values].sort((a, b) => {
    const dateDiff = effectiveDate(b).localeCompare(effectiveDate(a));
    if (dateDiff !== 0) {
      return dateDiff;
    }
    const titleDiff = a.title.localeCompare(b.title, "zh-CN");
    return titleDiff !== 0 ? titleDiff : a.id.localeCompare(b.id);
  });
}
