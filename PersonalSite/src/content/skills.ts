import skillsMd from "../../文稿/skills.md?raw";
import type { Locale, LocalizedString } from "../prefs/types";
import { tx } from "../prefs/tx";
import {
  MD_ENTRY,
  MD_SECTION,
  emptyToUndef,
  parseFields,
  parseInlineMarks,
  parseList,
  parseYesFlag,
  sectionBody,
  splitByHeading,
  type MdBlock,
} from "./pageMd";

export type SkillTool = {
  id: string;
  name: LocalizedString;
  icon: string;
  href?: string;
  blurb?: LocalizedString;
};

export type SkillDomainEntry = {
  toolId: string;
  score: number;
};

export type SkillDomain = {
  id: string;
  name: LocalizedString;
  deco: string;
  defaultOpen: boolean;
  entries: SkillDomainEntry[];
};

export type SkillLevel = {
  id: string;
  min: number;
  color: string;
  name: LocalizedString;
  deco: string;
  bullets: LocalizedString[];
};

export type SkillPageRecord = {
  title: LocalizedString;
  lead: LocalizedString;
  resumeLabel: LocalizedString;
  levels: SkillLevel[];
  tools: SkillTool[];
  domains: SkillDomain[];
};

const SCORE_LINE = /^[-*]\s+(.+?)\s+(\d+)\s*$/;

const COLOR_ALIAS: Record<string, string> = {
  蓝: "#3d7ab8",
  绿: "#3d8a62",
  黄: "#c4a24a",
  橙: "#d07a3a",
  红: "#c45c4a",
};

/**
 * 把技能 Markdown 收成页面记录。缺字段不写入。
 */
export function parseSkillsMd(source: string): SkillPageRecord {
  const sections = splitByHeading(source, MD_SECTION);
  const header = parseFields(sectionBody(sections, "页眉"));
  const tools = splitByHeading(sectionBody(sections, "工具"), MD_ENTRY).map((entry, index) => parseTool(entry, index));
  const levels = splitByHeading(sectionBody(sections, "等级"), MD_ENTRY)
    .map((entry, index) => parseLevel(entry, index))
    .sort((left, right) => right.min - left.min);
  const domains = splitByHeading(sectionBody(sections, "领域"), MD_ENTRY).map((entry, index) =>
    parseDomain(entry, index, tools),
  );

  return {
    title: localized(header.fields["页题"] || "技能", header.fields["英文页题"]),
    lead: header.fields["导语"] ?? "",
    resumeLabel: header.fields["简历链"] ?? "",
    levels,
    tools,
    domains,
  };
}

/**
 * 技能正本。改 `文稿/skills.md` 后保存即可更新页面。
 */
export const skillsPage: SkillPageRecord = parseSkillsMd(skillsMd);

export const skillLevels = skillsPage.levels;
export const skillTools = skillsPage.tools;
export const skillDomains = skillsPage.domains;

const toolDict = Object.fromEntries(skillTools.map((tool) => [tool.id, tool]));

/**
 * 按 id 取工具；缺项时返回空，调用方跳过渲染。
 */
export function findSkillTool(toolId: string): SkillTool | undefined {
  return toolDict[toolId];
}

/**
 * 由分值取档；低于最低档返回空。
 */
export function levelForScore(score: number): SkillLevel | undefined {
  return skillLevels.find((level) => score >= level.min);
}

/**
 * 默认展开的领域 id。
 */
export function defaultOpenSkillIds(): string[] {
  return skillDomains.filter((domain) => domain.defaultOpen).map((domain) => domain.id);
}

/**
 * 领域内可渲染的工具（跳过缺表项）。
 */
export function toolsOfDomain(domain: SkillDomain): { tool: SkillTool; score: number }[] {
  const rows: { tool: SkillTool; score: number }[] = [];
  for (const entry of domain.entries) {
    const tool = findSkillTool(entry.toolId);
    if (tool) {
      rows.push({ tool, score: entry.score });
    }
  }
  return rows;
}

/**
 * 工具显示名。
 */
export function skillToolName(tool: SkillTool, locale: Locale): string {
  return tx(tool.name, locale);
}

/**
 * 工具用途说明；缺描述则空串。
 */
