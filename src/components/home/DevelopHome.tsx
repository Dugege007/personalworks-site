import { useEffect, useMemo, useRef, useState, type PointerEvent } from "react";
import { Link } from "react-router-dom";
import { lexicon } from "../../content/lexicon";
import { gameProjects, profile } from "../../content/site";
import { stockPlaceholderSrc, workCoverSrc } from "../../content/stockMedia";
import {
  listLandscapeHeroFrames,
  listPublishedWorks,
  listWorkImages,
  type HomeHeroFrame,
} from "../../content/works";
import { hrefForWork } from "../../ia/href";
import { queryNotes, queryWorks } from "../../ia/query";
import type { HomeBlock, IaRecord } from "../../ia/types";
import { assetUrl } from "../../lib/assets";
import { usePrefersReducedMotion } from "../../hooks/usePrefersReducedMotion";
import { usePrefs } from "../../prefs/PrefsProvider";
import { ContactIcons } from "./ContactIcons";
import { HomeBeian } from "./HomeBeian";
import "../../styles/develop-home.css";

const HERO_INTERVAL_MS = 5000;
const HERO_NOREPEAT = 10;
const FRAME_NOREPEAT = 10;
const FRAME_IDLE_MIN_MS = 5000;
const FRAME_IDLE_MAX_MS = 10000;
const FRAME_FXS = ["fade", "rise", "zoom", "wipe"] as const;

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
  const reduced = usePrefersReducedMotion();
  const frames = useMemo(() => collectHeroFrames(block), [block]);
  const startRef = useRef<{ current: string; upcoming: string } | null>(null);
  if (!startRef.current && frames.length > 0) {
    const srcs = frames.map((frame) => frame.src);
    const current = pickFreshSrc(srcs, []);
    startRef.current = { current, upcoming: pickFreshSrc(srcs, [current]) };
  }
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [currentSrc, setCurrentSrc] = useState(startRef.current?.current ?? "");
  const [upcomingSrc, setUpcomingSrc] = useState(startRef.current?.upcoming ?? "");
  const [lastSrc, setLastSrc] = useState("");
  const recentRef = useRef<string[]>([]);
  const currentRef = useRef(currentSrc);
  const upcomingRef = useRef(upcomingSrc);
  const failedRef = useRef(failed);
  failedRef.current = failed;
  upcomingRef.current = upcomingSrc;

  const pool = frames.filter((frame) => !failed[frame.src]);
  const poolSrcs = pool.map((frame) => frame.src);
  const current = pool.find((frame) => frame.src === currentSrc) ?? pool[0] ?? null;
  const src = current?.src ?? "";
  currentRef.current = src;
  const shownSrcs = [lastSrc, src, upcomingSrc].filter(
    (item, index, list): item is string => Boolean(item) && !failed[item] && list.indexOf(item) === index,
  );
  const year = current?.year ?? block.heroYear ?? profile.portraitYear;
  const [lead, quote] = profile.bio;

  useEffect(() => {
    if (!currentSrc || !failed[currentSrc] || poolSrcs.length === 0) {
      return;
    }
    const next = pickFreshSrc(poolSrcs, [currentSrc, ...recentRef.current]);
    setCurrentSrc(next);
    const upcoming = pickFreshSrc(poolSrcs, [next, ...recentRef.current]);
    upcomingRef.current = upcoming;
    setUpcomingSrc(upcoming);
  }, [currentSrc, failed, poolSrcs]);

  useEffect(() => {
    if (reduced || poolSrcs.length < 2) {
      return;
    }
    const timer = window.setInterval(() => {
      const available = frames
        .filter((frame) => !failedRef.current[frame.src])
        .map((frame) => frame.src);
      const cur = currentRef.current;
      const reserved = upcomingRef.current;
      const next =
        reserved && reserved !== cur && available.includes(reserved)
          ? reserved
          : pickFreshSrc(available, [cur, ...recentRef.current]);
      recentRef.current = [...recentRef.current, cur].slice(-(HERO_NOREPEAT - 1));
      setLastSrc(cur);
      setCurrentSrc(next);
      currentRef.current = next;
      const upcoming = pickFreshSrc(available, [next, ...recentRef.current]);
      upcomingRef.current = upcoming;
      setUpcomingSrc(upcoming);
    }, HERO_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [frames, poolSrcs.length, reduced]);

  return (
    <section className="develop-hero" id="hero-bleed" data-tone={src ? "dark" : "light"}>
      {src ? (
        shownSrcs.map((item) => (
          <img
            key={item}
            className={`develop-hero-media${item === src ? " is-on" : ""}`}
            src={assetUrl(item)}
            alt=""
            onError={() => setFailed((prevFailed) => ({ ...prevFailed, [item]: true }))}
          />
        ))
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

/**
 * 头图优先用已发布风光摄影；缺图时回退到块上钉死的一张。
 */
function collectHeroFrames(block: HomeBlock): HomeHeroFrame[] {
  const fromWorks =
    block.query?.source === "works" ? listLandscapeHeroFrames() : [];
  if (fromWorks.length > 0) {
    return fromWorks;
  }
  if (block.heroSrc) {
    return [{ src: block.heroSrc, year: block.heroYear ?? profile.portraitYear }];
  }
  return [];
}

/**
 * 从图池随机取一张；最近若干张（含当前）不重复，图池不足时窗口收窄。
 */
function pickFreshSrc(pool: string[], recent: string[], windowSize = HERO_NOREPEAT): string {
  if (pool.length === 0) {
    return "";
  }
  if (pool.length === 1) {
    return pool[0] ?? "";
  }
  const limit = Math.min(windowSize, pool.length - 1);
  const forbidden = new Set(recent.filter(Boolean).slice(-limit));
  const candidates = pool.filter((src) => !forbidden.has(src));
  const last = recent[recent.length - 1];
  const bag = candidates.length > 0 ? candidates : pool.filter((src) => src !== last);
  return bag[Math.floor(Math.random() * bag.length)] ?? pool[0] ?? "";
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
        "profile-skills/skills.webp",
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
  id: string;
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
function SliceCard({ id, label, deco, to, srcs }: SliceCardProps) {
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
      data-slice={id}
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

type FrameShot = {
  src: string;
  title: string;
  href: string;
};

type FrameLane = {
  id: string;
  caption: string;
  featured: boolean;
  shots: FrameShot[];
};

function SelectedFrames({ ia }: { block: HomeBlock; ia: IaRecord }) {
  const lanes = useMemo(() => collectHomeFrameLanes(ia.id), [ia.id]);

  return (
    <section className="develop-frames" id="selected-frames">
      {lanes.map((lane) => (
        <FrameLaneCard key={lane.id} lane={lane} />
      ))}
    </section>
  );
}

/**
 * 精选五格：风光、人文、数字孪生、景观效果图、游戏开发，各从本类图池抽。
 */
function collectHomeFrameLanes(iaId: string): FrameLane[] {
  return [
    {
      id: lexicon.landscapePhoto.key,
      caption: lexicon.landscapePhoto.zh,
      featured: true,
      shots: shotsOfChannel(lexicon.landscapePhoto.key, iaId),
    },
    {
      id: lexicon.humanistPhoto.key,
      caption: lexicon.humanistPhoto.zh,
      featured: false,
      shots: shotsOfChannel(lexicon.humanistPhoto.key, iaId),
    },
    {
      id: lexicon.digitalTwin.key,
      caption: lexicon.digitalTwin.zh,
      featured: false,
      shots: shotsOfChannel(lexicon.digitalTwin.key, iaId),
    },
    {
      id: lexicon.landscapeRendering.key,
      caption: lexicon.landscapeRendering.zh,
      featured: false,
      shots: shotsOfChannel(lexicon.landscapeRendering.key, iaId),
    },
    {
      id: lexicon.gameDev.key,
      caption: lexicon.gameDev.zh,
      featured: false,
      shots: shotsOfGames(),
    },
  ];
}

function shotsOfChannel(channel: string, iaId: string): FrameShot[] {
  const shots: FrameShot[] = [];
  for (const work of listPublishedWorks(channel)) {
    const href = hrefForWork(work, iaId);
    for (const media of listWorkImages(work)) {
      if (!media.src) {
        continue;
      }
      shots.push({ src: media.src, title: work.title, href });
    }
  }
  return shots;
}

function shotsOfGames(): FrameShot[] {
  const shots: FrameShot[] = [];
  for (const game of gameProjects) {
    const href = `/${lexicon.gameDev.key}/${game.id}`;
    for (const shot of game.screenshots) {
      if (!shot.src) {
        continue;
      }
      shots.push({ src: shot.src, title: game.title, href });
    }
  }
  return shots;
}

function FrameLaneCard({ lane }: { lane: FrameLane }) {
  const reduced = usePrefersReducedMotion();
  const srcs = lane.shots.map((shot) => shot.src);
  const startRef = useRef("");
  if (!startRef.current && srcs.length > 0) {
    startRef.current = pickFreshSrc(srcs, [], FRAME_NOREPEAT);
  }
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [currentSrc, setCurrentSrc] = useState(startRef.current);
  const [lastSrc, setLastSrc] = useState("");
  const [fx, setFx] = useState<(typeof FRAME_FXS)[number]>("fade");
  const recentRef = useRef<string[]>([]);
  const currentRef = useRef(currentSrc);
  const srcsRef = useRef(srcs);
  const failedRef = useRef(failed);
  srcsRef.current = srcs;
  failedRef.current = failed;
  const pool = srcs.filter((src) => !failed[src]);
  const shown = pool.includes(currentSrc) ? currentSrc : (pool[0] ?? "");
  currentRef.current = shown;
  const shot = lane.shots.find((item) => item.src === shown);
  const shownSrcs = [lastSrc, shown].filter(
    (item, index, list): item is string => Boolean(item) && !failed[item] && list.indexOf(item) === index,
  );

  useEffect(() => {
    if (reduced || pool.length < 2) {
      return;
    }
    let timer = 0;
    const tick = () => {
      const available = srcsRef.current.filter((src) => !failedRef.current[src]);
      const cur = currentRef.current;
      const next = pickFreshSrc(available, [cur, ...recentRef.current], FRAME_NOREPEAT);
      recentRef.current = [...recentRef.current, cur].slice(-(FRAME_NOREPEAT - 1));
      setLastSrc(cur);
      setFx(FRAME_FXS[Math.floor(Math.random() * FRAME_FXS.length)] ?? "fade");
      setCurrentSrc(next);
      currentRef.current = next;
      timer = window.setTimeout(
        tick,
        FRAME_IDLE_MIN_MS + Math.random() * (FRAME_IDLE_MAX_MS - FRAME_IDLE_MIN_MS),
      );
    };
    timer = window.setTimeout(
      tick,
      FRAME_IDLE_MIN_MS + Math.random() * (FRAME_IDLE_MAX_MS - FRAME_IDLE_MIN_MS),
    );
    return () => window.clearTimeout(timer);
  }, [lane.id, pool.length, reduced]);

  if (!shown || !shot) {
    return (
      <div className={`develop-frame${lane.featured ? " is-featured" : ""}`}>
        <span className="develop-frame-copy">
          <strong>{lane.caption}</strong>
        </span>
      </div>
    );
  }

  return (
    <Link
      className={`develop-frame${lane.featured ? " is-featured" : ""}`}
      to={shot.href}
      data-fx={fx}
    >
      {shownSrcs.map((item) => (
        <img
          key={item}
          className={item === shown ? "is-on" : undefined}
          src={assetUrl(item)}
          alt=""
          onError={() => setFailed((prev) => ({ ...prev, [item]: true }))}
        />
      ))}
      <span className="develop-frame-copy">
        <strong>{shot.title}</strong>
        <em>{lane.caption}</em>
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
      <HomeBeian />
    </section>
  );
}
