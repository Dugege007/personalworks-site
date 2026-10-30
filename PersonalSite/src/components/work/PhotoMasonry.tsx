import {
  Children,
  isValidElement,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from "react";
import {
  PHOTO_MASONRY_GAP,
  masonryHeightsForPlacement,
  placeShortestColumns,
} from "./shortestColumn";

/**
 * 与 photo-masonry.css 的列数断点一致。一两项时仍为两列。
 */
export function photoMasonryColumnCount(itemCount: number, width: number): number {
  if (itemCount <= 2) {
    return 2;
  }
  if (width <= 560) {
    return 1;
  }
  if (width <= 900) {
    return 2;
  }
  if (width >= 1400) {
    return 4;
  }
  return 3;
}

function readColumnCount(itemCount: number): number {
  if (typeof window === "undefined") {
    return itemCount <= 2 ? 2 : 3;
  }
  return photoMasonryColumnCount(itemCount, window.innerWidth);
}

function sameNumbers(left: readonly number[], right: readonly number[]): boolean {
  if (left.length !== right.length) {
    return false;
  }
  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) {
      return false;
    }
  }
  return true;
}

function sameFlags(left: readonly boolean[], right: readonly boolean[]): boolean {
  if (left.length !== right.length) {
    return false;
  }
  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) {
      return false;
    }
  }
  return true;
}

/**
 * 媒体已经明确失败。加载尚未结束时 `complete` 为假，不能当成失败。
 */
function mediaLoadFailed(media: Element | null): boolean {
  if (media instanceof HTMLImageElement) {
    return media.complete && media.naturalWidth === 0 && media.currentSrc !== "";
  }
  if (media instanceof HTMLVideoElement) {
    return media.error != null;
  }
  return false;
}

function readGap(root: HTMLElement): number {
  const raw = getComputedStyle(root).getPropertyValue("--photo-masonry-gap").trim();
  const value = Number.parseFloat(raw);
  return Number.isFinite(value) && value >= 0 ? value : PHOTO_MASONRY_GAP;
}

/**
 * 优先用已经排出来的边框盒高度。图片尚未解码、盒子仍为 0 时，按列宽和固有比例估算。
 */
function readRenderedHeight(node: HTMLElement): number {
  if (node.offsetHeight > 0) {
    return node.offsetHeight;
  }
  const media = node.querySelector("img, video");
  if (!(media instanceof HTMLImageElement) && !(media instanceof HTMLVideoElement)) {
    return 0;
  }
  const naturalWidth = media instanceof HTMLImageElement ? media.naturalWidth : media.videoWidth;
  const naturalHeight = media instanceof HTMLImageElement ? media.naturalHeight : media.videoHeight;
  if (naturalWidth > 0 && naturalHeight > 0 && node.offsetWidth > 0) {
    return Math.max(1, Math.round((node.offsetWidth * naturalHeight) / naturalWidth));
  }
  return 0;
}

function itemStyle(column: number, top: number, columnCount: number, gap: number): CSSProperties {
  const tracks = Math.max(1, columnCount);
  const span = `(100% - ${gap}px * ${tracks - 1}) / ${tracks}`;
  const width = `calc(${span})`;
  const left = column <= 0 ? "0px" : `calc(${column} * (${span} + ${gap}px))`;
  return { top, left, width };
}

function itemKey(item: ReactNode, index: number): string | number {
  if (isValidElement(item) && item.key != null) {
    return item.key;
  }
  return index;
}

type PhotoMasonryProps = {
  count: number;
  children: ReactNode;
};

/**
 * 摄影图墙。下一张进入当前最矮的列，并列时取最左列。
 * 高度来自卡片实际渲染；懒加载或解码改变高度后重新分配。
 * 图片或视频已经失败且仍无高度时不占槽，成功后再量高排入。
 * 子项按传入顺序留在 DOM 中，阅读与键盘顺序不跟列走。
 */
