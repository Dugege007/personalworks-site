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
import { NavMark } from "./NavMarks";
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
const WHEEL_NOTCH = 100;
const POINTER_MOVE_PX = 8;
const DRAG_STEP_PX = 72;

function rankT(distance: number, span: number) {
  return Math.min(distance / span, 1);
}

function isMousePointer(event: PointerEvent | globalThis.PointerEvent) {
  return event.pointerType === "mouse";
}

/**
 * 景观效果图细目：按项目分块。图条是居中焦点条：
 * 焦点永远在图条水平中线；点两侧图滑到中间成焦；点焦点图开灯箱；
 * 滚轮与左右拖拽按张换焦；悬停两侧只强调、不抢焦。
 * 循环仍是三份首尾相接，动画结束后无缝回到中间份。
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
  const navDurationRef = useRef(0);
  const lastNavAtRef = useRef(0);
  const scrollAnimRef = useRef(0);
  const animGenRef = useRef(0);
  const wheelCarryRef = useRef(0);
  const mouseOnStripRef = useRef(false);
  const focusIndexRef = useRef(focusIndex);
  const navTargetRef = useRef<number | null>(null);
  const suppressClickRef = useRef(false);
  const dragRef = useRef({
    pointerId: -1,
    startX: 0,
    lastX: 0,
    acc: 0,
    moved: false,
  });
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

  const applyOverlap = useCallback(() => {
    const strip = stripRef.current;
    if (!strip) {
      return;
    }
    const buttons = [...strip.querySelectorAll<HTMLElement>(".lrb-shot")];
    buttons.forEach((btn, index) => {
      btn.style.marginLeft = index === 0 ? "0px" : `${-btn.offsetWidth * 0.5}px`;
    });
  }, []);

  const centerFocus = useCallback(
    (duration: number, onDone?: () => void) => {
      const strip = stripRef.current;
      const focus = focusRef.current;
      if (!strip || !focus) {
        onDone?.();
        return;
      }
      const stripBox = strip.getBoundingClientRect();
      const focusBox = focus.getBoundingClientRect();
      const stripMid = stripBox.left + strip.clientWidth / 2;
      const focusMid = focusBox.left + focusBox.width / 2;
      animateScrollTo(strip.scrollLeft + (focusMid - stripMid), duration, onDone);
    },
    [animateScrollTo],
  );

  const releasePaging = useCallback(() => {
    stripRef.current?.classList.remove("is-paging");
    navTargetRef.current = null;
  }, []);

  const recenterLoop = useCallback(() => {
    if (!looping) {
      releasePaging();
      centerFocus(0);
      return;
    }
    const slot = focusIndexRef.current;
    if (slot >= count && slot < count * 2) {
      releasePaging();
      centerFocus(0);
      return;
    }
    navTargetRef.current = null;
    const dest = count + logicalIndex(slot, count);
    navDurationRef.current = 0;
    setFocusIndex(dest);
  }, [centerFocus, count, looping, releasePaging]);

  const goToSlot = useCallback(
    (slot: number, duration?: number) => {
      if (count < 1) {
        return;
      }
      const maxSlot = looping ? count * 3 - 1 : count - 1;
      const nextIndex = Math.max(0, Math.min(maxSlot, slot));
      if (nextIndex === focusIndexRef.current && navTargetRef.current == null) {
        return;
      }
      const now = performance.now();
      navDurationRef.current = duration ?? PAGE_MS;
      lastNavAtRef.current = now;
      navTargetRef.current = nextIndex;
      stripRef.current?.classList.add("is-paging");
      setFocusIndex(nextIndex);
    },
    [count, looping],
  );

  const navigate = useCallback(
    (delta: number) => {
      if (count < 2 || delta === 0) {
        return;
      }
      const now = performance.now();
      const inFlight = navTargetRef.current != null;
      const duration = inFlight || now - lastNavAtRef.current < 280 ? PAGE_MS_RAPID : PAGE_MS;
      const fromSlot = navTargetRef.current ?? focusIndexRef.current;
      goToSlot(fromSlot + delta, duration);
    },
    [count, goToSlot],
  );

  const holdPrev = useHoldStep(() => navigate(-1));
  const holdNext = useHoldStep(() => navigate(1));

  useEffect(() => {
    if (syncIndex == null) {
      return;
    }
    const dest = looping ? count + logicalIndex(syncIndex, count) : syncIndex;
    navDurationRef.current = PAGE_MS;
    stripRef.current?.classList.add("is-paging");
    setFocusIndex(dest);
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
    centerFocus(navDurationRef.current, recenterLoop);
  }, [centerFocus, endPad, focusIndex, recenterLoop]);

  useLayoutEffect(() => {
    const strip = stripRef.current;
    if (!strip) {
      return;
    }

    const images = [...strip.querySelectorAll("img")];
    const onResize = () => {
      applyOverlap();
      if (!strip.classList.contains("is-paging")) {
        navDurationRef.current = 0;
        centerFocus(0);
      }
    };
    const observer = new ResizeObserver(onResize);
    observer.observe(strip);
    images.forEach((img) => {
      if (!img.complete) {
        img.addEventListener("load", onResize);
      }
    });
    onResize();
    return () => {
      observer.disconnect();
      images.forEach((img) => img.removeEventListener("load", onResize));
    };
  }, [applyOverlap, centerFocus, loopedShots.length]);

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

    wrap.addEventListener("wheel", onWheel, { passive: false });
    return () => {
      wrap.removeEventListener("wheel", onWheel);
      window.cancelAnimationFrame(scrollAnimRef.current);
      animGenRef.current += 1;
      if (mouseOnStripRef.current) {
        mouseOnStripRef.current = false;
        resumePageLenis();
      }
    };
  }, [count, navigate]);

  const onStripPointerEnter = (event: PointerEvent<HTMLDivElement>) => {
    if (!isMousePointer(event)) {
      return;
    }
    mouseOnStripRef.current = true;
    pausePageLenis();
  };

  const onStripPointerLeave = (event: PointerEvent<HTMLDivElement>) => {
    if (!isMousePointer(event)) {
      return;
    }
    mouseOnStripRef.current = false;
    resumePageLenis();
  };

  const onStripPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || count < 2) {
      return;
    }
    const target = event.target;
    if (
      target instanceof Element &&
      target.closest(".lrb-strip-step, .lrb-strip-rail")
    ) {
      return;
    }
    suppressClickRef.current = false;
    dragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      lastX: event.clientX,
      acc: 0,
      moved: false,
    };
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onStripPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (drag.pointerId !== event.pointerId) {
      return;
    }
    const dx = event.clientX - drag.lastX;
    drag.lastX = event.clientX;
    if (!drag.moved && Math.abs(event.clientX - drag.startX) > POINTER_MOVE_PX) {
      drag.moved = true;
      wrapRef.current?.classList.add("is-dragging");
    }
    if (!drag.moved) {
      return;
    }
    event.preventDefault();
    drag.acc += dx;
    let steps = 0;
    while (drag.acc <= -DRAG_STEP_PX) {
      drag.acc += DRAG_STEP_PX;
      steps += 1;
    }
    while (drag.acc >= DRAG_STEP_PX) {
      drag.acc -= DRAG_STEP_PX;
      steps -= 1;
    }
    if (steps !== 0) {
      suppressClickRef.current = true;
      const fromSlot = navTargetRef.current ?? focusIndexRef.current;
      goToSlot(fromSlot + steps, PAGE_MS_RAPID);
    }
  };

  const activateShot = (slot: number, logical: number) => {
    if (slot === focusIndexRef.current) {
      onOpen(logical);
      return;
    }
    goToSlot(slot);
  };

  const endDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (dragRef.current.pointerId !== event.pointerId) {
      return;
    }
    const didPage = suppressClickRef.current;
    dragRef.current.pointerId = -1;
    wrapRef.current?.classList.remove("is-dragging");
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    if (didPage) {
      return;
    }
    const hit = document.elementFromPoint(event.clientX, event.clientY);
    const btn = hit instanceof Element ? hit.closest<HTMLElement>(".lrb-shot") : null;
    if (!btn || !event.currentTarget.contains(btn)) {
      return;
    }
    const slot = Number(btn.dataset.slot);
    const logical = Number(btn.dataset.logical);
    if (!Number.isFinite(slot) || !Number.isFinite(logical)) {
      return;
    }
    suppressClickRef.current = true;
    activateShot(slot, logical);
  };

  const onShotClick = (slot: number, logical: number) => {
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }
    activateShot(slot, logical);
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
          onPointerLeave={onStripPointerLeave}
          onPointerDown={onStripPointerDown}
          onPointerMove={onStripPointerMove}
          onPointerUp={endDrag}
          onPointerCancel={endDrag}
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
                  data-slot={slot}
                  data-logical={logical}
                  aria-current={focused ? "true" : undefined}
                  aria-label={focused ? `放大${work.title} ${shot.label}` : `聚焦${work.title} ${shot.label}`}
                  style={{
                    ["--rank-scale" as string]: String(1 - scaleT * (1 - SCALE_FLOOR)),
                    ["--rank-sat" as string]: String(1 - satT * (1 - SAT_FLOOR)),
                    ["--rank-z" as string]: String(loopedShots.length - distance),
                  }}
                  onClick={() => onShotClick(slot, logical)}
                >
                  <img src={shot.src ? assetUrl(shot.src) : ""} alt="" draggable={false} />
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
                  <NavMark name="prev" />
                </span>
              </button>
              <button
                className="lrb-strip-step is-next"
                type="button"
                aria-label="下一张"
                {...holdNext}
              >
                <span className="lrb-strip-step-mark" aria-hidden="true">
                  <NavMark name="next" />
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
