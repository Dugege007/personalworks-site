import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Link } from "react-router-dom";
import { lexicon } from "../../content/lexicon";
import { profile } from "../../content/site";
import { stockPlaceholderSrc, workCoverSrc } from "../../content/stockMedia";
import { hrefForWork, channelTitleZh } from "../../ia/href";
import { queryNotes, queryWorks } from "../../ia/query";
import type { HomeBlock, IaRecord } from "../../ia/types";
import { assetUrl } from "../../lib/assets";
import { usePrefs } from "../../prefs/PrefsProvider";
import { ContactIcons } from "./ContactIcons";
import "../../styles/develop-home.css";

type DevelopHomeProps = {
  ia: IaRecord;
};

/**
 * 显影首页：通幅头图叠简介、切片、精选、心得、墨底收束。
 */
export function DevelopHome({ ia }: DevelopHomeProps) {
  return (
    <div className="develop-home">
      <div className="develop-progress" aria-hidden="true" />
      {ia.home.blocks.map((block, index) => (
        <DevelopBlock key={`${block.type}-${index}`} block={block} ia={ia} />
      ))}
    </div>
  );
}

type DevelopBlockProps = {
  block: HomeBlock;
  ia: IaRecord;
};

function DevelopBlock({ block, ia }: DevelopBlockProps) {
  if (block.type === "hero-bleed") {
    return <HeroBleed block={block} />;
  }
  if (block.type === "waypoint-slices") {
    return <WaypointSlices />;
  }
  if (block.type === "selected-frames") {
    return <SelectedFrames block={block} ia={ia} />;
  }
  if (block.type === "notes-tease") {
    return <NotesTease block={block} />;
  }
  if (block.type === "contact-close") {
    return <ContactClose />;
  }
  return null;
}

function HeroBleed({ block }: { block: HomeBlock }) {
  const [mediaFailed, setMediaFailed] = useState(false);
  const src = block.heroSrc && !mediaFailed ? assetUrl(block.heroSrc) : "";
  const year = block.heroYear ?? profile.portraitYear;
  const [lead, quote] = profile.bio;

  return (
    <section className="develop-hero" id="hero-bleed" data-tone={src ? "dark" : "light"}>
      {src ? (
        <img
          className="develop-hero-media"
          src={src}
          alt=""
          onError={() => setMediaFailed(true)}
        />
      ) : (
        <div className="develop-hero-well" aria-hidden="true" />
      )}
      <div className="develop-hero-veil" aria-hidden="true" />
      <div className="develop-hero-band">
        <div className="develop-hero-copy">
          {src ? (
            <p className="develop-hero-shot">
              {lexicon.shotIn.deco} · {year}
            </p>
          ) : null}
          <h1 className="develop-hero-name">{profile.name}</h1>
          <p className="develop-hero-en">{profile.nameEn}</p>
          <p className="develop-hero-id">{block.identity}</p>
          <div className="develop-hero-cta">
            <Link className="develop-btn is-solid" to={`/${lexicon.profileResume.key}`}>
              {lexicon.profileResume.zh}
            </Link>
            <Link className="develop-btn is-line" to={`/${lexicon.workIndex.key}`}>
              {lexicon.workIndex.zh}
            </Link>
          </div>
        </div>
        <div className="develop-hero-note">
          {lead ? <p className="develop-hero-lead">{lead}</p> : null}
          {quote ? <p className="develop-hero-quote">{quote}</p> : null}
        </div>
      </div>
    </section>
  );
}

function WaypointSlices() {
  const workSrcs = uniqueSrcs(queryWorks({ source: "works" }).map((work) => workCoverSrc(work)));
  const slices = [
    {
      id: lexicon.profileResume.key,
      label: lexicon.profileResume.zh,
      deco: lexicon.profileResume.deco,
      to: `/${lexicon.profileResume.key}`,
      srcs: uniqueSrcs(profile.portraitSrcs),
    },
    {
      id: lexicon.profileSkills.key,
      label: lexicon.profileSkills.zh,
      deco: lexicon.profileSkills.deco,
      to: `/${lexicon.profileSkills.key}`,
      srcs: uniqueSrcs([
        "home-page/skills.webp",
        stockPlaceholderSrc("landscape-cds", 1),
        stockPlaceholderSrc("landscape-cds", 2),
        stockPlaceholderSrc("landscape-cds", 3),
      ]),
    },
    {
      id: lexicon.workIndex.key,
      label: lexicon.workIndex.zh,
      deco: lexicon.workIndex.deco,
      to: `/${lexicon.workIndex.key}`,
      srcs: workSrcs,
    },
    {
      id: lexicon.notes.key,
      label: lexicon.notes.zh,
      deco: lexicon.notes.deco,
      to: `/${lexicon.notes.key}`,
      srcs: uniqueSrcs(["notes/cover.webp"]),
    },
  ];

  return (
    <section className="develop-slices" id="waypoint-slices">
      {slices.map((item) => (
        <SliceCard key={item.id} {...item} />
      ))}
    </section>
  );
}

type SliceCardProps = {
  label: string;
  deco: string;
  to: string;
  srcs: string[];
};

const SLICE_IDLE_MIN_MS = 5000;
const SLICE_IDLE_MAX_MS = 10000;
const SLICE_HOVER_MS = 2000;

/**
 * 切片图卡：空闲 5–10 秒换一张，悬停改为 2 秒；各卡自计时，最近五张不重复。
 */
