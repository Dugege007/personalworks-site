import { profile } from "./site";
import resumeMd from "../../文稿/resume.md?raw";
import type { LocalizedString } from "../prefs/types";
import {
  MD_ENTRY,
  MD_FIELD,
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

export type ResumeSkillGroup = {
  id: string;
  title: string;
  items: string[];
};

export type ResumeLink = {
  text: string;
  href: string;
};

export type ResumeRecord = {
  summary: LocalizedString;
  jobs: ResumeJob[];
  schools: ResumeSchool[];
  languages: LocalizedString[];
  skills: ResumeSkillGroup[];
  skillLinks: ResumeLink[];
  blogHref?: string;
  blogLabel?: LocalizedString;
};

/**
 * 把履历 Markdown 收成页面记录。缺字段不写入。
 */
export function parseResumeMd(source: string): ResumeRecord {
  const sections = splitByHeading(source, MD_SECTION);
  const extras = parseFields(sectionBody(sections, "笔记"));

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
    ...parseSkillSection(sectionBody(sections, "技能") || sectionBody(sections, "工具")),
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
 * 技能栏。有三级标题时按方向分组；没有时，以「- 」开头的能力描述仍整句显示。
 * 单独成行的 `[文字](地址)` 不进条目，排在条目之后。
 * 两种都没有时，才按顿号、逗号拆成短名。
 */
function parseSkillSection(body: string): { skills: ResumeSkillGroup[]; skillLinks: ResumeLink[] } {
  const { prose, links } = splitSkillLinks(body);
  const entries = splitByHeading(prose, MD_ENTRY);
  if (entries.length > 0) {
    return {
      skills: entries
        .map((entry, index) => ({
          id: `skill-${index + 1}`,
          title: entry.title,
          items: parseSkillItems(entry.body),
        }))
        .filter((group) => group.items.length > 0),
      skillLinks: links,
    };
  }

  const items = parseSkillItems(skillListLines(prose));
  if (items.length > 0) {
    return { skills: [{ id: "skill-1", title: "", items }], skillLinks: links };
  }

  const names = parseSkillNames(prose);
  if (names.length === 0) {
    return { skills: [], skillLinks: links };
  }
  return { skills: [{ id: "skill-1", title: "", items: names }], skillLinks: links };
}

/**
 * 抽出整行都是链接的句子。其余正文留给条目。
 */
function splitSkillLinks(body: string): { prose: string; links: ResumeLink[] } {
  const linkLine = /^\[([^\]\n]+)\]\(([^)\s]+)\)$/;
  const links: ResumeLink[] = [];
  const kept: string[] = [];

  for (const line of body.split(/\r?\n/)) {
    const match = line.trim().match(linkLine);
    const text = match?.[1]?.trim() ?? "";
    const href = match?.[2]?.trim() ?? "";
    if (text && isResumeHref(href)) {
      links.push({ text, href });
      continue;
    }
    kept.push(line);
  }

  return { prose: kept.join("\n"), links };
}

/**
 * 没有三级标题时，只收列表行，说明句不进页面。
 */
function skillListLines(body: string): string {
  return body
    .split(/\r?\n/)
    .filter((line) => /^[-*]\s+/.test(line))
    .join("\n");
}

/**
 * 方向下的能力条目。只保留列表行，跳过「键：值」。
 */
function parseSkillItems(body: string): string[] {
  return parseList(body).filter((item) => !MD_FIELD.test(item));
}

/**
 * 技能短名：顿号、逗号或换行均可。
 */
function parseSkillNames(body: string): string[] {
  return body
    .split(/[、,，\n]/)
    .map((item) => item.replace(/^[-*]\s+/, "").trim())
    .filter((item) => item && !item.endsWith("。"));
}

export type ResumeMark = {
  text: string;
  bold?: boolean;
  href?: string;
};

const RESUME_LINK = /\[([^\]\n]+)\]\(([^)\s]+)\)/g;

/**
 * 把 `**加粗**` 与 `[文字](地址)` 收成可渲染片段。没有地址的方括号仍作原文。
 */
export function parseResumeMarks(source: string): ResumeMark[] {
  const list: ResumeMark[] = [];
  let cursor = 0;

  for (const match of source.matchAll(RESUME_LINK)) {
    const index = match.index ?? 0;
    if (index > cursor) {
      list.push(...parseInlineMarks(source.slice(cursor, index)));
    }
    const label = (match[1] ?? "").trim();
    const href = (match[2] ?? "").trim();
    if (label && isResumeHref(href)) {
      list.push({ text: label, href });
    } else {
      list.push(...parseInlineMarks(match[0]));
    }
    cursor = index + match[0].length;
  }

  if (cursor < source.length) {
    list.push(...parseInlineMarks(source.slice(cursor)));
  }
  return list;
}

/**
 * 站内路径或 http(s) 地址。拒绝协议相对地址。
 */
function isResumeHref(href: string): boolean {
  if (href.startsWith("/") && !href.startsWith("//")) {
    return true;
  }
  return href.startsWith("https://") || href.startsWith("http://");
}
