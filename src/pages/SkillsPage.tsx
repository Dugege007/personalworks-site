import { useState } from "react";
import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { skillDomains } from "../content/skills";
import { hrefForKind } from "../ia/href";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
import { usePrefs } from "../prefs/PrefsProvider";
import { tx } from "../prefs/tx";
import "../styles/skills.css";

/**
 * 技能页：按领域展开。减少动效时全部展开。
 */
export function SkillsPage() {
  const { locale, currentSkin } = usePrefs();
  const reduced = usePrefersReducedMotion();
  const firstId = skillDomains[0]?.id ?? "";
  const [openId, setOpenId] = useState(firstId);

  return (
    <div className="skills" data-theme={lexicon.profileSkills.key}>
      <header className="skills-head">
        <h1>{lexicon.profileSkills.zh}</h1>
        <p className="skills-lead">按领域看会什么，各带一句做过的事。</p>
        <Link className="skills-cross" to={`/${lexicon.profileResume.key}`}>
          {lexicon.profileResume.zh}
        </Link>
      </header>

      <div className="skills-list">
        {skillDomains.map((domain) => {
          const open = reduced || openId === domain.id;
          const workHref = domain.workKind
            ? hrefForKind(domain.workKind, currentSkin.ia)
            : (domain.workHrefFallback ?? "");
          const workLabel = domain.workLabel ? tx(domain.workLabel, locale) : "";

          return (
            <section key={domain.id} className={`skills-domain${open ? " is-open" : ""}`}>
              <h2>
                <button
                  type="button"
                  aria-expanded={open}
                  onClick={() => {
                    if (reduced) {
                      return;
                    }
                    setOpenId((cur) => (cur === domain.id ? "" : domain.id));
                  }}
                >
                  <span className="skills-domain-name">{tx(domain.name, locale)}</span>
                  <span className="skills-domain-deco">{domain.deco}</span>
                </button>
              </h2>
              {open ? (
                <div className="skills-body">
                  <ul className="skills-items">
                    {domain.items.map((item) => (
                      <li key={item.id}>
                        <strong>{tx(item.name, locale)}</strong>
                        <p>{tx(item.proof, locale)}</p>
                      </li>
                    ))}
                  </ul>
                  {domain.tools.length > 0 ? (
                    <ul className="skills-tools">
                      {domain.tools.map((tool) => (
                        <li key={tool}>{tool}</li>
                      ))}
                    </ul>
                  ) : null}
                  {workHref ? (
                    <Link className="skills-work" to={workHref}>
                      {workLabel}
                    </Link>
                  ) : null}
                </div>
              ) : null}
            </section>
          );
        })}
      </div>
    </div>
  );
}
