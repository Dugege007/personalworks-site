import { useEffect, useRef, useState, type PointerEvent } from "react";
import {
  nextComboIndex,
  pickWeightedSrc,
  pushComboRecent,
  uniqueDraws,
  type ComboRelayOptions,
  type DrawSrc,
} from "./comboCycle";
import { whenSiteBootReleased } from "./siteBoot";

/**
 * 一组图框共用一个计时器：固定间隔换一格，悬停只跳过该格。
 * 图池不足两张的格由 canAdvance 标成不可换，这一轮不占时间。
 */
export function useComboRelay(
  count: number,
  options: ComboRelayOptions,
  reduced: boolean,
  canAdvance?: (index: number) => boolean,
) {
  const [advance, setAdvance] = useState<number[]>(() => Array.from({ length: count }, () => 0));
  const turnRef = useRef(0);
  const hoverRef = useRef<number | null>(null);
  const countRef = useRef(count);
  const canRef = useRef(canAdvance);
  const intervalRef = useRef(options.intervalMs);
  countRef.current = count;
  canRef.current = canAdvance;
  intervalRef.current = options.intervalMs;

  useEffect(() => {
    setAdvance((prev) => {
      if (prev.length === count) {
        return prev;
      }
      return Array.from({ length: count }, (_, index) => prev[index] ?? 0);
    });
  }, [count]);

  useEffect(() => {
    let cancelled = false;
    let timer = 0;
    const stop = () => {
      window.clearTimeout(timer);
      timer = 0;
    };
    const schedule = () => {
      stop();
      if (cancelled || reduced) {
        return;
      }
      timer = window.setTimeout(() => {
        const total = countRef.current;
        const index = nextComboIndex(
          turnRef.current,
          hoverRef.current,
          total,
          (item) => canRef.current?.(item) ?? true,
        );
        if (index >= 0 && total > 0) {
          turnRef.current = (index + 1) % total;
          setAdvance((prev) => prev.map((value, item) => (item === index ? value + 1 : value)));
        }
        schedule();
      }, intervalRef.current);
    };
    void whenSiteBootReleased().then(() => {
      if (!cancelled) {
        schedule();
      }
    });
    return () => {
      cancelled = true;
      stop();
    };
  }, [reduced, options.intervalMs]);

  /**
   * 只记下当前停着的格。到点跳过它，不把整组停掉。
   */
  function bind(index: number) {
    return {
      onPointerEnter(event: PointerEvent<HTMLElement>) {
        if (event.pointerType !== "mouse") {
          return;
        }
        hoverRef.current = index;
      },
      onPointerLeave() {
        if (hoverRef.current === index) {
          hoverRef.current = null;
        }
      },
    };
  }

  return { advance, bind };
}

/**
 * 打开时定下当前张，并抽出一张先挂上、尚未点亮的下一张。
 */
function openComboSlot(pool: readonly DrawSrc[], noRepeat: number) {
  const current = pickWeightedSrc(pool, [], noRepeat);
  const primed = pickWeightedSrc(pool, current ? [current] : [], noRepeat);
  return {
    current,
    previous: "",
    primed: primed && primed !== current ? primed : "",
  };
}

/**
 * 单格图池：打开时随机第一张，并先把下一张挂上。
 * 换张时点亮已经在页面上的那张，再另挂一张待用。
 * 新图若在点亮的同一帧才插入，不透明度会直接是 1，0.9s 淡入不会发生。
 */
export function useComboShown(pool: readonly DrawSrc[], advance: number, noRepeat: number) {
  const [failed, setFailed] = useState<Record<string, true>>({});
  const [slot, setSlot] = useState(() => openComboSlot(pool, noRepeat));
  const recentRef = useRef<string[]>([]);
  const slotRef = useRef(slot);
  const shownRef = useRef("");
  const availableRef = useRef<DrawSrc[]>([]);

  const draws = uniqueDraws(pool.filter((item) => item.src && !failed[item.src]));
  const available = draws.map((item) => item.src);
  const shown = available.includes(slot.current) ? slot.current : (available[0] ?? "");
  availableRef.current = draws;
  slotRef.current = slot;
  shownRef.current = shown;
  const stacked = [slot.previous, shown, slot.primed].filter(
    (src, index, list): src is string => Boolean(src) && !failed[src] && list.indexOf(src) === index,
  );

  useEffect(() => {
    if (advance < 1) {
      return;
    }
    const drawsNow = availableRef.current;
    const cur = shownRef.current;
    const reserved = slotRef.current.primed;
    // 当前张放在近窗末尾。池张数不超过不重复上限时，最早的一张才能回到候选。
    const next =
      reserved && reserved !== cur && drawsNow.some((item) => item.src === reserved)
        ? reserved
        : pickWeightedSrc(drawsNow, [...recentRef.current, cur].filter(Boolean), noRepeat);
    if (!next || next === cur) {
      return;
    }
    recentRef.current = pushComboRecent(recentRef.current, cur, noRepeat);
    const upcoming = pickWeightedSrc(drawsNow, [...recentRef.current, next], noRepeat);
    setSlot({
      previous: cur,
      current: next,
      primed: upcoming !== next ? upcoming : "",
    });
  }, [advance, noRepeat]);

  return {
    shown,
    stacked,
    available,
    fail(src: string) {
      setFailed((prev) => ({ ...prev, [src]: true }));
    },
  };
}
