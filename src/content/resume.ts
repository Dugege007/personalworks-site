import { profile } from "./site";
import resumeMd from "./resume.md?raw";
import type { LocalizedString } from "../prefs/types";

export type ResumeJob = {
  id: string;
  org?: LocalizedString;
  role: LocalizedString;
  start?: string;
  end?: string;
  summary: LocalizedString;
  collapsedByDefault?: boolean;
};

export type ResumeSchool = {
  id: string;
  school: LocalizedString;
  degree: LocalizedString;
  years: string;
  note?: LocalizedString;
};

export type ResumeRecord = {
  summary: LocalizedString;
  jobs: ResumeJob[];
  schools: ResumeSchool[];
  languages: LocalizedString[];
  tools: string[];
  notes?: LocalizedString[];
  blogHref?: string;
  blogLabel?: LocalizedString;
};

const SECTION = /^##\s+(.+?)\s*$/;
const ENTRY = /^###\s+(.+?)\s*$/;
const FIELD = /^(组织|起始|结束|说明|折叠|id|学位|年份|学校|笔记|博客名|博客)[：:]\s*(.*)$/;

type MdBlock = {
  title: string;
  body: string;
};

/**
 * 把履历 Markdown 收成页面记录。缺字段不写入。
 */
export function parseResumeMd(source: string): ResumeRecord {
  const sections = splitByHeading(source, SECTION);
  const extras = parseFields(sectionBody(sections, "补充"));
  const notes = extras.fields["笔记"] ? [extras.fields["笔记"]] : undefined;

  return {
    summary: firstParagraph(sectionBody(sections, "简介")),
    jobs: splitByHeading(sectionBody(sections, "经历"), ENTRY).map((entry, index) => {
      const fields = parseFields(entry.body);
      const summary = fields.fields["说明"] || firstParagraph(fields.rest);
      return {
        id: fields.fields.id || `exp-${index + 1}`,
        role: entry.title,
        org: emptyToUndef(fields.fields["组织"]),
        start: emptyToUndef(fields.fields["起始"]),
        end: emptyToUndef(fields.fields["结束"]),
        summary,
        collapsedByDefault: parseFoldFlag(fields.fields["折叠"]),
      };
    }),
    schools: splitByHeading(sectionBody(sections, "教育"), ENTRY).map((entry, index) => {
      const fields = parseFields(entry.body);
      return {
        id: fields.fields.id || `edu-${index + 1}`,
        school: fields.fields["学校"] || entry.title,
        degree: fields.fields["学位"] ?? "",
        years: fields.fields["年份"] ?? "",
        note: emptyToUndef(fields.fields["说明"] || firstParagraph(fields.rest)),
      };
    }),
    languages: parseList(sectionBody(sections, "语言")),
    tools: parseTools(sectionBody(sections, "工具")),
    notes,
    blogHref: emptyToUndef(extras.fields["博客"]),
    blogLabel: emptyToUndef(extras.fields["博客名"]),
  };
}

/**
 * 履历正本。改 `resume.md` 后保存即可更新页面。
 */
export const resume: ResumeRecord = parseResumeMd(resumeMd);

/**
 * 把起止年拼成履历用的连字符区间。缺起止则不显示。
 */
export function formatResumeSpan(start?: string, end?: string): string {
  if (!start && !end) {
    return "";
  }
  if (start && !end) {
    return `${start}-至今`;
  }
  if (!start && end) {
    return end;
  }
  return `${start}-${end}`;
}

/**
 * 简历页身份行，与形象同源。
 */
export function resumeIdentity(): string {
  return profile.identity.split(" / ").join(" · ");
}

/**
 * 按指定标题正则切开 Markdown。
 */
function splitByHeading(source: string, pattern: RegExp): MdBlock[] {
  const list: MdBlock[] = [];
  let title = "";
  const lines: string[] = [];

  const flush = () => {
    if (!title) {
      lines.length = 0;
      return;
    }
    list.push({ title, body: lines.join("\n").trim() });
    lines.length = 0;
  };

  for (const line of source.replace(/^\uFEFF/, "").split(/\r?\n/)) {
    const match = line.match(pattern);
    if (match?.[1]) {
      flush();
      title = match[1].trim();
      continue;
    }
    lines.push(line);
  }
  flush();
  return list;
}

/**
 * 取同名二级节正文；未写则空串。
 */
function sectionBody(sections: MdBlock[], title: string): string {
  return sections.find((item) => item.title === title)?.body ?? "";
}

/**
 * 抽出「键：值」行，其余留给段落。
 */
function parseFields(body: string): { fields: Record<string, string>; rest: string } {
  const fields: Record<string, string> = {};
  const rest: string[] = [];
  let lastKey = "";
  for (const line of body.split(/\r?\n/)) {
    const match = line.match(FIELD);
    if (match?.[1]) {
      lastKey = match[1];
      fields[lastKey] = (match[2] ?? "").trim();
      continue;
    }
    if (lastKey === "说明") {
      const next = line.trim();
      if (next) {
        fields[lastKey] = [fields[lastKey], next].filter(Boolean).join("\n");
      }
      continue;
    }
    rest.push(line);
  }
  return { fields, rest: rest.join("\n").trim() };
}

/**
 * 取第一段非空正文，忽略无序列表标记。
 */
function firstParagraph(body: string): string {
  const chunks = body
    .split(/\n\s*\n/)
    .map((chunk) => chunk.replace(/^[-*]\s+/gm, "").trim())
    .filter(Boolean);
  return chunks[0] ?? "";
}

/**
 * 语言等短列：每行一项，可带减号。
 */
function parseList(body: string): string[] {
  return body
    .split(/\r?\n/)
    .map((line) => line.replace(/^[-*]\s+/, "").trim())
    .filter((item) => item && !item.endsWith("。"));
}

/**
 * 工具名：顿号、逗号或换行均可。
 */
function parseTools(body: string): string[] {
  return body
    .split(/[、,，\n]/)
    .map((item) => item.replace(/^[-*]\s+/, "").trim())
    .filter((item) => item && !item.endsWith("。"));
}

function emptyToUndef(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

/**
 * `折叠：是` 为默认收起；`否` 或未写则默认展开。
 */
function parseFoldFlag(value: string | undefined): boolean {
  const normalized = value?.trim();
  return normalized === "是" || normalized === "true" || normalized === "1";
}

export type ResumeMark = {
  text: string;
  bold?: boolean;
};

/**
 * 把 `**加粗**` 收成可渲染片段，其它标记仍作原文。
 */
export function parseResumeMarks(source: string): ResumeMark[] {
  const list: ResumeMark[] = [];
  const pattern = /\*\*([^*]+)\*\*/g;
  let cursor = 0;
  let match = pattern.exec(source);
  while (match) {
    if (match.index > cursor) {
      list.push({ text: source.slice(cursor, match.index) });
    }
    list.push({ text: match[1] ?? "", bold: true });
    cursor = match.index + match[0].length;
    match = pattern.exec(source);
  }
  if (cursor < source.length) {
    list.push({ text: source.slice(cursor) });
  }
  return list;
}
