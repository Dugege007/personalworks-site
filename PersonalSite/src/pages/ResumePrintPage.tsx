import { useLayoutEffect, useRef, useState, Fragment } from "react";
import { profileDisplayName, readProfileNameMode } from "../content/profileNameMode";
import { annotateTerms } from "../content/glossary";
import { glossaryBook } from "../content/glossaryDoc";
import { lexicon } from "../content/lexicon";
import { formatResumeSpan, parseResumeMarks, resume } from "../content/resume";
import { profile } from "../content/site";
import { queryNotes } from "../ia/query";
import {
  PRINT_FIT_LEVELS,
  isPrintInternship,
  printContacts,
  printIntroLines,
  printJobSummary,
  printSheetLimitMm,
  resumePdfSaveTitle,
  toResumePrintHref,
  type PrintJobInput,
} from "../lib/resumePrint";
import { resumeNoteLabel, selectResumeNotes } from "../lib/resumeNotes";
import type { LocalizedString } from "../prefs/types";
import { tx } from "../prefs/tx";
import "../styles/resume-print.css";

/**
 * 打印稿固定中文，不跟皮肤语言切换。
 */
function zh(value: LocalizedString | undefined): string {
  if (!value) {
    return "";
  }
  return tx(value, "zh-CN");
}

const mail = profile.contactChannels.find((item) => item.id === "profileMail");
const github = profile.contactChannels.find((item) => item.id === "profileGithub");
const blogHref = resume.blogHref ? toResumePrintHref(resume.blogHref) : "";
const blogLabel = zh(resume.blogLabel) || "CSDN 笔记";

/**
 * 一页 A4 投递稿。正文来自当前 `文稿/resume.md` 与简历页笔记栏，链接写成 https 绝对地址。
 */
