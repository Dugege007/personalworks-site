import { useEffect, useLayoutEffect, useState, Fragment } from "react";
import { lexicon } from "../content/lexicon";
import { formatResumeSpan, parseResumeMarks, resume } from "../content/resume";
import { profile } from "../content/site";
import { workRecords } from "../content/works";
import { hrefForDevelopChannel } from "../ia/workTree";
import {
  compressJobSummary,
  digitalTwinClientNames,
  isOmittedPrintJob,
  printBlogHref,
  printContactLines,
  printRoleLine,
  selectPrintSkillItems,
  toResumePrintHref,
  type PrintJobInput,
} from "../lib/resumePrint";
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

const resumeHref = toResumePrintHref(`/${lexicon.profileResume.key}`);
const lineSimHref = toResumePrintHref(
  hrefForDevelopChannel(lexicon.twinAndSim.key, lexicon.lineSimulation.key),
);
const blogHref = printBlogHref(resume.blogHref);
const blogLabel = zh(resume.blogLabel) || "CSDN 笔记";

/**
 * 一页 A4 投递稿。无顶栏、无肖像、无二维码；站内链接在渲染时写成 https 绝对地址。
 */
export function ResumePrintPage() {
  const [ready, setReady] = useState(false);
  const contacts = printContactLines(profile.contactChannels);
  const role = printRoleLine(zh(resume.summary));
  const clientNames = digitalTwinClientNames(
    workRecords.map((work) => ({ id: work.id, title: work.title, channel: work.channel })),
  );
  const jobs = resume.jobs
    .filter((job) => !isOmittedPrintJob({ role: zh(job.role), org: zh(job.org) }))
    .map((job) => {
      const input: PrintJobInput = {
        id: job.id,
        role: zh(job.role),
        org: zh(job.org),
        summary: zh(job.summary),
        span: formatResumeSpan(job.start, job.end),
      };
      return { ...input, summary: compressJobSummary(input, lineSimHref, clientNames) };
    });
  const lead = jobs[0];
  const rest = jobs.slice(1);
  const skills = selectPrintSkillItems(resume.skills.flatMap((group) => group.items));

  useLayoutEffect(() => {
    document.documentElement.setAttribute("data-resume-print", "1");
    return () => {
      document.documentElement.removeAttribute("data-resume-print");
    };
  }, []);

  useEffect(() => {
    const previousTitle = document.title;
    document.title = `${profile.name}-Unity数字孪生-简历`;
    let alive = true;
    const fonts = document.fonts;
    if (fonts?.ready) {
      void fonts.ready.then(() => {
        if (alive) {
          setReady(true);
        }
      });
    } else {
      setReady(true);
    }
    return () => {
      alive = false;
      document.title = previousTitle;
    };
  }, []);

  return (
    <main className="resume-print" data-ready={ready ? "true" : "false"}>
      <header className="resume-print-header">
        <div className="resume-print-id">
          <h1 className="resume-print-name">{profile.name}</h1>
          <p className="resume-print-en">{profile.nameEn}</p>
        </div>
        {role ? <p className="resume-print-role">{role}</p> : null}
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
        <p className="resume-print-site">
          <a href={resumeHref}>{resumeHref}</a>
        </p>
      </header>

      {lead ? (
        <section className="resume-print-block">
          <h2>经历</h2>
          <PrintJob job={lead} />
        </section>
      ) : null}

      <div className="resume-print-split">
        <div>
          {skills.length > 0 || resume.skillLinks.length > 0 ? (
            <section className="resume-print-block">
              <h2>技能</h2>
              {skills.length > 0 ? (
                <ul className="resume-print-skills">
                  {skills.map((item) => (
                    <li key={item}>
                      <PrintMarks text={item} />
                    </li>
                  ))}
                </ul>
              ) : null}
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
            <section className="resume-print-block">
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
        <div>
          {resume.schools.length > 0 ? (
            <section className="resume-print-block">
              <h2>教育</h2>
              <ul className="resume-print-schools">
                {resume.schools.map((school) => (
                  <li key={school.id}>
                    <div className="resume-print-job-head">
                      <strong>{zh(school.school)}</strong>
                      {school.years ? <time>{school.years}</time> : null}
                    </div>
                    {school.degree ? <p className="resume-print-org">{zh(school.degree)}</p> : null}
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
          {resume.languages.length > 0 ? (
            <section className="resume-print-block">
              <h2>语言</h2>
              <p className="resume-print-line">{resume.languages.map((item) => zh(item)).join("；")}</p>
            </section>
          ) : null}
        </div>
        <section className="resume-print-block">
          <h2>笔记</h2>
          <p className="resume-print-line">
            算法与工程笔记见个人网站与 <PrintAnchor href={blogHref} text={blogLabel} />
          </p>
        </section>
      </div>
    </main>
  );
}

/**
 * 单条经历。组织里的加粗标记按稿面渲染。
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
 * 履历片段。链接一律绝对化，并在文案后附上主机路径，便于纸面抄写。
 */
function PrintMarks({ text }: { text: string }) {
  return (
    <>
      {parseResumeMarks(text).map((mark, index) => {
        const key = `${mark.href ?? ""}-${mark.text}-${index}`;
        if (mark.href) {
          return <PrintAnchor key={key} href={mark.href} text={mark.text} />;
        }
        if (mark.bold) {
          return <strong key={key}>{mark.text}</strong>;
        }
        return <Fragment key={key}>{mark.text}</Fragment>;
      })}
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
  const label = visibleLinkText(text, absolute);
  return (
    <a className={label.includes("（") ? undefined : "is-short"} href={absolute}>
      {label}
    </a>
  );
}

/**
 * 个人站简历、数字孪生索引、产线仿真索引和 CSDN 在纸面上露出路径。其余外链只留短文案。
 */
function shouldRevealPath(url: URL): boolean {
  if (url.host === "blog.csdn.net") {
    return true;
  }
  if (url.host !== "duhongbo.com" && url.host !== "www.duhongbo.com") {
    return false;
  }
  return (
    url.pathname === "/profile-resume" ||
    url.pathname.endsWith("/digital-twin") ||
    url.pathname.endsWith("/line-sim")
  );
}

/**
 * 外链文案未含主机名时，括号补上可手打的路径。斜杠后允许换行，避免从英文单词中间断开。
 */
function visibleLinkText(text: string, href: string): string {
  if (!/^https?:/i.test(href)) {
    return text;
  }
  try {
    const url = new URL(href);
    if (text.includes(url.host) || !shouldRevealPath(url)) {
      return text;
    }
    const path = `${url.host}${url.pathname}`.replace(/\/$/, "").replaceAll("/", "/\u200b");
    return `${text}（${path}）`;
  } catch {
    return text;
  }
}
