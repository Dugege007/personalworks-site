import { useState } from "react";
import { Link } from "react-router-dom";
import { ProfileName } from "../components/ProfileName";
import { ContactIcons } from "../components/home/ContactIcons";
import { lexicon } from "../content/lexicon";
import {
  formatResumeSpan,
  parseResumeMarks,
  resume,
  resumeIdentity,
  type ResumeJob,
} from "../content/resume";
import type { Locale } from "../prefs/types";
import { profile } from "../content/site";
import { queryNotes } from "../ia/query";
import { assetUrl } from "../lib/assets";
import { tx } from "../prefs/tx";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/resume.css";

const RESUME_NOTE_LIMIT = 3;
const skillsHref = `/${lexicon.profileSkills.key}`;

/**
 * 简历单页：左栏钉住身份，右栏时间线。缺字段不渲染。
 */
export function ResumePage() {
  const { locale } = usePrefs();
  const [failed, setFailed] = useState(false);
  const portrait = !failed && profile.portraitSrc ? assetUrl(profile.portraitSrc) : "";
  const noteCards = queryNotes({ source: "notes" })
    .filter((note) => !note.draft)
    .slice(0, RESUME_NOTE_LIMIT);
  const blogLabel = resume.blogLabel ? tx(resume.blogLabel, locale) : "CSDN 笔记";
  const hasNotesCol = noteCards.length > 0 || Boolean(resume.blogHref);
  const hasExtras = resume.languages.length > 0 || resume.skills.length > 0 || hasNotesCol;

  return (
    <div className="resume" data-theme={lexicon.profileResume.key}>
      <aside className="resume-aside">
        <div className="resume-portrait">
          {portrait ? (
            <img
              src={portrait}
              alt={`${profile.name} / ${lexicon.profile.zh}`}
              onError={() => setFailed(true)}
            />
          ) : (
            <div className="resume-portrait-well" aria-hidden="true" />
          )}
        </div>
        <div className="resume-id">
          <p className="resume-deco">{lexicon.profileResume.deco}</p>
          <h1 className="resume-name">
            <ProfileName />
          </h1>
          <p className="resume-en">{profile.nameEn}</p>
          <p className="resume-role">{resumeIdentity()}</p>
          <ContactIcons channels={profile.contactChannels} />
        </div>
      </aside>

      <div className="resume-main">
        <section className="resume-block">
          <h2>简介</h2>
          <p className="resume-summary">{tx(resume.summary, locale)}</p>
        </section>

        {resume.jobs.length > 0 ? (
          <section className="resume-block">
            <h2>经历</h2>
            <ol className="resume-timeline">
              {resume.jobs.map((job, index) => (
                <ResumeJobItem key={job.id} job={job} index={index} locale={locale} />
              ))}
            </ol>
          </section>
        ) : null}

        {resume.schools.length > 0 ? (
          <section className="resume-block">
            <h2>教育</h2>
            <ul className="resume-schools">
              {resume.schools.map((school, index) => (
                <li key={school.id} style={{ ["--i" as string]: String(index) }}>
                  <div className="resume-job-head">
                    <div>
                      <strong>{tx(school.school, locale)}</strong>
                      <span className="resume-org">{tx(school.degree, locale)}</span>
                    </div>
                    <time>{school.years}</time>
                  </div>
                  {school.note ? <p>{tx(school.note, locale)}</p> : null}
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        {hasExtras ? (
          <section className="resume-block resume-extras">
            {resume.skills.length > 0 ? (
              <div>
                <h2>
                  <Link className="resume-heading-link" to={skillsHref}>
                    {lexicon.profileSkills.zh}
                  </Link>
                </h2>
                <ul className="resume-tools">
                  {resume.skills.map((skill) => (
                    <li key={skill}>
                      <Link to={skillsHref}>{skill}</Link>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            {resume.languages.length > 0 || hasNotesCol ? (
              <div className="resume-side">
                {resume.languages.length > 0 ? (
                  <div>
                    <h2>语言</h2>
                    <ul className="resume-langs">
                      {resume.languages.map((item) => (
                        <li key={tx(item, locale)}>{tx(item, locale)}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {hasNotesCol ? (
                  <div>
                    <h2>笔记</h2>
                    <ul className="resume-notes">
                      {noteCards.map((note) => (
                        <li key={note.slug}>
                          <Link to={`/${lexicon.notes.key}/${note.slug}`}>{note.title}</Link>
                        </li>
                      ))}
                      {resume.blogHref ? (
                        <li>
                          <a href={resume.blogHref} target="_blank" rel="noreferrer noopener">
                            {blogLabel}
                          </a>
                        </li>
                      ) : null}
                    </ul>
                  </div>
                ) : null}
              </div>
            ) : null}
          </section>
        ) : null}
      </div>
    </div>
  );
}

/**
 * 单条经历：点职务开合组织与说明；默认开合由稿面 `折叠` 决定。
 */
function ResumeJobItem({ job, index, locale }: { job: ResumeJob; index: number; locale: Locale }) {
  const [open, setOpen] = useState(!job.collapsedByDefault);
  const span = formatResumeSpan(job.start, job.end);
  const org = job.org ? tx(job.org, locale) : "";
  const role = tx(job.role, locale);
  const summary = tx(job.summary, locale);

  return (
    <li
      className={`resume-job${open ? " is-open" : ""}`}
      style={{ ["--i" as string]: String(index) }}
    >
      <button
        type="button"
        className="resume-job-toggle"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        <div className="resume-job-head">
          <div>
            <strong>
              {role}
              <i className="resume-job-chevron" aria-hidden="true" />
            </strong>
            {open && org ? (
              <span className="resume-org">
                <ResumeMarks text={org} />
              </span>
            ) : null}
          </div>
          {span ? <time>{span}</time> : null}
        </div>
      </button>
      {open && summary ? (
        <p>
          <ResumeMarks text={summary} />
        </p>
      ) : null}
    </li>
  );
}

/**
 * 履历正文里的 `**加粗**`。
 */
function ResumeMarks({ text }: { text: string }) {
  return (
    <>
      {parseResumeMarks(text).map((mark, index) =>
        mark.bold ? <strong key={`${mark.text}-${index}`}>{mark.text}</strong> : mark.text,
      )}
    </>
  );
}
