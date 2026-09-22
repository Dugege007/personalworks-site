import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { stockPlaceholderSrc } from "../content/stockMedia";
import { channelsOfKind } from "../ia/query";
import {
  hrefForDevelopKind,
  isDevelopWorkKind,
  listDevelopKindDoors,
  type DevelopKindDoor,
} from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { whenSiteBootReleased } from "../lib/siteBoot";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
import "../styles/develop-work.css";

const GATE_IDLE_MIN_MS = 3000;
const GATE_IDLE_MAX_MS = 10000;

/**
 * 显影作品第一层：四门非对称画册，不列细目条目。
 */
export function WorkIndexPage() {
  const [params] = useSearchParams();
  const kind = params.get("kind");
  if (kind && isDevelopWorkKind(kind)) {
    return <Navigate to={hrefForDevelopKind(kind)} replace />;
  }

  return <WorkGate />;
}

/**
 * 四门共用一个 3–10 秒倒计时；到点只切一门，左到右再循环。
 * 鼠标停在某一门上时跳过该门，计时不整组停。
 */
function WorkGate() {
  const reduced = usePrefersReducedMotion();
  const doors = listDevelopKindDoors();
  const [advance, setAdvance] = useState(() => doors.map(() => 0));
  const turnRef = useRef(0);
  const hoverIndexRef = useRef<number | null>(null);
  const timerRef = useRef(0);

  useEffect(() => {
    let cancelled = false;
    void whenSiteBootReleased().then(() => {
      if (!cancelled) {
        scheduleGateTurn();
      }
    });
    return () => {
      cancelled = true;
      window.clearTimeout(timerRef.current);
    };
  }, [reduced]);

  function scheduleGateTurn() {
    window.clearTimeout(timerRef.current);
    timerRef.current = 0;
    if (reduced) {
      return;
    }
    const delay = GATE_IDLE_MIN_MS + Math.random() * (GATE_IDLE_MAX_MS - GATE_IDLE_MIN_MS);
    timerRef.current = window.setTimeout(() => {
      const index = nextGateIndex(turnRef.current, hoverIndexRef.current, doors.length);
      turnRef.current = (index + 1) % doors.length;
      setAdvance((prev) => prev.map((value, itemIndex) => (itemIndex === index ? value + 1 : value)));
      scheduleGateTurn();
    }, delay);
  }

  /**
   * 只记录当前停着的门，供到点时跳过；不中断倒计时。
   */
  function handleDoorPointerEnter(index: number, event: PointerEvent<HTMLAnchorElement>) {
    if (event.pointerType !== "mouse") {
      return;
    }
    hoverIndexRef.current = index;
  }

  function handleDoorPointerLeave(index: number) {
    if (hoverIndexRef.current === index) {
      hoverIndexRef.current = null;
    }
  }

  return (
    <div className="develop-work-gate" data-theme={lexicon.workIndex.key}>
      <h1 className="develop-work-visually-hidden">{lexicon.workIndex.zh}</h1>
      {doors.map((door, index) => (
        <KindDoorCard
          key={door.kind}
          door={door}
          index={index}
          advance={advance[index] ?? 0}
          onPointerEnter={(event) => handleDoorPointerEnter(index, event)}
          onPointerLeave={() => handleDoorPointerLeave(index)}
        />
      ))}
    </div>
  );
}

type KindDoorCardProps = {
  door: DevelopKindDoor;
  index: number;
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLAnchorElement>) => void;
  onPointerLeave: () => void;
};

/**
 * 门类图卡：由四门共用计时器点名换图；预载下一张，与头图一样交叉淡化；本门最近五张不重复。
 */
