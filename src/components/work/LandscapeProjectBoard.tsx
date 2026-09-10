import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type PointerEvent,
} from "react";
import {
  formatShotCaption,
  formatStartedOn,
  listWorkImages,
  type WorkRecord,
} from "../../content/works";
import { pausePageLenis, resumePageLenis } from "../../hooks/useLenis";
import { assetUrl } from "../../lib/assets";
import { ImageLightbox } from "./ImageLightbox";
import "../../styles/develop-landscape-board.css";

type LandscapeProjectBoardProps = {
  works: WorkRecord[];
};

type OpenShot = {
  workId: string;
  index: number;
};

const SCALE_FLOOR = 0.78;
const SAT_FLOOR = 0.48;
const SCALE_SPAN = 3;
const SAT_SPAN = 3;
const PAGE_MS = 540;
const PAGE_MS_RAPID = 180;
const SETTLE_GAP = 8;
const WHEEL_NOTCH = 100;
const POINTER_MOVE_PX = 4;

function rankT(distance: number, span: number) {
  return Math.min(distance / span, 1);
}

function pointerMoved(from: { x: number; y: number }, to: { x: number; y: number }) {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  return dx * dx + dy * dy > POINTER_MOVE_PX * POINTER_MOVE_PX;
}

function isMousePointer(event: PointerEvent | globalThis.PointerEvent) {
  return event.pointerType === "mouse";
}

/**
 * 景观效果图细目：按项目分块。图条是双侧叠层焦点条（2D overlapping focus strip）：
 * 悬停抢焦，距离越远越小、饱和度越低，重叠约半幅，焦点永远在最前。
 * 对照 React Bits Depth Carousel 的重叠、后退变暗与 transform 缩放，收成平铺双侧，不引入其库或 3D 扇叠。
 * 循环是三份首尾相接：从尾张再往后是下一段的首张，动画结束后无缝回到中间份。
 */
export function LandscapeProjectBoard({ works }: LandscapeProjectBoardProps) {
  const [open, setOpen] = useState<OpenShot | null>(null);
  const activeWork = works.find((item) => item.id === open?.workId);
  const activeImages = useMemo(
    () =>
      activeWork
        ? listWorkImages(activeWork).flatMap((item) =>
            item.src
              ? [
                  {
                    src: item.src,
                    alt: `${activeWork.title} ${item.label}`,
                    label: item.label,
                  },
                ]
              : [],
          )
        : [],
    [activeWork],
  );

  const close = useCallback(() => setOpen(null), []);
  const step = useCallback(
    (delta: number) => {
      setOpen((current) => {
        if (!current) {
          return current;
        }
        const work = works.find((item) => item.id === current.workId);
        const count = work ? listWorkImages(work).length : 0;
        if (count === 0) {
          return current;
        }
        return {
          workId: current.workId,
          index: (current.index + delta + count) % count,
        };
      });
    },
    [works],
  );
  const toPrev = useCallback(() => step(-1), [step]);
  const toNext = useCallback(() => step(1), [step]);

  return (
    <div className="lrb">
      {works.map((work) => (
        <ProjectModule
          key={work.id}
          work={work}
          syncIndex={open?.workId === work.id ? open.index : null}
          onOpen={(index) => setOpen({ workId: work.id, index })}
        />
      ))}
      {open && activeWork && activeImages.length > 0 ? (
        <ImageLightbox
          images={activeImages}
          index={open.index}
          title={activeWork.title}
          summary={activeWork.summary}
          onClose={close}
          onPrev={toPrev}
          onNext={toNext}
        />
      ) : null}
    </div>
  );
}

type ProjectModuleProps = {
  work: WorkRecord;
  syncIndex: number | null;
  onOpen: (index: number) => void;
};

function logicalIndex(slot: number, count: number) {
  if (count <= 0) {
    return 0;
  }
  return ((slot % count) + count) % count;
}

function stripOrigin(strip: HTMLElement) {
  const box = strip.getBoundingClientRect();
  const style = getComputedStyle(strip);
  return box.left + Number.parseFloat(style.paddingLeft || "0");
}

function stripEnd(strip: HTMLElement) {
  const box = strip.getBoundingClientRect();
  const style = getComputedStyle(strip);
  return box.right - Number.parseFloat(style.paddingRight || "0");
}

