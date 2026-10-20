/**
 * 简历打印态的纯转换：相对链接写成线上绝对地址，并按一页 A4 投递视图压缩正文。
 * 不读取 Markdown，页面把 `文稿/resume.md` 的解析结果传进来。
 */

export const RESUME_PRINT_ORIGIN = "https://duhongbo.com";

export const RESUME_PRINT_BLOG_HREF = "https://blog.csdn.net/Dugege007";

/** 网站上已公开、且允许写进一页纸的数字孪生客户。顺序与投递叙事一致。 */
export const PRINT_TWIN_CLIENT_IDS = [
  "akzonobel-shanghai-songjiang",
  "weichai-spark-plug",
  "ningbo-siweier",
  "shanghai-mansheng-packaging",
] as const;

const SECONDARY_SKILL = /Photoshop/;
const SITE_META_SKILL = /Vibe Coding|\[网站\]/;

export type PrintContactInput = {
  id: string;
  value?: string;
  href?: string;
};

export type PrintContact = {
  id: string;
  label: string;
  text: string;
  href?: string;
};

export type PrintJobInput = {
  id: string;
  role: string;
  org?: string;
  summary: string;
  span: string;
};

export type PrintWorkTitle = {
  id: string;
  title: string;
  channel: string;
};

const CONTACT_LABEL: Record<string, string> = {
  profilePhone: "手机",
  profileMail: "邮箱",
  profileBase: "现居地",
  profileGithub: "GitHub",
};

/**
 * 站内路径补成 https 绝对地址；`http://duhongbo.com` 升为 https。其余地址原样返回。
 */
export function toResumePrintHref(href: string, origin = RESUME_PRINT_ORIGIN): string {
  const trimmed = href.trim();
  if (trimmed.startsWith("/") && !trimmed.startsWith("//")) {
    return `${origin}${trimmed}`;
  }
  if (trimmed.startsWith("http://duhongbo.com")) {
    return `https://duhongbo.com${trimmed.slice("http://duhongbo.com".length)}`;
  }
  return trimmed;
}

/**
 * 笔记栏外链。稿面未写时仍保留已公开的 CSDN 地址。
 */
export function printBlogHref(href?: string): string {
  const next = href?.trim() ? toResumePrintHref(href) : "";
  return next || RESUME_PRINT_BLOG_HREF;
}

/**
 * 联系区全文。只取站点已有文字；微信与 QQ 仅有二维码，打印稿不放。
 * 手机号仅在形象数据里写了全文时出现。
 */
export function printContactLines(channels: PrintContactInput[]): PrintContact[] {
  const lines: PrintContact[] = [];
  for (const channel of channels) {
    const label = CONTACT_LABEL[channel.id];
    const text = channel.value?.trim() ?? "";
    if (!label || !text) {
      continue;
    }
    let href = channel.href?.trim() ? toResumePrintHref(channel.href) : undefined;
    if (!href && channel.id === "profileMail") {
      href = `mailto:${text}`;
    }
    if (!href && channel.id === "profilePhone") {
      href = `tel:${text.replace(/\s+/g, "")}`;
    }
    lines.push({ id: channel.id, label, text, href });
  }
  return lines;
}

/**
 * 标题里地点后缀不进一页纸的客户列举，避免和简历原文重复堆叠。
 */
export function shortClientTitle(title: string): string {
  const head = title.split("·")[0]?.trim() ?? "";
  return head || title.trim();
}

/**
 * 从公开作品记录里按固定顺序取出客户短名。站点未收录的项目不会被写进 PDF。
 */
export function digitalTwinClientNames(works: PrintWorkTitle[]): string[] {
  const names: string[] = [];
  for (const id of PRINT_TWIN_CLIENT_IDS) {
    const work = works.find((item) => item.id === id && item.channel === "digital-twin");
    if (!work) {
      continue;
    }
    const name = shortClientTitle(work.title);
    if (name && !names.includes(name)) {
      names.push(name);
    }
  }
  return names;
}

/**
 * 技能保留数字孪生主线，并留下景观施工图这一句。次要的后期软件与站点自述不进一页纸。
 */
export function selectPrintSkillItems(items: string[]): string[] {
  return items.filter((item) => !SECONDARY_SKILL.test(item) && !SITE_META_SKILL.test(item));
}

/**
 * 简介取第一句，作为页眉定位。
 */
export function printRoleLine(summary: string): string {
  return summary.split(/\n/).map((line) => line.trim()).find(Boolean) ?? "";
}

/**
 * 实习与古建园林院不进投递页。
 */
export function isOmittedPrintJob(job: { role: string; org?: string }): boolean {
  const org = job.org ?? "";
  return job.role.includes("实习") || org.includes("古建园林设计院");
}

/**
 * 默派段：客户名与网站公开作品对齐，ProdLineSim 写成自研并链到站内产线仿真索引，不写仓库地址。
 */
export function compressMopaiSummary(summary: string, lineSimHref: string, clientNames: string[]): string {
  let text = summary.replace(/\s*\[查看功能简介\]/g, "");
  text = text.replace(/[；;]\s*ProdLineSim\s*产线仿真项目\s*。?/g, "。");
  const detail = `自研产线仿真平台 ProdLineSim。\n[详见网站](${lineSimHref})`;
  if (text.includes("；自研开发产线仿真平台")) {
    text = text.replace("；自研开发产线仿真平台", `。\n${detail}`);
  } else if (text.includes("自研开发产线仿真平台")) {
    text = text.replace("自研开发产线仿真平台", detail);
  } else if (!text.includes(lineSimHref)) {
    text = `${text.trim()}\n${detail}。`;
  }
  const missing = clientNames.filter((name) => name && !text.includes(name));
  if (missing.length > 0 && text.includes("等多个项目")) {
    text = text.replace("等多个项目", `、${missing.join("、")}等多个项目`);
  }
  text = text.replace(/\s*(\[查看项目简介\]\([^)]+\))/g, "\n$1");
  return text.replace(/。{2,}/g, "。").replace(/[ \t]+。/g, "。").trim();
}

/**
 * 自学段只留获奖句。游戏 Demo 枚举与独立项目条不进一页纸。
 */
export function compressStudySummary(summary: string): string {
  const lines = summary.split(/\n/).map((line) => line.trim()).filter(Boolean);
  const award = lines.find((line) => line.includes("奖"));
  return award ?? lines[lines.length - 1] ?? summary.trim();
}

/**
 * 按组织把经历收成打印正文。景观两段保持稿面，已是两行量级。
 */
export function compressJobSummary(job: PrintJobInput, lineSimHref: string, clientNames: string[]): string {
  const org = job.org ?? "";
  if (org.includes("默派") || job.role.includes("数字孪生与产线仿真")) {
    return compressMopaiSummary(job.summary, lineSimHref, clientNames);
  }
  if (org.includes("自学") || org.includes("独立开发")) {
    return compressStudySummary(job.summary);
  }
  return job.summary.trim();
}