function KindDoorCard({ door, index, advance, onPointerEnter, onPointerLeave }: KindDoorCardProps) {
  const fallback = stockPlaceholderSrc(
    door.kind === lexicon.gameDev.key
      ? lexicon.gameDev.key
      : (channelsOfKind(door.kind)[0] ?? door.kind),
    1,
  );
  const srcs = door.srcs.length > 0 ? door.srcs : [door.coverSrc, fallback].filter((item): item is string => Boolean(item));
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [current, setCurrent] = useState(() => pickFirstDoorSrc(srcs));
  const [lastSrc, setLastSrc] = useState("");
  const [upcomingSrc, setUpcomingSrc] = useState("");
  const recentRef = useRef<string[]>([]);
  const currentRef = useRef(current);
  const upcomingRef = useRef("");
  const poolRef = useRef<string[]>([]);

  const pool = srcs.filter((src) => !failed[src]);
  const shown = pool.includes(current) ? current : (pool[0] ?? "");
  poolRef.current = pool;
  currentRef.current = shown;
  const shownSrcs = [lastSrc, shown, upcomingSrc].filter(
    (item, itemIndex, list): item is string => Boolean(item) && !failed[item] && list.indexOf(item) === itemIndex,
  );

  useEffect(() => {
    const upcoming = pickNextDoorSrc(poolRef.current, currentRef.current, recentRef.current);
    if (!upcoming || upcoming === currentRef.current) {
      return;
    }
    upcomingRef.current = upcoming;
    setUpcomingSrc(upcoming);
  }, []);

  useEffect(() => {
    if (advance < 1) {
      return;
    }
    const reserved = upcomingRef.current;
    const next =
      reserved && reserved !== currentRef.current && poolRef.current.includes(reserved)
        ? reserved
        : pickNextDoorSrc(poolRef.current, currentRef.current, recentRef.current);
    if (!next || next === currentRef.current) {
      return;
    }
    recentRef.current = [...recentRef.current, currentRef.current].slice(-4);
    setLastSrc(currentRef.current);
    setCurrent(next);
    currentRef.current = next;
    const upcoming = pickNextDoorSrc(poolRef.current, next, recentRef.current);
    upcomingRef.current = upcoming === next ? "" : upcoming;
    setUpcomingSrc(upcomingRef.current);
  }, [advance]);

  return (
    <Link
      className={`develop-work-gate-cell is-${index + 1}`}
      to={door.href}
      data-kind={door.kind}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {shownSrcs.length > 0 ? (
        shownSrcs.map((src) => (
          <img
            key={src}
            className={src === shown ? "is-on" : undefined}
            src={assetUrl(src)}
            alt=""
            {...(index === 0 && src === shown ? { "data-boot-first": "" } : {})}
            onError={() => setFailed((prev) => ({ ...prev, [src]: true }))}
          />
        ))
      ) : (
        <span className={`develop-work-well is-kind-${door.kind}`} aria-hidden="true" />
      )}
      <span className="develop-work-copy">
        <strong>{door.zh}</strong>
        <em>{door.deco}</em>
      </span>
    </Link>
  );
}

/**
 * 轮到被悬停的门则顺延到下一门，避免整组停表。
 */
function nextGateIndex(from: number, skip: number | null, count: number): number {
  if (count < 1) {
    return 0;
  }
  let index = ((from % count) + count) % count;
  if (skip === null || count < 2) {
    return index;
  }
  for (let step = 0; step < count; step += 1) {
    if (index !== skip) {
      return index;
    }
    index = (index + 1) % count;
  }
  return index;
}

/**
 * 打开作品墙时从本门图池随机抽首张，不钉死封面。
 */
function pickFirstDoorSrc(pool: string[]): string {
  if (pool.length === 0) {
    return "";
  }
  return pool[Math.floor(Math.random() * pool.length)] ?? "";
}

/**
 * 从图池取下一张：最近四张加当前共五张不重复；池不足时窗口收窄。
 */
function pickNextDoorSrc(pool: string[], current: string, recent: string[]): string {
  if (pool.length < 2) {
    return current;
  }
  const windowSize = Math.min(4, pool.length - 1);
  const forbidden = new Set([...recent, current].slice(-windowSize));
  const candidates = pool.filter((src) => !forbidden.has(src));
  const bag = candidates.length > 0 ? candidates : pool.filter((src) => src !== current);
  return bag[Math.floor(Math.random() * bag.length)] ?? current;
}
