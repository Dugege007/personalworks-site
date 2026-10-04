import { useEffect, useMemo, useRef, useState, type MouseEvent, type PointerEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { lexicon } from "../../content/lexicon";
import { photoExhibitTag, photoExhibitTitle } from "../../content/photoFacet";
import { TermText } from "../TermText";
import { gameProjects, profile, skillSliceSrcs } from "../../content/site";
import { heroFrameFitsViewport, type PhotoOrientation } from "../../content/photoOrientation";
import { photoSizes } from "../../content/photoSizes";
import {
  drawOfCover,
  drawOfImage,
  hrefForPhotoWork,
  listLandscapeHeroFrames,
  listMatchingPhotoFrames,
  listPublishedPhotoWorks,
  listPublishedWorks,
  listWorkImages,
  type HomeHeroFrame,
} from "../../content/works";
import { contentDraw, starDrawWeight } from "../../content/stars";
import { fixedDraw, pickWeightedSrc, uniqueDraws, type DrawSrc, comboRelay3s, comboRelay4s } from "../../lib/comboCycle";
import { useComboRelay, useComboShown } from "../../lib/useComboRelay";
import { hrefForWork } from "../../ia/href";
import { channelStillDraws } from "../../ia/workTree";
import { queryNotes, queryWorks } from "../../ia/query";
import type { HomeBlock, IaRecord } from "../../ia/types";
import { assetUrl } from "../../lib/assets";
import { whenSiteBootReleased } from "../../lib/siteBoot";
import { usePrefersReducedMotion } from "../../hooks/usePrefersReducedMotion";
import { useViewportOrientation } from "../../hooks/useViewportOrientation";
import { usePrefs } from "../../prefs/PrefsProvider";
import { ProfileName } from "../ProfileName";
import { ContactIcons } from "./ContactIcons";
import { HomeBeian } from "./HomeBeian";
import "../../styles/develop-home.css";

const HERO_INTERVAL_MS = 5000;
const HERO_ENTER_MS = 3000;
const HERO_HINT = "再次点击进入该相册";
const HERO_NOREPEAT = 10;

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
  const viewport = useViewportOrientation();
  const navigate = useNavigate();
  const frames = useMemo(() => collectHeroFrames(block), [block]);
  const [measured, setMeasured] = useState<Record<string, { width: number; height: number }>>({});
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [paused, setPaused] = useState(false);
  const [hint, setHint] = useState(false);
  const hintTimerRef = useRef(0);
  const enterUntilRef = useRef(0);
  const hrefRef = useRef("");
  const startRef = useRef("");
  const upcomingStartRef = useRef("");
  if (!startRef.current) {
    const draws = heroDraws(frames, viewport, {});
    startRef.current = pickWeightedSrc(draws, [], HERO_NOREPEAT);
    upcomingStartRef.current = pickWeightedSrc(draws, [startRef.current], HERO_NOREPEAT);
  }
  const [currentSrc, setCurrentSrc] = useState(startRef.current);
  const [upcomingSrc, setUpcomingSrc] = useState(upcomingStartRef.current);
  const [lastSrc, setLastSrc] = useState("");
  const recentRef = useRef<string[]>([]);
  const currentRef = useRef(currentSrc);
  const upcomingRef = useRef(upcomingSrc);
  const failedRef = useRef(failed);
  failedRef.current = failed;
  upcomingRef.current = upcomingSrc;

  const sizedFrames = useMemo(
    () =>
      frames.map((frame) => {
        const size = measured[frame.src];
        if (size) {
          return { ...frame, width: size.width, height: size.height };
        }
        return frame;
      }),
    [frames, measured],
  );
  const eligibleDraws = useMemo(
    () => heroDraws(sizedFrames, viewport, failed),
    [failed, sizedFrames, viewport],
  );
  const eligibleSrcs = useMemo(() => eligibleDraws.map((item) => item.src), [eligibleDraws]);
  const pool = sizedFrames.filter((frame) => !failed[frame.src]);
  const selected = pool.find((frame) => frame.src === currentSrc) ?? null;
  const current = selected ?? pool.find((frame) => frame.src === eligibleSrcs[0]) ?? null;
  const src = current?.src ?? "";
  hrefRef.current = current?.href ?? "";
  if (selected) {
    currentRef.current = selected.src;
  }
  const shownSrcs = [lastSrc, src, upcomingSrc].filter(
    (item, index, list): item is string => Boolean(item) && !failed[item] && list.indexOf(item) === index,
  );
  const year = current?.year ?? block.heroYear ?? profile.portraitYear;
  const [lead, quote] = profile.bio;

  useEffect(() => {
    let cancelled = false;
    for (const frame of frames) {
      if (frame.width && frame.height) {
        continue;
      }
      const img = new Image();
      img.onload = () => {
        if (cancelled || !img.naturalWidth || !img.naturalHeight) {
          return;
        }
        setMeasured((prev) => {
          if (prev[frame.src]) {
            return prev;
          }
          return { ...prev, [frame.src]: { width: img.naturalWidth, height: img.naturalHeight } };
        });
      };
      img.src = assetUrl(frame.src);
    }
    return () => {
      cancelled = true;
    };
  }, [frames]);

  useEffect(() => {
    if (eligibleSrcs.length === 0) {
      return;
    }
    const cur = currentSrc;
    if (cur && eligibleSrcs.includes(cur)) {
      if (upcomingRef.current && !eligibleSrcs.includes(upcomingRef.current)) {
        const upcoming = pickWeightedSrc(eligibleDraws, [cur, ...recentRef.current], HERO_NOREPEAT);
        upcomingRef.current = upcoming;
        setUpcomingSrc(upcoming);
      }
      return;
    }
    const next = pickWeightedSrc(eligibleDraws, [cur, ...recentRef.current], HERO_NOREPEAT);
    if (cur) {
      setLastSrc(cur);
    }
    setCurrentSrc(next);
    currentRef.current = next;
    const upcoming = pickWeightedSrc(eligibleDraws, [next, ...recentRef.current], HERO_NOREPEAT);
    upcomingRef.current = upcoming;
    setUpcomingSrc(upcoming);
  }, [currentSrc, eligibleDraws, eligibleSrcs]);

  useEffect(() => {
    if (reduced || paused || eligibleSrcs.length < 2) {
      return;
    }
    let timer = 0;
    let cancelled = false;
    void whenSiteBootReleased().then(() => {
      if (cancelled) {
        return;
      }
      timer = window.setInterval(() => {
        const available = eligibleDraws.filter((item) => !failedRef.current[item.src]);
        const cur = currentRef.current;
        const reserved = upcomingRef.current;
        const next =
          reserved && reserved !== cur && available.some((item) => item.src === reserved)
            ? reserved
            : pickWeightedSrc(available, [cur, ...recentRef.current], HERO_NOREPEAT);
        recentRef.current = [...recentRef.current, cur].slice(-(HERO_NOREPEAT - 1));
        setLastSrc(cur);
        setCurrentSrc(next);
        currentRef.current = next;
        const upcoming = pickWeightedSrc(available, [next, ...recentRef.current], HERO_NOREPEAT);
        upcomingRef.current = upcoming;
        setUpcomingSrc(upcoming);
      }, HERO_INTERVAL_MS);
    });
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [eligibleDraws, paused, reduced]);

  useEffect(() => {
    return () => window.clearTimeout(hintTimerRef.current);
  }, []);

  /**
   * 头图点击：先暂停轮换并居中提示；三秒内再点进入相册，超时重置轮换计时。
   */
  function handleHeroClick(event: MouseEvent<HTMLElement>) {
    const target = event.target as HTMLElement;
    if (target.closest("a, button")) {
      return;
    }
    if (Date.now() < enterUntilRef.current) {
      enterUntilRef.current = 0;
      window.clearTimeout(hintTimerRef.current);
      setHint(false);
      const href = hrefRef.current;
      if (href) {
        navigate(href);
      }
      return;
    }
    setPaused(true);
    setHint(true);
    enterUntilRef.current = Date.now() + HERO_ENTER_MS;
    window.clearTimeout(hintTimerRef.current);
    hintTimerRef.current = window.setTimeout(() => {
      enterUntilRef.current = 0;
      setHint(false);
      setPaused(false);
    }, HERO_ENTER_MS);
  }

  return (
    <section
      className="develop-hero"
      id="hero-bleed"
      data-tone={src ? "dark" : "light"}
      onClick={handleHeroClick}
    >
      {src ? (
        shownSrcs.map((item) => (
          <img
            key={item}
            className={`develop-hero-media${item === src ? " is-on" : ""}`}
            src={assetUrl(item)}
            alt=""
            {...(item === src ? { "data-boot-first": "" } : {})}
            onError={() => setFailed((prevFailed) => ({ ...prevFailed, [item]: true }))}
          />
        ))
      ) : (
        <div className="develop-hero-well" aria-hidden="true" />
      )}
      <div className="develop-hero-veil" aria-hidden="true" />
      {hint ? (
        <p className="develop-hero-hint" aria-live="polite">
          <span className="develop-hero-hint-box">
            <i aria-hidden="true" />
            <i aria-hidden="true" />
            <i aria-hidden="true" />
            <i aria-hidden="true" />
            {HERO_HINT}
          </span>
        </p>
      ) : null}
      <div className="develop-hero-band">
        <div className="develop-hero-copy">
          {src ? (
            <p className="develop-hero-shot">
              {lexicon.shotIn.deco} · {year}
            </p>
          ) : null}
          <h1 className="develop-hero-name">
            <ProfileName />
          </h1>
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
          {lead ? (
            <p className="develop-hero-lead">
              <TermText text={lead} />
            </p>
          ) : null}
          {quote ? (
            <p className="develop-hero-quote">
              <TermText text={quote} />
            </p>
          ) : null}
        </div>
      </div>
    </section>
  );
}

/**
 * 头图优先用已发布风光摄影；缺图时回退到块上钉死的一张。
 */
function collectHeroFrames(block: HomeBlock): HomeHeroFrame[] {
  const fromWorks = (block.query?.source === "works" ? listLandscapeHeroFrames() : []).filter(
    (frame) => starDrawWeight(frame.stars ?? 0) > 0,
  );
  if (fromWorks.length > 0) {
    return fromWorks;
  }
  if (block.heroSrc) {
    const size = photoSizes[block.heroSrc];
    return [
      {
        src: block.heroSrc,
        year: block.heroYear ?? profile.portraitYear,
        href: hrefForHeroSrc(block.heroSrc),
        width: size?.width,
        height: size?.height,
        fixed: true,
      },
    ];
  }
  return [];
}

/**
 * 视口内可抽的头图。回退图权重为 1，内容层按星级。
 */
function heroDraws(
  frames: HomeHeroFrame[],
  viewport: PhotoOrientation,
  failed: Record<string, true>,
): DrawSrc[] {
  return frames
    .filter((frame) => !failed[frame.src] && heroFrameFitsViewport(frame, viewport))
    .map((frame) => ({
      src: frame.src,
      weight: frame.fixed ? 1 : starDrawWeight(frame.stars ?? 0),
    }))
    .filter((item) => item.weight > 0);
}

/**
 * 回退头图按对象键找回所属风光摄影项目页。
 */
function hrefForHeroSrc(src: string): string | undefined {
  for (const work of listPublishedPhotoWorks()) {
    if (listWorkImages(work).some((media) => media.src === src)) {
      return hrefForPhotoWork(work);
    }
  }
  return undefined;
}

function WaypointSlices() {
  const reduced = usePrefersReducedMotion();
  const workSrcs = uniqueDraws(queryWorks({ source: "works" }).map((work) => drawOfCover(work)));
  const slices = [
    {
      id: lexicon.profileResume.key,
      label: lexicon.profileResume.zh,
      deco: lexicon.profileResume.deco,
      to: `/${lexicon.profileResume.key}`,
      srcs: uniqueDraws(profile.portraitSrcs.map((src) => fixedDraw(src))),
    },
    {
      id: lexicon.profileSkills.key,
      label: lexicon.profileSkills.zh,
      deco: lexicon.profileSkills.deco,
      to: `/${lexicon.profileSkills.key}`,
      srcs: uniqueDraws(skillSliceSrcs.map((src) => fixedDraw(src))),
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
      srcs: uniqueDraws([fixedDraw("notes/cover.webp")]),
    },
  ];
  const { advance, bind } = useComboRelay(slices.length, comboRelay4s, reduced, (index) => {
    return (slices[index]?.srcs.length ?? 0) >= 2;
  });

  return (
    <section className="develop-slices" id="waypoint-slices">
      {slices.map((item, index) => (
        <SliceCard key={item.id} {...item} advance={advance[index] ?? 0} {...bind(index)} />
      ))}
    </section>
  );
}

type SliceCardProps = {
  id: string;
  label: string;
  deco: string;
  to: string;
  srcs: DrawSrc[];
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLElement>) => void;
  onPointerLeave: () => void;
};

