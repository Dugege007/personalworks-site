export type StudioAlias = {
  folder: string;
  zh: string;
};

/**
 * 任职过的设计单位夹 → 网页简称。不是开发商，也不是地点。未列入的夹不出标签。
 */
export const studioAliases: readonly StudioAlias[] = [
  { folder: "上海道田景观工程咨询有限公司", zh: "道田景观" },
  { folder: "上海日清景观设计有限公司", zh: "日清景观" },
  { folder: "洛阳市古建园林设计院", zh: "洛阳古建" },
];

/** 栏目下已点名的公司夹；超过一家才在年份后出简称。 */
const studioFoldersByChannel: Record<string, readonly string[]> = {
  "landscape-rendering": ["上海道田景观工程咨询有限公司"],
  "landscape-cds": [
    "洛阳市古建园林设计院",
    "上海道田景观工程咨询有限公司",
    "上海日清景观设计有限公司",
  ],
};

/**
 * 该栏目是否在年份后展示公司简称。
 */
export function channelShowsStudioAlias(channel: string): boolean {
  return (studioFoldersByChannel[channel]?.length ?? 0) > 1;
}

/**
 * 从作品字段或投放夹路径解析公司简称。
 */
export function resolveStudioAlias(work: {
  studioAlias?: string;
  stageFolder?: string;
}): string | undefined {
  const written = work.studioAlias?.trim();
  if (written) {
    return written;
  }
  if (!work.stageFolder) {
    return undefined;
  }
  const parts = work.stageFolder.split(/[\\/]/);
  for (const studio of studioAliases) {
    if (parts.includes(studio.folder)) {
      return studio.zh;
    }
  }
  return undefined;
}

/**
 * 访客页用的公司简称；单公司栏目不返回。
 */
export function studioAliasForDisplay(work: {
  channel: string;
  studioAlias?: string;
  stageFolder?: string;
}): string | undefined {
  return channelShowsStudioAlias(work.channel) ? resolveStudioAlias(work) : undefined;
}