export function PhotoMasonry({ count, children }: PhotoMasonryProps) {
  const rootRef = useRef<HTMLDivElement>(null);
  const items = useMemo(() => Children.toArray(children), [children]);
  const itemSignature = items
    .map((item, index) => (isValidElement(item) && item.key != null ? String(item.key) : String(index)))
    .join("\u0000");
  const [cols, setCols] = useState(() => readColumnCount(count));
  const [measured, setMeasured] = useState<number[]>([]);
  const [failed, setFailed] = useState<boolean[]>([]);
  const [gap, setGap] = useState(PHOTO_MASONRY_GAP);
  const [columnWidth, setColumnWidth] = useState(0);

  useLayoutEffect(() => {
    const root = rootRef.current;
    if (!root) {
      return;
    }

    let frame = 0;
    let cancelled = false;

    const measure = () => {
      if (cancelled || !root.isConnected) {
        return;
      }
      const nextCols = readColumnCount(count);
      const nextGap = readGap(root);
      const next = Array.from({ length: items.length }, () => 0);
      const nextFailed = Array.from({ length: items.length }, () => false);
      let width = 0;
      for (const node of root.querySelectorAll<HTMLElement>("[data-masonry-index]")) {
        const index = Number(node.dataset.masonryIndex);
        if (!Number.isInteger(index) || index < 0 || index >= items.length) {
          continue;
        }
        next[index] = readRenderedHeight(node);
        nextFailed[index] = mediaLoadFailed(node.querySelector("img, video"));
        if (width === 0 && node.offsetWidth > 0) {
          width = Math.round(node.offsetWidth);
        }
      }
      setCols((current) => (current === nextCols ? current : nextCols));
      setGap((current) => (current === nextGap ? current : nextGap));
      setColumnWidth((current) => (current === width ? current : width));
      setMeasured((current) => (sameNumbers(current, next) ? current : next));
      setFailed((current) => (sameFlags(current, nextFailed) ? current : nextFailed));
    };

    const schedule = () => {
      if (frame !== 0) {
        return;
      }
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        measure();
      });
    };

    measure();

    const observer = new ResizeObserver(() => {
      schedule();
    });
    observer.observe(root);
    for (const node of root.querySelectorAll<HTMLElement>("[data-masonry-index]")) {
      observer.observe(node);
      const media = node.querySelector("img, video");
      if (media) {
        observer.observe(media);
      }
    }

    const onMedia = (event: Event) => {
      const target = event.target;
      if (target instanceof HTMLImageElement || target instanceof HTMLVideoElement) {
        measure();
      }
    };
    root.addEventListener("load", onMedia, true);
    root.addEventListener("error", onMedia, true);
    root.addEventListener("loadedmetadata", onMedia, true);

    const queries = [
      window.matchMedia("(max-width: 560px)"),
      window.matchMedia("(max-width: 900px)"),
      window.matchMedia("(min-width: 1400px)"),
    ];
    for (const query of queries) {
      query.addEventListener("change", schedule);
    }

    return () => {
      cancelled = true;
      if (frame !== 0) {
        window.cancelAnimationFrame(frame);
      }
      observer.disconnect();
      root.removeEventListener("load", onMedia, true);
      root.removeEventListener("error", onMedia, true);
      root.removeEventListener("loadedmetadata", onMedia, true);
      for (const query of queries) {
        query.removeEventListener("change", schedule);
      }
    };
  }, [count, itemSignature, items.length]);

  const resolvedHeights = useMemo(
    () =>
      masonryHeightsForPlacement(
        Array.from({ length: items.length }, (_, index) => measured[index] ?? 0),
        columnWidth > 0 ? columnWidth : 1,
        failed,
      ),
    [columnWidth, failed, items.length, measured],
  );
  const layout = useMemo(
    () => placeShortestColumns(resolvedHeights, cols, gap),
    [cols, gap, resolvedHeights],
  );

  return (
    <div
      className="photo-masonry is-row"
      data-count={count}
      data-layout="shortest-column"
      ref={rootRef}
      style={{ height: layout.containerHeight }}
    >
      {items.map((item, index) => {
        const placement = layout.placements[index];
        const column = placement?.column ?? 0;
        const collapsed = failed[index] === true && !((measured[index] ?? 0) > 0);
        return (
          <div
            className={collapsed ? "photo-masonry-item is-load-failed" : "photo-masonry-item"}
            data-masonry-index={index}
            data-column={column}
            aria-hidden={collapsed ? true : undefined}
            inert={collapsed ? true : undefined}
            key={itemKey(item, index)}
            style={itemStyle(column, placement?.top ?? 0, cols, gap)}
          >
            {item}
          </div>
        );
      })}
    </div>
  );
}
