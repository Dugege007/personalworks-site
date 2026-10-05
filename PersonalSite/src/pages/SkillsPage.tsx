import { useState, type CSSProperties, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { glossaryEntryFor } from "../content/glossary";
import { glossaryBook } from "../content/glossaryDoc";
import { TermLabel, TermText } from "../components/TermText";
import {
  defaultOpenSkillIds,
  levelForScore,
  parseSkillMarks,
  skillDomains,
  skillLevelName,
  skillToolBlurb,
  skillToolName,
  skillsPage,
  toolsOfDomain,
  txSkillBullet,
  type SkillLevel,
  type SkillTool,
} from "../content/skills";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
import { usePrefs } from "../prefs/PrefsProvider";
import { tx } from "../prefs/tx";
import type { Locale } from "../prefs/types";
import "../styles/skills.css";

/**
 * 技能页：按工作领域展开，图标配熟练度条。
 */
export function SkillsPage() {
  const { locale } = usePrefs();
  const reduced = usePrefersReducedMotion();
  const [openIds, setOpenIds] = useState<string[]>(defaultOpenSkillIds);

  const toggle = (id: string) => {
    setOpenIds((cur) => (cur.includes(id) ? cur.filter((item) => item !== id) : [...cur, id]));
  };

  return (
    <div className="skills" data-theme={lexicon.profileSkills.key}>
      <header className="skills-head">
        <h1>{tx(skillsPage.title, locale)}</h1>
        {skillsPage.lead ? (
          <p className="skills-lead">
            <TermText text={tx(skillsPage.lead, locale)} />
          </p>
        ) : null}
        {skillsPage.resumeLabel ? (
          <Link className="skills-cross" to={`/${lexicon.profileResume.key}`}>
            {tx(skillsPage.resumeLabel, locale)}
          </Link>
        ) : null}
      </header>

      <div className="skills-list">
        {skillDomains.map((domain) => {
          const open = openIds.includes(domain.id);
          const rows = toolsOfDomain(domain);
          const domainName = tx(domain.name, locale);

          return (
            <section key={domain.id} className={`skills-domain${open ? " is-open" : ""}`}>
              <h2>
                <button type="button" aria-expanded={open} onClick={() => toggle(domain.id)}>
                  <span className="skills-domain-titles">
                    <span className="skills-domain-name">{domainName}</span>
                  </span>
                  {open ? null : (
                    <span className="skills-domain-preview" aria-hidden="true">
                      {rows.map(({ tool }) => (
                        <SkillMark key={tool.id} tool={tool} name={skillToolName(tool, locale)} compact />
                      ))}
                    </span>
                  )}
                </button>
              </h2>
              {open ? (
                <div className={`skills-body${reduced ? " is-static" : ""}`}>
                  <ul className="skills-meters">
                    {rows.map(({ tool, score }) => (
                      <li key={tool.id}>
                        <SkillMeter tool={tool} score={score} locale={locale} />
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </section>
          );
        })}
      </div>
    </div>
  );
}

function SkillMeter({ tool, score, locale }: { tool: SkillTool; score: number; locale: Locale }) {
  const level = levelForScore(score);
  const name = skillToolName(tool, locale);
  const blurb = skillToolBlurb(tool, locale);
  const glossaryEntry = glossaryEntryFor(glossaryBook, skillLookupNames(tool));
  const fill = Math.min(100, Math.max(0, score));
  const grade = level ? skillLevelName(level, locale) : "";
  const nameLabel = <div className="skill-name">{name}</div>;

  return (
    <div className="skill-meter">
      <SkillMark tool={tool} name={name} />
      <div className="skill-copy">
        {glossaryEntry ? (
          <TermLabel label={name} entry={glossaryEntry} plain className="skill-name" />
        ) : blurb ? (
          <HoverTip variant="name" panel={<p className="skill-tip-line">{blurb}</p>}>
            {nameLabel}
          </HoverTip>
        ) : (
          nameLabel
        )}
        {level ? (
          <>
            <HoverTip panel={<LevelPanel level={level} locale={locale} />}>
              <div
                className="skill-bar"
                style={
                  {
                    "--skill-fill": `${fill}%`,
                    "--skill-color": level.color,
                  } as CSSProperties
                }
                role="meter"
                aria-label={`${name} ${grade}`}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={fill}
              >
                <span className="skill-bar-fill" />
              </div>
            </HoverTip>
            <div className="skill-grade">{grade}</div>
          </>
        ) : null}
      </div>
    </div>
  );
}

function skillLookupNames(tool: SkillTool): string[] {
  const name = tool.name;
  if (typeof name === "string") {
    return [name];
  }
  return [name["zh-CN"], name.en ?? ""].filter((item) => item.length > 0);
}

function SkillMark({ tool, name, compact }: { tool: SkillTool; name: string; compact?: boolean }) {
  const img = <img src={tool.icon} alt="" width={compact ? 16 : 32} height={compact ? 16 : 32} />;
  const className = `skill-mark${compact ? " is-compact" : ""}`;

  if (!compact && tool.href) {
    return (
      <a className={className} href={tool.href} target="_blank" rel="noreferrer noopener" aria-label={name}>
        {img}
      </a>
    );
  }

  return (
    <span className={className}>
      {img}
      {compact ? <span className="skills-sr">{name}</span> : null}
    </span>
  );
}

function LevelPanel({ level, locale }: { level: SkillLevel; locale: Locale }) {
  return (
    <div className="skill-tip-lines">
      {level.bullets.map((bullet, index) => (
        <p key={index} className="skill-tip-line">
          {parseSkillMarks(txSkillBullet(bullet, locale)).map((mark, markIndex) =>
            mark.bold ? <strong key={`${mark.text}-${markIndex}`}>{mark.text}</strong> : mark.text,
          )}
        </p>
      ))}
    </div>
  );
}

function HoverTip({
  panel,
  children,
  variant,
}: {
  panel: ReactNode;
  children: ReactNode;
  variant?: "name";
}) {
  return (
    <span className={`skill-hot${variant === "name" ? " is-name" : ""}`} tabIndex={0}>
      {children}
      <span className="skill-tip" role="tooltip">
        <span className="skill-tip-card">{panel}</span>
      </span>
    </span>
  );
}