/**
 * 切片图卡：四栏点名换图。本栏近 5 张不重复。换张为 0.9s 淡入。
 * 只挂刚离开的一张、当前张和已抽好的下一张，换上当前张后再抽下下一张。
 */
function SliceCard({ id, label, deco, to, srcs, advance, onPointerEnter, onPointerLeave }: SliceCardProps) {
  const { shown, stacked, fail } = useComboShown(srcs, advance, comboRelay4s.noRepeat);

  return (
    <Link
      className="develop-slice"
      to={to}
      data-slice={id}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {stacked.length > 0 ? (
        stacked.map((src) => (
          <img
            key={src}
            className={src === shown ? "is-on" : undefined}
            src={assetUrl(src)}
            alt=""
            onError={() => fail(src)}
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
 * 去掉空值和重复路径，保持首次出现的顺序。
 */
type FrameShot = {
  src: string;
  title: string;
  href: string;
  weight: number;
};

type FrameLane = {
  id: string;
  caption: string;
  featured: boolean;
  shots: FrameShot[];
};

function SelectedFrames({ ia }: { block: HomeBlock; ia: IaRecord }) {
  const reduced = usePrefersReducedMotion();
  const lanes = useMemo(() => collectHomeFrameLanes(ia.id), [ia.id]);
  const { advance, bind } = useComboRelay(lanes.length, comboRelay3s, reduced, (index) => {
    return (lanes[index]?.shots.length ?? 0) >= 2;
  });

  return (
    <section className="develop-frames" id="selected-frames">
      {lanes.map((lane, index) => (
        <FrameLaneCard key={lane.id} lane={lane} advance={advance[index] ?? 0} {...bind(index)} />
      ))}
    </section>
  );
}

/**
 * 精选五格：风光、展馆、数字孪生、景观效果图、游戏开发。
 * 摄影两格按类型或自由标签筛静帧，不再按旧栏目夹。
 */
function collectHomeFrameLanes(iaId: string): FrameLane[] {
  return [
    {
      id: lexicon.landscapePhoto.key,
      caption: lexicon.landscapePhoto.zh,
      featured: true,
      shots: shotsOfPhotoFacet({ themes: [lexicon.landscapePhoto.key] }),
    },
    {
      id: photoExhibitTag,
      caption: photoExhibitTitle,
      featured: false,
      shots: shotsOfPhotoFacet({ tags: [photoExhibitTag] }),
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

function shotsOfPhotoFacet(partial: { themes?: string[]; tags?: string[] }): FrameShot[] {
  const shots: FrameShot[] = [];
  for (const frame of listMatchingPhotoFrames(partial)) {
    const draw = drawOfImage(frame.media);
    if (!draw) {
      continue;
    }
    shots.push({
      src: draw.src,
      title: frame.work.title,
      href: hrefForPhotoWork(frame.work),
      weight: draw.weight,
    });
  }
  return shots;
}

function shotsOfChannel(channel: string, iaId: string): FrameShot[] {
  const weightOf = new Map(channelStillDraws(channel).map((item) => [item.src, item.weight]));
  const shots: FrameShot[] = [];
  const seen = new Set<string>();
  for (const work of listPublishedWorks(channel)) {
    const href = hrefForWork(work, iaId);
    for (const media of work.media) {
      const src = media.kind === "video" ? media.poster : media.src;
      const weight = src ? weightOf.get(src) : undefined;
      if (!src || weight == null || seen.has(src)) {
        continue;
      }
      seen.add(src);
      shots.push({ src, title: work.title, href, weight });
    }
  }
  return shots;
}

function shotsOfGames(): FrameShot[] {
  const shots: FrameShot[] = [];
  for (const game of gameProjects) {
    const href = `/${lexicon.gameDev.key}/${game.id}`;
    for (const shot of game.screenshots) {
      if (!shot.src || shot.kind === "video") {
        continue;
      }
      const draw = contentDraw(shot.src, shot);
      if (!draw) {
        continue;
      }
      shots.push({ src: draw.src, title: game.title, href, weight: draw.weight });
    }
  }
  return shots;
}

function FrameLaneCard({
  lane,
  advance,
  onPointerEnter,
  onPointerLeave,
}: {
  lane: FrameLane;
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLElement>) => void;
  onPointerLeave: () => void;
}) {
  const srcs = lane.shots.map((shot) => ({ src: shot.src, weight: shot.weight }));
  const { shown, stacked, fail } = useComboShown(srcs, advance, comboRelay3s.noRepeat);
  const shot = lane.shots.find((item) => item.src === shown);

  if (!shown || !shot) {
    return (
      <div
        className={`develop-frame${lane.featured ? " is-featured" : ""}`}
        onPointerEnter={onPointerEnter}
        onPointerLeave={onPointerLeave}
      >
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
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {stacked.map((item) => (
        <img
          key={item}
          className={item === shown ? "is-on" : undefined}
          src={assetUrl(item)}
          alt=""
          onError={() => fail(item)}
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
        <p className="develop-close-lead">待填写描述</p>
      </div>
      <ContactIcons channels={profile.contactChannels} />
      <HomeBeian />
    </section>
  );
}
