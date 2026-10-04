/**
 * 内容补丁隔离样例。结构对齐 PersonalWorks works.ts 的 album / listed / cover / covered。
 */

type WorkChannel =
  | "landscape-rendering"
  | "landscape-photo"
  | "digital-twin"
  | "line-sim"
  | "landscape-cds";

function album(channel: WorkChannel, id: string, count: number, ext = "jpg") {
  return Array.from({ length: count }, (_, index) => {
    const slot = String(index + 1).padStart(2, "0");
    return {
      kind: "image" as const,
      label: `效果图 ${slot}`,
      src: `${channel}/${id}/${slot}.${ext}`,
    };
  });
}

function listed(entries: Array<[src: string, label: string]>) {
  return entries.map(([src, label]) => ({ kind: "image" as const, src, label }));
}

function images(...labels: string[]) {
  return labels.map((label) => ({ kind: "image" as const, label }));
}

function cover(channel: WorkChannel, id: string, label: string) {
  return { kind: "image" as const, label, src: `${channel}/${id}/01.webp` };
}

function covered(channel: WorkChannel, id: string, labels: string[]) {
  return labels.map((label, index) => ({
    kind: "image" as const,
    label,
    ...(index === 0 ? { src: `${channel}/${id}/01.webp` } : {}),
  }));
}

export const placeholderWorks = [
  {
    id: "xiaowayao",
    channel: "landscape-rendering",
    title: "丰台小瓦窑",
    media: album("landscape-rendering", "xiaowayao", 3),
  },
  {
    id: "huaian-fukang-15",
    channel: "landscape-rendering",
    title: "淮安富康城15#",
    media: listed([
      ["landscape-rendering/huaian-fukang/01.jpg", "效果图 01"],
      ["landscape-rendering/huaian-fukang/02.jpg", "效果图 02"],
      ["landscape-rendering/huaian-fukang-15/01.jpg", "效果图 03"],
    ]),
  },
  {
    id: "huaian-fukang-3",
    channel: "landscape-rendering",
    title: "淮安富康城3#",
    media: listed([
      ["landscape-rendering/huaian-fukang/01.jpg", "效果图 01"],
      ["landscape-rendering/huaian-fukang/07.jpg", "效果图 02"],
      ["landscape-rendering/huaian-fukang/08.jpg", "效果图 03"],
    ]),
  },
  {
    id: "sample-1",
    channel: "digital-twin",
    title: "总图叠合",
    media: [cover("digital-twin", "sample-1", "总图"), ...images("分层"), { kind: "video", label: "漫游切片 · 静音点击播放" }],
  },
  {
    id: "sample-2",
    channel: "digital-twin",
    title: "短片回归",
    media: listed([
      ["digital-twin/sample-2/01.webp", "总图"],
      ["digital-twin/sample-2/02.webp", "分层"],
    ]),
  },
  {
    id: "sample-3",
    channel: "digital-twin",
    title: "占位回填",
    media: [cover("digital-twin", "sample-3", "总图"), { kind: "video", label: "演示" }],
  },
  {
    id: "sample-1",
    channel: "landscape-photo",
    title: "山脊",
    media: [cover("landscape-photo", "sample-1", "山脊"), ...images("云隙")],
  },
  {
    id: "sample-2",
    channel: "landscape-photo",
    title: "水面",
    media: covered("landscape-photo", "sample-2", ["水面", "倒影", "岸"]),
  },
  {
    id: "sample-3",
    channel: "landscape-photo",
    title: "雾色",
    media: covered("landscape-photo", "sample-3", ["雾"]),
  },
  {
    id: "sample-1",
    channel: "line-sim",
    title: "工位节拍",
    media: [cover("line-sim", "sample-1", "工位"), ...images("节拍板"), { kind: "video", label: "节拍回放 · 静音点击播放" }],
  },
  {
    id: "sample-1",
    channel: "landscape-cds",
    title: "总图选页",
    media: covered("landscape-cds", "sample-1", ["总图选页", "图框", "索引"]),
  },
];
