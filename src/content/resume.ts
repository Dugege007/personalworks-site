import { profile } from "./site";
import resumeMd from "../../文稿/resume.md?raw";
import type { LocalizedString } from "../prefs/types";
import {
  MD_ENTRY,
  MD_SECTION,
  emptyToUndef,
  firstParagraph,
  parseFields,
  parseInlineMarks,
  parseList,
  parseYesFlag,
  sectionBody,
  splitByHeading,
} from "./pageMd";

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

/**
 * 把履历 Markdown 收成页面记录。缺字段不写入。
 */
export function parseResumeMd(source: string): ResumeRecord {
  const sections = splitByHeading(source, MD_SECTION);
  const extras = parseFields(sectionBody(sections, "补充"));
  const notes = extras.fields["笔记"] ? [extras.fields["笔记"]] : undefined;

  return {
    summary: firstParagraph(sectionBody(sections, "简介")),
    jobs: splitByHeading(sectionBody(sections, "经历"), MD_ENTRY).map((entry, index) => {
      const fields = parseFields(entry.body);
      const summary = fields.fields["说明"] || firstParagraph(fields.rest);
      return {
        id: fields.fields.id || `exp-${index + 1}`,
        role: entry.title,
        org: emptyToUndef(fields.fields["组织"]),
        start: emptyToUndef(fields.fields["起始"]),
        end: emptyToUndef(fields.fields["结束"]),
        summary,
        collapsedByDefault: parseYesFlag(fields.fields["折叠"]),
      };
    }),
    schools: splitByHeading(sectionBody(sections, "教育"), MD_ENTRY).map((entry, index) => {
      const fields = parseFields(entry.body);
      return {
        id: fields.fields.id || `edu-${index + 1}`,
        school: fields.fields["学校"] || entry.title,
        degree: fields.fields["学位"] ?? "",
        years: fields.fields["年份"] ?? "",
        note: emptyToUndef(fields.fields["说明"] || firstParagraph(fields.rest)),
      };
    }),
    languages: parseResumeList(sectionBody(sections, "语言")),
    tools: parseTools(sectionBody(sections, "工具")),
    notes,
    blogHref: emptyToUndef(extras.fields["博客"]),
    blogLabel: emptyToUndef(extras.fields["博客名"]),
  };
}

/**
 * 履历正本。改 `文稿/resume.md` 后保存即可更新页面。
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
 * 履历语言列：跳过以句号收尾的说明句。
 */
function parseResumeList(body: string): string[] {
  return parseList(body).filter((item) => !item.endsWith("。"));
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

export type ResumeMark = {
  text: string;
  bold?: boolean;
};

/**
 * 把 `**加粗**` 收成可渲染片段，其它标记仍作原文。
 */
export function parseResumeMarks(source: string): ResumeMark[] {
  return parseInlineMarks(source);
}