export function skillToolBlurb(tool: SkillTool, locale: Locale): string {
  return tool.blurb ? tx(tool.blurb, locale) : "";
}

/**
 * 等级显示名。
 */
export function skillLevelName(level: SkillLevel, locale: Locale): string {
  return tx(level.name, locale);
}

/**
 * 当前语言下的一条说明（可含 `**加粗**`）。
 */
export function txSkillBullet(bullet: LocalizedString, locale: Locale): string {
  return tx(bullet, locale);
}

export { parseInlineMarks as parseSkillMarks };

function parseTool(entry: MdBlock, index: number): SkillTool {
  const fields = parseFields(entry.body);
  const id = fields.fields.id || slugify(entry.title) || `tool-${index + 1}`;
  const blurbZh = emptyToUndef(fields.fields["描述"]);
  return {
    id,
    name: localized(entry.title, fields.fields["英文"]),
    icon: resolveIcon(fields.fields["图标"], id),
    href: emptyToUndef(fields.fields["官网"]),
    blurb: blurbZh ? localized(blurbZh, fields.fields["英文描述"]) : undefined,
  };
}

function parseLevel(entry: MdBlock, index: number): SkillLevel {
  const parsed = parseFields(entry.body);
  const min = Number.parseInt(parsed.fields["下限"] ?? "", 10);
  const zhBullets = parseList(parsed.rest);
  const enBullets = parseList(parsed.fields["英文说明"] ?? "");
  const bullets: LocalizedString[] = zhBullets.map((line, bulletIndex) => {
    const en = enBullets[bulletIndex];
    return en ? { "zh-CN": line, en } : line;
  });

  return {
    id: parsed.fields.id || slugify(parsed.fields["英文"] || entry.title) || `level-${index + 1}`,
    min: Number.isFinite(min) ? min : 0,
    color: resolveColor(parsed.fields["条色"]),
    name: localized(entry.title, parsed.fields["英文"]),
    deco: parsed.fields["短写"] ?? "",
    bullets,
  };
}

function parseDomain(entry: MdBlock, index: number, tools: SkillTool[]): SkillDomain {
  const parsed = parseFields(entry.body);
  const entries: SkillDomainEntry[] = [];
  for (const line of parsed.rest.split(/\r?\n/)) {
    const match = line.trim().match(SCORE_LINE);
    if (!match) {
      continue;
    }
    const tool = matchTool(tools, match[1] ?? "");
    const score = Number.parseInt(match[2] ?? "", 10);
    if (!tool || !Number.isFinite(score)) {
      continue;
    }
    entries.push({ toolId: tool.id, score });
  }

  return {
    id: parsed.fields.id || `skill-${slugify(entry.title) || index + 1}`,
    name: localized(entry.title, parsed.fields["英文"]),
    deco: parsed.fields["短写"] ?? "",
    defaultOpen: parseYesFlag(parsed.fields["展开"]),
    entries,
  };
}

function matchTool(tools: SkillTool[], token: string): SkillTool | undefined {
  const key = token.trim();
  const lower = key.toLowerCase();
  return tools.find((tool) => {
    if (tool.id === key || tool.id === lower) {
      return true;
    }
    const zh = typeof tool.name === "string" ? tool.name : tool.name["zh-CN"];
    const en = typeof tool.name === "string" ? "" : (tool.name.en ?? "");
    return zh === key || zh.toLowerCase() === lower || en.toLowerCase() === lower;
  });
}

function resolveIcon(raw: string | undefined, id: string): string {
  const value = (raw ?? "").trim() || `${id}.svg`;
  if (value.startsWith("/") || /^https?:/i.test(value)) {
    return value;
  }
  return `/skill-icons/${value.replace(/^skill-icons\//, "")}`;
}

function resolveColor(raw: string | undefined): string {
  const value = (raw ?? "").trim();
  if (!value) {
    return COLOR_ALIAS["绿"] ?? "#3d8a62";
  }
  if (value.startsWith("#")) {
    return value;
  }
  return COLOR_ALIAS[value] ?? value;
}

function localized(zh: string, en?: string): LocalizedString {
  const english = en?.trim();
  return english ? { "zh-CN": zh, en: english } : zh;
}

function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[#._]+/g, "")
    .replace(/\s+/g, "-")
    .replace(/[^a-z0-9-]/g, "");
}