export function ResumePrintPage() {
  const displayName = profileDisplayName(readProfileNameMode(), profile.name, profile.legalName);
  const rootRef = useRef<HTMLElement>(null);
  const steppedAt = useRef(-1);
  const [fontsReady, setFontsReady] = useState(false);
  const [fitIndex, setFitIndex] = useState(0);
  const fit = PRINT_FIT_LEVELS[Math.min(fitIndex, PRINT_FIT_LEVELS.length - 1)] ?? PRINT_FIT_LEVELS[0];
  const contacts = printContacts(
    { mail: mail?.value, githubText: github?.value, githubHref: github?.href },
    { github: fit.dropGithub, homepage: fit.dropHomepage },
  );
  const summary = printIntroLines(zh(resume.summary));
  const jobs = resume.jobs
    .filter((job) => !isPrintInternship({ role: zh(job.role), org: zh(job.org) }))
    .map((job) => {
    const input: PrintJobInput = {
      id: job.id,
      role: zh(job.role),
      org: zh(job.org),
      summary: zh(job.summary),
      span: formatResumeSpan(job.start, job.end),
    };
    return { ...input, summary: printJobSummary(input, fit.compressSecondary) };
  });
  const lead = jobs[0];
  const rest = jobs.slice(1);
  const notes = selectResumeNotes(queryNotes({ source: "notes" }));
  const hasNotes = Boolean(blogHref) || notes.length > 0;

  useLayoutEffect(() => {
    document.documentElement.setAttribute("data-resume-print", "1");
    return () => {
      document.documentElement.removeAttribute("data-resume-print");
    };
  }, []);

  useLayoutEffect(() => {
    const previousTitle = document.title;
    document.title = `${displayName}-Unity数字孪生-简历`;
    let alive = true;
    const fonts = document.fonts;
    if (fonts?.ready) {
      void fonts.ready.then(() => {
        if (alive) {
          setFontsReady(true);
        }
      });
    } else {
      setFontsReady(true);
    }
    return () => {
      alive = false;
      document.title = previousTitle;
    };
  }, [displayName]);

  useLayoutEffect(() => {
    const el = rootRef.current;
    if (!el) {
      return;
    }
    if (!fontsReady) {
      el.dataset.fit = "fitting";
      return;
    }
    const limit = measureMm(printSheetLimitMm(window.matchMedia("print").matches));
    const previousMin = el.style.minHeight;
    el.style.minHeight = "0";
    const height = el.scrollHeight;
    el.style.minHeight = previousMin;
    const overflow = height > limit - 6;
    if (!overflow) {
      el.dataset.fit = "done";
      return;
    }
    if (fitIndex >= PRINT_FIT_LEVELS.length - 1) {
      el.dataset.fit = "overflow";
      return;
    }
    if (steppedAt.current === fitIndex) {
      return;
    }
    steppedAt.current = fitIndex;
    el.dataset.fit = "fitting";
    setFitIndex(fitIndex + 1);
  }, [fontsReady, fitIndex]);

  return (
    <>
    <div className="resume-print-bar">
      <button type="button" className="resume-print-download" onClick={() => downloadResumePdf(displayName)}>
        下载
      </button>
    </div>
    <main
      ref={rootRef}
      className="resume-print"
      data-ready={fontsReady ? "true" : "false"}
      data-density={String(fit.density)}
      data-compress={fit.compressSecondary ? "1" : "0"}
    >
      <header className="resume-print-header">
        <div className="resume-print-id">
          <h1 className="resume-print-name">{displayName}</h1>
          <p className="resume-print-en">{profile.nameEn}</p>
        </div>
        {summary ? (
          <p className="resume-print-role">
            <PrintMarks text={summary} />
          </p>
        ) : null}
        {contacts.length > 0 ? (
          <ul className="resume-print-contacts">
            {contacts.map((item) => (
              <li key={item.id}>
                <span className="resume-print-kicker">{item.label}</span>
                {item.href ? <a href={item.href}>{item.text}</a> : item.text}
              </li>
            ))}
          </ul>
        ) : null}
      </header>

      {lead ? (
        <section className="resume-print-block resume-print-lead">
          <h2>经历</h2>
          <PrintJob job={lead} />
        </section>
      ) : null}

      <div className="resume-print-split">
        <div>
          {resume.skills.some((group) => group.items.length > 0) || resume.skillLinks.length > 0 ? (
            <section className="resume-print-block resume-print-skillbox">
              <h2>技能</h2>
              {resume.skills.map((group) =>
                group.items.length > 0 ? (
                  <div key={group.id}>
                    {group.title ? <h3 className="resume-print-subhead">{group.title}</h3> : null}
                    <ol className="resume-print-skills">
                      {group.items.map((item) => (
                        <li key={item}>
                          <PrintMarks text={item} annotate />
                        </li>
                      ))}
                    </ol>
                  </div>
                ) : null,
              )}
              {resume.skillLinks.map((link) => (
                <p key={`${link.href}-${link.text}`} className="resume-print-more">
                  <PrintAnchor href={link.href} text={link.text} />
                </p>
              ))}
            </section>
          ) : null}
        </div>

        <div>
          {rest.length > 0 ? (
            <section className="resume-print-block resume-print-prior">
              <h2>此前经历</h2>
              <ol className="resume-print-jobs">
                {rest.map((job) => (
                  <PrintJob key={job.id} job={job} />
                ))}
              </ol>
            </section>
          ) : null}
        </div>
      </div>

      <div className="resume-print-foot">
        <div className="resume-print-foot-main">
          {resume.schools.length > 0 ? (
            <section className="resume-print-block resume-print-edu">
              <h2>教育</h2>
              <ul className="resume-print-schools">
                {resume.schools.map((school) => (
                  <li key={school.id}>
                    <div className="resume-print-job-head">
                      <strong>{zh(school.school)}</strong>
                      {school.years ? <time>{school.years}</time> : null}
                    </div>
                    {school.degree ? <p className="resume-print-org">{zh(school.degree)}</p> : null}
                    {school.note ? (
                      <p className="resume-print-body">
                        <PrintMarks text={zh(school.note)} />
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
        </div>
        {hasNotes ? (
          <section className="resume-print-block resume-print-notes-block">
            <h2>笔记</h2>
            <ul className="resume-print-notes">
              {blogHref ? (
                <li>
                  <a href={blogHref}>{blogLabel}</a>
                </li>
              ) : null}
              {notes.map((note) => (
                <li key={note.slug}>
                  <a href={toResumePrintHref(`/${lexicon.notes.key}/${note.slug}`)}>{resumeNoteLabel(note)}</a>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>
    </main>
    </>
  );
}

/**
 * 按当前版式打开系统打印对话框。另存为 PDF 时可以选择目录，建议文件名是当前姓名加当天日期。
 */
function downloadResumePdf(displayName: string): void {
  const previous = document.title;
  document.title = resumePdfSaveTitle(displayName, new Date());
  const restore = () => {
    document.title = previous;
    window.removeEventListener("afterprint", restore);
  };
  window.addEventListener("afterprint", restore);
  window.print();
}

/**
 * 单条经历。组织与说明按文稿标记渲染，不改写句子。
 */
function PrintJob({ job }: { job: PrintJobInput }) {
  return (
    <article className="resume-print-job">
      <div className="resume-print-job-head">
        <strong>{job.role}</strong>
        {job.span ? <time>{job.span}</time> : null}
      </div>
      {job.org ? (
        <p className="resume-print-org">
          <PrintMarks text={job.org} />
        </p>
      ) : null}
      {job.summary ? (
        <p className="resume-print-body">
          <PrintMarks text={job.summary} />
        </p>
      ) : null}
    </article>
  );
}

/**
 * 履历片段。链接一律绝对化，可见文字仍用文稿里的标签。
 */
function PrintMarks({ text, annotate }: { text: string; annotate?: boolean }) {
  return (
    <>
      {parseResumeMarks(text).map((mark, index) => {
        const key = `${mark.href ?? ""}-${mark.text}-${index}`;
        if (mark.href) {
          return <PrintAnchor key={key} href={mark.href} text={mark.text} />;
        }
        const body = annotate ? <PrintTerms text={mark.text} /> : mark.text;
        if (mark.bold) {
          return <strong key={key}>{body}</strong>;
        }
        return <Fragment key={key}>{body}</Fragment>;
      })}
    </>
  );
}

/**
 * 技能条目里，网页上加粗的名词这里同样加粗。不带悬停提示。
 */
function PrintTerms({ text }: { text: string }) {
  return (
    <>
      {annotateTerms(text, glossaryBook).map((piece, index) =>
        piece.kind === "term" ? <strong key={index}>{piece.text}</strong> : <Fragment key={index}>{piece.text}</Fragment>,
      )}
    </>
  );
}

/**
 * 真实锚点。拒绝没有协议的地址，避免把 localhost 写进 PDF。
 */
function PrintAnchor({ href, text }: { href: string; text: string }) {
  const absolute = toResumePrintHref(href);
  if (!/^(https?:|mailto:|tel:)/i.test(absolute)) {
    return <span>{text}</span>;
  }
  return <a href={absolute}>{text}</a>;
}

/**
 * 把毫米换成当前页面的像素，供一页高度比较。
 */
function measureMm(mm: number): number {
  const probe = document.createElement("div");
  probe.style.position = "absolute";
  probe.style.visibility = "hidden";
  probe.style.height = `${mm}mm`;
  document.body.appendChild(probe);
  const px = probe.getBoundingClientRect().height;
  probe.remove();
  return px;
}