function SliceCard({ label, deco, to, srcs }: SliceCardProps) {
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [current, setCurrent] = useState(srcs[0] ?? "");
  const recentRef = useRef<string[]>([]);
  const currentRef = useRef(current);
  const poolRef = useRef<string[]>([]);
  const hoverRef = useRef(false);
  const timerRef = useRef<number>(0);

  const pool = srcs.filter((src) => !failed[src]);
  const shown = pool.includes(current) ? current : (pool[0] ?? "");
  poolRef.current = pool;
  currentRef.current = shown;

  useEffect(() => {
    scheduleSliceTick();
    return () => window.clearTimeout(timerRef.current);
  }, []);

  /**
   * 按当前是否悬停排下一次换图；空闲随机 5–10 秒。
   */
  function scheduleSliceTick() {
    window.clearTimeout(timerRef.current);
    timerRef.current = 0;
    if (poolRef.current.length < 2) {
      return;
    }
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }
    const delay = hoverRef.current
      ? SLICE_HOVER_MS
      : SLICE_IDLE_MIN_MS + Math.random() * (SLICE_IDLE_MAX_MS - SLICE_IDLE_MIN_MS);
    timerRef.current = window.setTimeout(() => {
      const next = pickNextSliceSrc(poolRef.current, currentRef.current, recentRef.current);
      recentRef.current = [...recentRef.current, currentRef.current].slice(-4);
      setCurrent(next);
      scheduleSliceTick();
    }, delay);
  }

  /**
   * 细指针悬停加快到两秒；触屏只走空闲节拍。
   */
  function handlePointerEnter(event: PointerEvent<HTMLAnchorElement>) {
    if (event.pointerType !== "mouse" || hoverRef.current) {
      return;
    }
    hoverRef.current = true;
    scheduleSliceTick();
  }

  function handlePointerLeave() {
    if (!hoverRef.current) {
      return;
    }
    hoverRef.current = false;
    scheduleSliceTick();
  }

  return (
    <Link
      className="develop-slice"
      to={to}
      onPointerEnter={handlePointerEnter}
      onPointerLeave={handlePointerLeave}
    >
      {pool.length > 0 ? (
        pool.map((src) => (
          <img
            key={src}
            className={src === shown ? "is-on" : undefined}
            src={assetUrl(src)}
            alt=""
            onError={() => setFailed((prev) => ({ ...prev, [src]: true }))}
          />
        ))
      ) : (
        <span className="develop-slice-well" />
      )}
      <span className="develop-slice-copy">
        <strong>{label}</strong>
        <em>{deco}</em>
      </span>
    </Link>
  );
}

/**
 * 从图池取下一张：最近四张加当前共五张不重复；池不足时窗口收窄。
 */
function pickNextSliceSrc(pool: string[], current: string, recent: string[]): string {
  if (pool.length < 2) {
    return current;
  }
  const windowSize = Math.min(4, pool.length - 1);
  const forbidden = new Set([...recent, current].slice(-windowSize));
  const candidates = pool.filter((src) => !forbidden.has(src));
  const bag = candidates.length > 0 ? candidates : pool.filter((src) => src !== current);
  return bag[Math.floor(Math.random() * bag.length)] ?? current;
}

/**
 * 去掉空值和重复路径，保持首次出现的顺序。
 */
function uniqueSrcs(items: Array<string | undefined>): string[] {
  const seen = new Set<string>();
  const list: string[] = [];
  for (const item of items) {
    if (!item || seen.has(item)) {
      continue;
    }
    seen.add(item);
    list.push(item);
  }
  return list;
}

function SelectedFrames({ block, ia }: { block: HomeBlock; ia: IaRecord }) {
  const works = block.query ? queryWorks(block.query) : [];

  return (
    <section className="develop-frames" id="selected-frames">
      {works.map((work, index) => {
        const src = workCoverSrc(work, index);
        return (
          <FrameCard
            key={`${work.channel}-${work.id}`}
            title={work.title}
            caption={channelTitleZh(work.channel)}
            href={hrefForWork(work, ia.id)}
            src={src}
            featured={index === 0}
          />
        );
      })}
    </section>
  );
}

type FrameCardProps = {
  title: string;
  caption: string;
  href: string;
  src?: string;
  featured: boolean;
};

function FrameCard({ title, caption, href, src, featured }: FrameCardProps) {
  const [failed, setFailed] = useState(false);
  const media = src && !failed ? assetUrl(src) : "";

  return (
    <Link className={`develop-frame${featured ? " is-featured" : ""}`} to={href}>
      {media ? <img src={media} alt="" onError={() => setFailed(true)} /> : null}
      <span className="develop-frame-copy">
        <strong>{title}</strong>
        <em>{caption}</em>
      </span>
    </Link>
  );
}

function NotesTease({ block }: { block: HomeBlock }) {
  const notes = block.query ? queryNotes(block.query) : [];
  const note = notes[0];
  if (!note) {
    return null;
  }

  return (
    <section className="develop-notes" id="notes-tease">
      <Link className="develop-notes-link" to={`/${lexicon.notes.key}/${note.slug}`}>
        <time dateTime={note.date.replaceAll(".", "-")}>{note.date}</time>
        <h2>{note.title}</h2>
        <p>{note.summary}</p>
      </Link>
    </section>
  );
}

function ContactClose() {
  const { currentSkin } = usePrefs();

  return (
    <section className="develop-close" id="contact-close">
      <div>
        <p className="develop-close-brand">
          <span>{currentSkin.brand.zh}</span>
          <span>{currentSkin.brand.deco}</span>
        </p>
        <p className="develop-close-lead">写信或到场都可以。作品在墙里，照片在册里。</p>
      </div>
      <ContactIcons channels={profile.contactChannels} />
    </section>
  );
}
