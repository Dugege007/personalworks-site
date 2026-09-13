export type DatedRecord = {
  id: string;
  title: string;
  capturedOn?: string;
  startedOn?: string;
  year?: string;
};

const DATE_PATTERN = /^(\d{4})(?:-(\d{2})(?:-(\d{2}))?)?$/;

/**
 * 取得可比较的有效日期。完整拍摄日期优先，其次为项目日期与年份。
 */
export function effectiveDate(value: DatedRecord): string {
  for (const candidate of [value.capturedOn, value.startedOn, value.year]) {
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
