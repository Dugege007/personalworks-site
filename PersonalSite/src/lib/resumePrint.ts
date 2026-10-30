/**
 * 简历打印态：正文取当前文稿，站内链接补成线上绝对地址。
 * 一页放不下时按档位收行距、压缩实习与自学，再依次去掉 Github 与个人主页。
 */

export const RESUME_PRINT_ORIGIN = "https://duhongbo.com";

/** 投递稿手机号。公开形象页不写全文，只在打印稿出现。 */
export const PRINT_PHONE_LABEL = "手机号(同微信)";
export const PRINT_PHONE_TEXT = "15139219231";

export const PRINT_HOME_LABEL = "个人主页";
export const PRINT_HOME_TEXT = "www.duhongbo.com";
export const PRINT_HOME_HREF = "https://duhongbo.com";

/** 与 `resume-print.css` 的 `@page` 边距一致。打印态可用高度要扣掉上下边距。 */
export const PRINT_PAGE_HEIGHT_MM = 297;
export const PRINT_PAGE_MARGIN_MM = 12;

export type PrintContactSource = {
  mail?: string;
  githubText?: string;
  githubHref?: string;
};

export type PrintContactDrop = {
  github?: boolean;
  homepage?: boolean;
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

export type PrintFitLevel = {
  density: 0 | 1 | 2 | 3;
  compressSecondary: boolean;
  dropGithub: boolean;
  dropHomepage: boolean;
};

/**
 * 从疏到密。先收行距和栏间距，再压缩实习与自学，最后才删联系项。
 * Github 先于个人主页去掉。
 */
export const PRINT_FIT_LEVELS: readonly PrintFitLevel[] = [
  { density: 0, compressSecondary: false, dropGithub: false, dropHomepage: false },
  { density: 1, compressSecondary: false, dropGithub: false, dropHomepage: false },
  { density: 2, compressSecondary: false, dropGithub: false, dropHomepage: false },
  { density: 2, compressSecondary: true, dropGithub: false, dropHomepage: false },
  { density: 3, compressSecondary: true, dropGithub: false, dropHomepage: false },
  { density: 3, compressSecondary: true, dropGithub: true, dropHomepage: false },
  { density: 3, compressSecondary: true, dropGithub: true, dropHomepage: true },
];

/**
 * 下载文件名：当前页面显示的中文姓名，加当天日期。例：杜红勃简历-20261031.pdf。
 */
export function resumePdfFilename(displayName: string, date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${displayName}简历-${year}${month}${day}.pdf`;
}

/**
 * 打印对话框用来建议文件名的标题。不带扩展名，避免被存成 `.pdf.pdf`。
 */
export function resumePdfSaveTitle(displayName: string, date: Date): string {
  return resumePdfFilename(displayName, date).replace(/\.pdf$/i, "");
}

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
 * 打印态一页的可用高度（毫米）。屏幕预览含内边距，按整页 297mm 量；打印时边距在 `@page`。
 */
export function printSheetLimitMm(printMedia: boolean): number {
  if (!printMedia) {
    return PRINT_PAGE_HEIGHT_MM;
  }
  return PRINT_PAGE_HEIGHT_MM - PRINT_PAGE_MARGIN_MM * 2;
}

/**
 * 联系区按三列排。第三列第一行是现居地。邮箱、Github 取形象数据。
 * 个人主页与 Github 带超链接，邮箱和现居地只留文字。地方不够时先去掉 Github，再去掉个人主页。
 */
export function printContacts(source: PrintContactSource, drop: PrintContactDrop = {}): PrintContact[] {
  const lines: PrintContact[] = [
    { id: "phone", label: PRINT_PHONE_LABEL, text: PRINT_PHONE_TEXT },
  ];
  const mail = source.mail?.trim() ?? "";
  if (mail) {
    lines.push({ id: "mail", label: "邮箱", text: mail });
  }
  lines.push({ id: "base", label: "现居地", text: "上海 青浦区" });
  if (!drop.homepage) {
    lines.push({
      id: "homepage",
      label: PRINT_HOME_LABEL,
      text: PRINT_HOME_TEXT,
      href: PRINT_HOME_HREF,
    });
  }
  const githubText = source.githubText?.trim() ?? "";
  if (!drop.github && githubText) {
    const githubHref = source.githubHref?.trim() ?? "";
    lines.push({
      id: "github",
      label: "Github",
      text: githubText,
      href: githubHref ? toResumePrintHref(githubHref) : undefined,
    });
  }
  return lines;
}

/**
 * 页眉简介只留前两行。
 */
export function printIntroLines(summary: string, count = 2): string {
  return summary
    .split(/\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, count)
    .join("\n");
}

/**
 * 实习看职务或组织。打印稿的此前经历不收录实习。
 */
export function isPrintInternship(job: { role: string; org?: string }): boolean {
  return secondaryKind(job) === "intern";
}

/**
 * 实习看职务或组织；自学看组织里的「自学」。不按项目名单取舍。
 */
export function isPrintSecondaryJob(job: { role: string; org?: string }): boolean {
  return secondaryKind(job) !== "";
}

/**
 * 默认整段照录。仅在档位要求压缩时，实习留第一行，自学留含「奖」的行。
 */
export function printJobSummary(job: PrintJobInput, compressSecondary: boolean): string {
  const summary = job.summary.trim();
  if (!compressSecondary) {
    return summary;
  }
  const kind = secondaryKind(job);
  if (!kind) {
    return summary;
  }
  const lines = summary.split(/\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length === 0) {
    return summary;
  }
  if (kind === "study") {
    const awards = lines.filter((line) => line.includes("奖"));
    return (awards.length > 0 ? awards : [lines[0]]).join("\n");
  }
  return lines[0] ?? summary;
}

/**
 * 去掉加粗记号后再判断，避免文稿里的 `**` 挡住「实习」「自学」。
 */
function secondaryKind(job: { role: string; org?: string }): "" | "intern" | "study" {
  const role = (job.role ?? "").replace(/\*/g, "");
  const org = (job.org ?? "").replace(/\*/g, "");
  if (role.includes("实习") || org.includes("实习")) {
    return "intern";
  }
  if (org.includes("自学")) {
    return "study";
  }
  return "";
}