function wheelDelta(event: WheelEvent) {
  const raw = Math.abs(event.deltaY) >= Math.abs(event.deltaX) ? event.deltaY : event.deltaX;
  if (event.deltaMode === 1) {
    return raw * WHEEL_NOTCH;
  }
  if (event.deltaMode === 2) {
    return raw * WHEEL_NOTCH * 8;
  }
  return raw;
}

function ProjectModule({ work, syncIndex, onOpen }: ProjectModuleProps) {
  const shots = listWorkImages(work);
  const count = shots.length;
  const looping = count >= 2;
  const [focusIndex, setFocusIndex] = useState(() => (count >= 2 ? count : 0));
  const [endPad, setEndPad] = useState(0);
  const stripRef = useRef<HTMLDivElement>(null);
  const wrapRef = useRef<HTMLDivElement>(null);
  const focusRef = useRef<HTMLButtonElement>(null);
  const pinXRef = useRef<number | null>(null);
  const navDurationRef = useRef(PAGE_MS);
  const lastNavAtRef = useRef(0);
  const scrollAnimRef = useRef(0);
  const animGenRef = useRef(0);
  const wheelCarryRef = useRef(0);
  const mouseOnStripRef = useRef(false);
  const homeAlignRef = useRef(true);
  const focusIndexRef = useRef(focusIndex);
  const recenterTimerRef = useRef(0);
  const easeLockUntilRef = useRef(0);
  const navTargetRef = useRef<number | null>(null);
  const originPinXRef = useRef<number | null>(null);
  const pointerPosRef = useRef({ x: 0, y: 0 });
  const lockPointerPosRef = useRef({ x: 0, y: 0 });
  focusIndexRef.current = focusIndex;
  const loopedShots = useMemo(() => {
    if (!looping) {
      return shots.map((shot, slot) => ({ shot, logical: slot, slot }));
    }
    return Array.from({ length: count * 3 }, (_, slot) => ({
      shot: shots[slot % count],
      logical: slot % count,
      slot,
    }));
  }, [count, looping, shots]);
  const tags = [
    work.startedOn ? formatStartedOn(work.startedOn) : work.year,
    work.place,
    work.siteType,
    ...(work.tags ?? []),
  ].filter((item): item is string => Boolean(item));

  const animateScrollTo = useCallback((target: number, duration: number, onDone?: () => void) => {
    const strip = stripRef.current;
    if (!strip) {
      onDone?.();
      return;
    }
    const gen = ++animGenRef.current;
    const finish = () => {
      if (gen === animGenRef.current) {
        onDone?.();
      }
    };
    const max = Math.max(0, strip.scrollWidth - strip.clientWidth);
    const to = Math.max(0, Math.min(max, target));
    const from = strip.scrollLeft;
    window.cancelAnimationFrame(scrollAnimRef.current);
    if (
      Math.abs(to - from) < 1 ||
      duration <= 0 ||
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    ) {
      strip.scrollLeft = to;
      finish();
      return;
    }
    const started = performance.now();
    const tick = (now: number) => {
      if (gen !== animGenRef.current) {
        return;
      }
      const t = Math.min(1, (now - started) / duration);
      const eased = 1 - (1 - t) ** 4;
      strip.scrollLeft = from + (to - from) * eased;
      if (t < 1) {
        scrollAnimRef.current = window.requestAnimationFrame(tick);
      } else {
        finish();
      }
    };
    scrollAnimRef.current = window.requestAnimationFrame(tick);
  }, []);

  const alignHome = useCallback(() => {
    if (!homeAlignRef.current) {
      return;
    }
    const strip = stripRef.current;
    const nodes = strip ? [...strip.querySelectorAll<HTMLElement>(".lrb-shot")] : [];
    const home = looping ? nodes[count] : nodes[0];
    if (!strip || !home) {
      return;
    }
    strip.scrollLeft += home.getBoundingClientRect().left - stripOrigin(strip);
  }, [count, looping]);

  const applyOverlap = useCallback(() => {
    const strip = stripRef.current;
    if (!strip) {
      return;
    }
    const buttons = [...strip.querySelectorAll<HTMLElement>(".lrb-shot")];
    buttons.forEach((btn, index) => {
      btn.style.marginLeft = index === 0 ? "0px" : `${-btn.offsetWidth * 0.5}px`;
    });
    alignHome();
  }, [alignHome]);

  const releasePaging = useCallback(() => {
    stripRef.current?.classList.remove("is-paging");
    navTargetRef.current = null;
    originPinXRef.current = null;
  }, []);

  const stealFocusByHover = useCallback(
    (slot: number) => {
      if (slot < 0 || slot === focusIndexRef.current) {
        return;
      }
      homeAlignRef.current = false;
      pinXRef.current = null;
      animGenRef.current += 1;
      window.clearTimeout(recenterTimerRef.current);
      releasePaging();
      lockPointerPosRef.current = { ...pointerPosRef.current };
      easeLockUntilRef.current = performance.now() + PAGE_MS;
      setFocusIndex(slot);
    },
    [releasePaging],
  );

  const settleEnds = useCallback(() => {
    const strip = stripRef.current;
    const focus = focusRef.current;
    if (!strip || !focus) {
      return;
    }
    const origin = stripOrigin(strip);
    const end = stripEnd(strip);
    const focusBox = focus.getBoundingClientRect();
    const logical = logicalIndex(focusIndexRef.current, count);
    if (logical === 0 && focusBox.left > origin + SETTLE_GAP) {
      animateScrollTo(strip.scrollLeft + (focusBox.left - origin), PAGE_MS);
      return;
    }
    if (count > 1 && logical === count - 1 && focusBox.right < end - SETTLE_GAP) {
      animateScrollTo(strip.scrollLeft + (focusBox.right - end), PAGE_MS);
    }
  }, [animateScrollTo, count]);

  const recenterLoop = useCallback(() => {
    if (!looping) {
      releasePaging();
      return;
    }
    const slot = focusIndexRef.current;
    if (slot >= count && slot < count * 2) {
      releasePaging();
      return;
    }
    navTargetRef.current = null;
    originPinXRef.current = null;
    const dest = count + logicalIndex(slot, count);
    const strip = stripRef.current;
    const nodes = strip ? [...strip.querySelectorAll<HTMLElement>(".lrb-shot")] : [];
    const from = nodes[slot];
    pinXRef.current = from?.getBoundingClientRect().left ?? null;
    navDurationRef.current = 0;
    setFocusIndex(dest);
  }, [count, looping, releasePaging]);

  const pinFocus = useCallback(() => {
    const pinX = pinXRef.current;
    const strip = stripRef.current;
    if (pinX == null || !strip) {
      return;
    }
    const nodes = [...strip.querySelectorAll<HTMLElement>(".lrb-shot")];
    const focus = nodes[focusIndex] ?? focusRef.current;
    if (!focus) {
      pinXRef.current = null;
      return;
    }
    const duration = navDurationRef.current;
    pinXRef.current = null;
    animateScrollTo(
      strip.scrollLeft + (focus.getBoundingClientRect().left - pinX),
      duration,
      recenterLoop,
    );
  }, [animateScrollTo, focusIndex, recenterLoop]);

  const navigate = useCallback(
    (delta: number) => {
      if (count < 2 || delta === 0) {
        return;
      }
      const now = performance.now();
      const inFlight = navTargetRef.current != null;
      navDurationRef.current =
        !inFlight && now - lastNavAtRef.current < 280 ? PAGE_MS_RAPID : PAGE_MS;
      lastNavAtRef.current = now;
      easeLockUntilRef.current = now + navDurationRef.current;
      lockPointerPosRef.current = { ...pointerPosRef.current };
      homeAlignRef.current = false;
      const strip = stripRef.current;
      const nodes = strip ? [...strip.querySelectorAll<HTMLElement>(".lrb-shot")] : [];
      const maxSlot = count * 3 - 1;
      if (!inFlight) {
        navTargetRef.current = focusIndexRef.current;
        originPinXRef.current = nodes[focusIndexRef.current]?.getBoundingClientRect().left ?? null;
      }
      const fromSlot = navTargetRef.current ?? focusIndexRef.current;
      const nextIndex = Math.max(0, Math.min(maxSlot, fromSlot + delta));
      navTargetRef.current = nextIndex;
      strip?.classList.add("is-paging");
      pinXRef.current = originPinXRef.current;
      setFocusIndex(nextIndex);
      const duration = navDurationRef.current;
      window.clearTimeout(recenterTimerRef.current);
      recenterTimerRef.current = window.setTimeout(() => {
        if (focusIndexRef.current === nextIndex) {
          recenterLoop();
        }
      }, duration + 48);
    },
    [count, recenterLoop],
  );

  const holdPrev = useHoldStep(() => navigate(-1));
  const holdNext = useHoldStep(() => navigate(1));

  useEffect(() => {
    if (syncIndex == null) {
      return;
    }
    pinXRef.current = null;
    setFocusIndex(looping ? count + logicalIndex(syncIndex, count) : syncIndex);
  }, [count, looping, syncIndex]);

  useLayoutEffect(() => {
    const strip = stripRef.current;
    if (!strip) {
      return;
    }
    const updatePad = () => setEndPad(looping ? 0 : Math.round(strip.clientWidth * 0.5));
    updatePad();
    const observer = new ResizeObserver(updatePad);
    observer.observe(strip);
    return () => observer.disconnect();
  }, [looping]);

  useLayoutEffect(() => {
    alignHome();
  }, [alignHome, endPad]);

  useLayoutEffect(() => {
    pinFocus();
  }, [focusIndex, pinFocus]);

  useLayoutEffect(() => {
    const strip = stripRef.current;
    if (!strip) {
      return;
    }

    const images = [...strip.querySelectorAll("img")];
    const observer = new ResizeObserver(applyOverlap);
    observer.observe(strip);
    images.forEach((img) => {
      if (!img.complete) {
        img.addEventListener("load", applyOverlap);
      }
    });
    applyOverlap();
    return () => {
      observer.disconnect();
      images.forEach((img) => img.removeEventListener("load", applyOverlap));
    };
  }, [applyOverlap, loopedShots.length]);

  useEffect(() => {
    const wrap = wrapRef.current;
    if (!wrap) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      if (!mouseOnStripRef.current) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      if (count < 2) {
        return;
      }
      wheelCarryRef.current += wheelDelta(event);
      let steps = 0;
      while (wheelCarryRef.current >= WHEEL_NOTCH) {
        wheelCarryRef.current -= WHEEL_NOTCH;
        steps += 1;
      }
      while (wheelCarryRef.current <= -WHEEL_NOTCH) {
        wheelCarryRef.current += WHEEL_NOTCH;
        steps -= 1;
      }
      if (steps !== 0) {
        navigate(steps);
      }
    };

    const onPointerLeave = (event: globalThis.PointerEvent) => {
      if (event.pointerType && event.pointerType !== "mouse") {
        return;
      }
      mouseOnStripRef.current = false;
      wheelCarryRef.current = 0;
      resumePageLenis();
      settleEnds();
    };

    wrap.addEventListener("wheel", onWheel, { passive: false });
    wrap.addEventListener("pointerleave", onPointerLeave);
    return () => {
      wrap.removeEventListener("wheel", onWheel);
      wrap.removeEventListener("pointerleave", onPointerLeave);
      window.clearTimeout(recenterTimerRef.current);
      window.cancelAnimationFrame(scrollAnimRef.current);
      animGenRef.current += 1;
      if (mouseOnStripRef.current) {
        mouseOnStripRef.current = false;
        resumePageLenis();
      }
    };
  }, [count, navigate, settleEnds]);

  const hoverEaseLocked = () =>
    performance.now() < easeLockUntilRef.current ||
    Boolean(stripRef.current?.classList.contains("is-paging"));

  const onStripPointerEnter = (event: PointerEvent<HTMLDivElement>) => {
    if (!isMousePointer(event)) {
      return;
    }
    mouseOnStripRef.current = true;
    pointerPosRef.current = { x: event.clientX, y: event.clientY };
    pausePageLenis();
  };

  const onStripPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (!isMousePointer(event)) {
      return;
    }
    pointerPosRef.current = { x: event.clientX, y: event.clientY };
    if (!hoverEaseLocked() || !pointerMoved(lockPointerPosRef.current, pointerPosRef.current)) {
      return;
    }
    const strip = stripRef.current;
    if (!strip) {
      return;
    }
    const hit = document.elementFromPoint(event.clientX, event.clientY)?.closest(".lrb-shot");
    if (!(hit instanceof HTMLElement) || !strip.contains(hit)) {
      return;
    }
    stealFocusByHover([...strip.querySelectorAll(".lrb-shot")].indexOf(hit));
  };

  const onStripPointerLeave = (event: PointerEvent<HTMLDivElement>) => {
    if (!isMousePointer(event)) {
      return;
    }
    mouseOnStripRef.current = false;
    resumePageLenis();
  };

  const logicalFocus = logicalIndex(focusIndex, count);
  const railLeft = count > 0 ? `${(logicalFocus / count) * 100}%` : "0%";
  const railWidth = count > 0 ? `${100 / count}%` : "100%";

  return (
    <article className="lrb-project">
      <header className="lrb-head">
        <h2>{work.title}</h2>
        {tags.length > 0 ? (
          <ul className="lrb-tags">
            {tags.map((tag) => (
              <li key={tag}>{tag}</li>
            ))}
          </ul>
        ) : null}
      </header>
      {work.summary ? <p className="lrb-lead">{work.summary}</p> : null}
      {shots.length > 0 ? (
        <div
          className="lrb-strip-wrap"
          ref={wrapRef}
          data-lenis-prevent
          onPointerEnter={onStripPointerEnter}
          onPointerMove={onStripPointerMove}
          onPointerLeave={onStripPointerLeave}
        >
          <div className="lrb-strip" ref={stripRef}>
            <div className="lrb-strip-pad" style={{ width: endPad }} aria-hidden="true" />
            {loopedShots.map(({ shot, logical, slot }) => {
              const distance = Math.abs(slot - focusIndex);
              const scaleT = rankT(distance, SCALE_SPAN);
              const satT = rankT(distance, SAT_SPAN);
              const focused = slot === focusIndex;
              return (
                <button
                  key={`${slot}-${shot.src}`}
                  ref={focused ? focusRef : undefined}
                  className={focused ? "lrb-shot is-focus" : "lrb-shot"}
                  type="button"
                  aria-current={focused ? "true" : undefined}
                  aria-label={`查看${work.title} ${shot.label}`}
                  style={{
                    ["--rank-scale" as string]: String(1 - scaleT * (1 - SCALE_FLOOR)),
                    ["--rank-sat" as string]: String(1 - satT * (1 - SAT_FLOOR)),
                    ["--rank-z" as string]: String(loopedShots.length - distance),
                  }}
                  onPointerEnter={(event) => {
                    if (!isMousePointer(event)) {
                      return;
                    }
                    pointerPosRef.current = { x: event.clientX, y: event.clientY };
                    if (slot === focusIndexRef.current) {
                      return;
                    }
                    if (
                      hoverEaseLocked() &&
                      !pointerMoved(lockPointerPosRef.current, pointerPosRef.current)
                    ) {
                      return;
                    }
                    stealFocusByHover(slot);
                  }}
                  onClick={() => onOpen(logical)}
                >
                  <img src={shot.src ? assetUrl(shot.src) : ""} alt="" />
                  {focused ? (
                    <span className="lrb-shot-cap">
                      {formatShotCaption(logical, count, shot.label)}
                    </span>
                  ) : null}
                </button>
              );
            })}
            <div className="lrb-strip-pad" style={{ width: endPad }} aria-hidden="true" />
          </div>
          {shots.length > 1 ? (
            <>
              <button
                className="lrb-strip-step is-prev"
                type="button"
                aria-label="上一张"
                {...holdPrev}
              >
                <span className="lrb-strip-step-mark" aria-hidden="true">
                  ‹
                </span>
              </button>
              <button
                className="lrb-strip-step is-next"
                type="button"
                aria-label="下一张"
                {...holdNext}
              >
                <span className="lrb-strip-step-mark" aria-hidden="true">
                  ›
                </span>
              </button>
            </>
          ) : null}
          <div className="lrb-strip-rail" aria-hidden="true">
            <span className="lrb-strip-rail-thumb" style={{ left: railLeft, width: railWidth }} />
          </div>
        </div>
      ) : null}
    </article>
  );
}

function useHoldStep(step: () => void) {
  const holdRef = useRef(0);
  const repeatRef = useRef(0);
  const repeatingRef = useRef(false);

  const clear = () => {
    window.clearTimeout(holdRef.current);
    window.clearInterval(repeatRef.current);
    holdRef.current = 0;
    repeatRef.current = 0;
  };

  useEffect(() => () => clear(), []);

  return {
    onPointerDown: (event: PointerEvent<HTMLButtonElement>) => {
      if (event.button !== 0) {
        return;
      }
      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      repeatingRef.current = false;
      holdRef.current = window.setTimeout(() => {
        repeatingRef.current = true;
        step();
        repeatRef.current = window.setInterval(step, 140);
      }, 500);
    },
    onPointerUp: () => {
      const wasRepeating = repeatingRef.current;
      clear();
      repeatingRef.current = false;
      if (!wasRepeating) {
        step();
      }
    },
    onPointerCancel: () => {
      clear();
      repeatingRef.current = false;
    },
  };
}
