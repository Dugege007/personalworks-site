export type WorkChannel =
  | "twin"
  | "factory"
  | "render"
  | "construction"
  | "landscape-photo"
  | "portrait";

export type WorkMedia = {
  kind: "image" | "video";
  label: string;
  src?: string;
};

export type WorkConsent = "granted" | "denied" | "pending";

export type WorkRecord = {
  id: string;
  channel: WorkChannel;
  title: string;
  year: string;
  summary: string;
  body: string;
  media: WorkMedia[];
  tags?: string[];
  role?: string;
  lineType?: string;
  metrics?: string;
  clientAlias?: string;
  sheetType?: string;
  placeAlias?: string;
  exifLite?: string;
  consent?: WorkConsent;
  displayName?: string;
};

function images(...labels: string[]): WorkMedia[] {
  return labels.map((label) => ({ kind: "image", label }));
}

export const placeholderWorks: WorkRecord[] = [
  {
    id: "sample-1",
    channel: "twin",
    title: "总图叠合",
    year: "2024",
    tags: ["场地", "分层"],
    role: "场景搭建与数据分层",
    summary: "把总图、建档与测点收进同一套可量测的层。",
    body: "占位正文。先叠合场地总图，再把运行层与测点层对齐。后续替换为项目说明与脱敏截图。",
    media: [...images("总图", "分层", "测点"), { kind: "video", label: "漫游切片 · 静音点击播放" }],
  },
  {
    id: "sample-2",
    channel: "twin",
    title: "运行剖切",
    year: "2024",
    tags: ["剖切"],
    role: "运行态说明",
    summary: "沿一条剖切看设备如何进入与离开视野。",
    body: "占位正文。剖切用于说明运行关系，不在浏览器里驱动仿真引擎。",
    media: images("剖切 A", "剖切 B", "注释层"),
  },
  {
    id: "sample-3",
    channel: "twin",
    title: "测点归档",
    year: "2023",
    tags: ["测点"],
    role: "测点编目",
    summary: "把散落的测点收成可检索的档案编号。",
    body: "占位正文。测点以编号归档，详情页只展示已脱敏的位置别名。",
    media: images("编号墙", "点位"),
  },
  {
    id: "sample-1",
    channel: "factory",
    title: "工位节拍",
    year: "2024",
    tags: ["节拍"],
    lineType: "装配线",
    metrics: "节拍 48s · 纯展示",
    summary: "把工位节拍放慢，用来看堵塞如何形成。",
    body: "占位正文。强调工位、节拍与等待，而不是地球级孪生。视频默认静音。",
    media: [...images("工位", "节拍板"), { kind: "video", label: "节拍回放 · 静音点击播放" }],
  },
  {
    id: "sample-2",
    channel: "factory",
    title: "物流回路",
    year: "2024",
    lineType: "循环物流",
    metrics: "回路 3 段",
    summary: "物料在回路里对位，而不是在地图上飘。",
    body: "占位正文。物流回路用水平导轨的方式阅读，不提供在线调参。",
    media: images("回路", "缓冲位", "回流"),
  },
  {
    id: "sample-3",
    channel: "factory",
    title: "瓶颈回放",
    year: "2023",
    lineType: "瓶颈观察",
    summary: "回放一次堵塞，用来说明观察方法。",
    body: "占位正文。瓶颈回放只作结果说明，不接入实时数据。",
    media: images("堵塞前", "堵塞中"),
  },
  {
    id: "sample-1",
    channel: "render",
    title: "主视角",
    year: "2022",
    role: "方案与空间叙事",
    clientAlias: "某庭院（别名）",
    summary: "空间先被框住，再被光慢慢填满。",
    body: "占位正文。效果图序列说明空间层次；客户真名默认不展示。",
    media: images("主视角", "近景", "林缘"),
  },
  {
    id: "sample-2",
    channel: "render",
    title: "黄昏庭院",
    year: "2021",
    role: "效果图",
    clientAlias: "某宅院（别名）",
    summary: "用黄昏金把庭院的一层光留下来。",
    body: "占位正文。宽画幅在手机上改为纵向堆叠，避免横向撑破视口。",
    media: images("黄昏", "水面", "廊"),
  },
  {
    id: "sample-3",
    channel: "render",
    title: "林缘断面",
    year: "2021",
    role: "断面叙事",
    summary: "用断面说明林缘如何把园子托住。",
    body: "占位正文。断面用于说明，不是施工依据。",
    media: images("断面", "层次"),
  },
  {
    id: "sample-1",
    channel: "construction",
    title: "总图选页",
    year: "2020",
    sheetType: "总图",
    summary: "脱敏后的总图选页，只展示图纸能力。",
    body: "占位正文。正式施工图须为网图版本后再入库。本页不作施工依据。",
    media: images("总图选页", "图框", "索引"),
  },
  {
    id: "sample-2",
    channel: "construction",
    title: "铺装详图",
    year: "2020",
    sheetType: "详图",
    summary: "铺装节点的脱敏选页。",
    body: "占位正文。详图仅展示表达能力，不提供下载原图。",
    media: images("铺装", "收边"),
  },
  {
    id: "sample-3",
    channel: "construction",
    title: "索引裁切",
    year: "2019",
    sheetType: "索引",
    summary: "从完整图册裁出的索引页。",
    body: "占位正文。索引用于说明图种组织，不作为出图模板。",
    media: images("索引"),
  },
  {
    id: "sample-1",
    channel: "landscape-photo",
    title: "山脊",
    year: "2025",
    placeAlias: "某山脊（别名）",
    exifLite: "1/250 · f/8 · ISO 100",
    summary: "把观看停在光线刚好够用的那一瞬。",
    body: "界面让于画面。地点只用别名；EXIF 已去掉 GPS。",
    media: images("山脊", "云隙"),
  },
  {
    id: "sample-2",
    channel: "landscape-photo",
    title: "水面",
    year: "2024",
    placeAlias: "某湖（别名）",
    exifLite: "1/60 · f/11 · ISO 64",
    summary: "水面把天光收成一层。",
    body: "占位正文。系列图控制在数张以内，不塞原片。",
    media: images("水面", "倒影", "岸"),
  },
  {
    id: "sample-3",
    channel: "landscape-photo",
    title: "雾色",
    year: "2024",
    placeAlias: "某谷（别名）",
    summary: "雾把层次减到还能走的程度。",
    body: "占位正文。无 EXIF 时不留空行。",
    media: images("雾"),
  },
  {
    id: "sample-1",
    channel: "portrait",
    title: "留白",
    year: "2025",
    consent: "granted",
    summary: "已授权肖像的抽象框景，不展示被摄者真名。",
    body: "占位正文。人像页信息量低于风光；未提供 displayName 时不写姓名。",
    media: images("留白", "侧光"),
  },
  {
    id: "sample-2",
    channel: "portrait",
    title: "侧光",
    year: "2024",
    consent: "granted",
    summary: "侧光把轮廓留下来，背景尽量空。",
    body: "占位正文。生产列表只收录 consent 为已授权的条目。",
    media: images("侧光"),
  },
  {
    id: "sample-3",
    channel: "portrait",
    title: "静场",
    year: "2024",
    consent: "granted",
    summary: "把人留在刚好够用的光线里。",
    body: "占位正文。下架时更换对象键并刷新 CDN，本轮无真实人脸。",
    media: images("静场", "手", "窗"),
  },
  {
    id: "held",
    channel: "portrait",
    title: "未授权条目",
    year: "2024",
    consent: "denied",
    summary: "不应出现在列表或详情。",
    body: "此条仅用于验证未授权不对外可见。",
    media: images("不应展示"),
  },
];

/**
 * 列出某细目下对访客可见的作品；未授权人像不进入列表。
 */
export function listPublishedWorks(channel: string): WorkRecord[] {
  return placeholderWorks.filter(
    (item) => item.channel === channel && (!item.consent || item.consent === "granted"),
  );
}

/**
 * 按细目与 id 查找可见作品。未知 id 或未授权人像一律视为不存在。
 */
export function findPublishedWork(channel: string, id: string): WorkRecord | undefined {
  const work = placeholderWorks.find((item) => item.channel === channel && item.id === id);
  if (!work) {
    return undefined;
  }
  if (work.consent && work.consent !== "granted") {
    return undefined;
  }
  return work;
}
