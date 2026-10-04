import { useEffect, useRef, useState } from "react";
import { constructionSheetMark, constructionSheets } from "../../content/constructionSheet";
import type { WorkMedia } from "../../content/works";
import { assetUrl } from "../../lib/assets";
import { ImageLightbox, type LightboxShot } from "./ImageLightbox";
import { NavMark } from "./NavMarks";
import "../../styles/construction-sheets.css";

type ConstructionSheetDeskProps = {
  title: string;
  media: WorkMedia[];
};

type NetInfo = {
  saveData?: boolean;
};

/**
 * 预取下一张的字节，不创建 Image，避免在当前张之外再解码一张大图。
 */
function prefetchSheetBytes(href: string, signal: AbortSignal) {
  const connection = (navigator as Navigator & { connection?: NetInfo }).connection;
  if (connection?.saveData) {
    return;
  }
  void fetch(href, { signal, cache: "force-cache" })
    .then((response) => (response.ok ? response.arrayBuffer() : undefined))
    .catch(() => undefined);
}

/**
 * 景观施工图图桌：固定图台里按原比例看当前一张，目录只出短标。
 */
export function ConstructionSheetDesk({ title, media }: ConstructionSheetDeskProps) {
  const sheets = constructionSheets(media);
  const indexRef = useRef<HTMLOListElement>(null);
  const [index, setIndex] = useState(0);
  const [open, setOpen] = useState(false);
  const current = sheets[index];
  const nextSrc = sheets[index + 1]?.src;

  useEffect(() => {
    if (!nextSrc) {
      return;
    }
    const controller = new AbortController();
    prefetchSheetBytes(assetUrl(nextSrc), controller.signal);
    return () => controller.abort();
  }, [nextSrc]);

  useEffect(() => {
    if (open) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.altKey || event.ctrlKey || event.metaKey) {
        return;
      }
      const target = event.target;
      if (target instanceof HTMLElement && target.closest("input, textarea, select")) {
        return;
      }
      if (event.key === "ArrowLeft") {
        event.preventDefault();
        setIndex((value) => Math.max(0, value - 1));
      }
      if (event.key === "ArrowRight") {
        event.preventDefault();
        setIndex((value) => Math.min(sheets.length - 1, value + 1));
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, sheets.length]);

  useEffect(() => {
    const root = indexRef.current;
    const node = root?.querySelector<HTMLButtonElement>("button[aria-current='true']");
    if (!root || !node) {
      return;
    }
    const rootBox = root.getBoundingClientRect();
    const nodeBox = node.getBoundingClientRect();
    const above = nodeBox.top - rootBox.top;
    const below = nodeBox.bottom - rootBox.bottom;
    if (above < 0) {
      root.scrollTop += above;
    } else if (below > 0) {
      root.scrollTop += below;
    }
    const before = nodeBox.left - rootBox.left;
    const after = nodeBox.right - rootBox.right;
    if (before < 0) {
      root.scrollLeft += before;
    } else if (after > 0) {
      root.scrollLeft += after;
    }
  }, [index]);

  if (!current?.src) {
    return <p className="cds-desk-empty">这一套还没有可展示的图纸。</p>;
  }

  const shots: LightboxShot[] = sheets.map((item, sheetIndex) => ({
    src: item.src ?? "",
    alt: constructionSheetMark(item, sheetIndex),
    label: constructionSheetMark(item, sheetIndex),
    kind: "image",
  }));
  const mark = constructionSheetMark(current, index);
  const href = assetUrl(current.src);

  const step = (delta: number) => {
    setIndex((value) => {
      const next = value + delta;
      if (next < 0 || next >= sheets.length) {
        return value;
      }
      return next;
    });
  };

  return (
    <div className="cds-desk">
      <ol className="cds-desk-index" aria-label="图纸目录" ref={indexRef}>
        {sheets.map((item, sheetIndex) => {
          const sheetMark = constructionSheetMark(item, sheetIndex);
          const selected = sheetIndex === index;
          return (
            <li key={item.src}>
              <button
                type="button"
                aria-current={selected ? "true" : undefined}
                title={sheetMark}
                onClick={() => setIndex(sheetIndex)}
              >
                {sheetMark}
              </button>
            </li>
          );
        })}
      </ol>
      <div className="cds-desk-main">
        <div className="cds-desk-stage">
          <button type="button" className="cds-desk-open" aria-label="查看大图" onClick={() => setOpen(true)}>
            <img key={href} src={href} alt={mark} decoding="async" draggable={false} />
          </button>
        </div>
        <div className="cds-desk-bar">
          <button type="button" aria-label="上一张" disabled={index === 0} onClick={() => step(-1)}>
            <NavMark name="prev" />
          </button>
          <p className="cds-desk-count" aria-live="polite">
            {index + 1} / {sheets.length}
          </p>
          <button
            type="button"
            aria-label="下一张"
            disabled={index === sheets.length - 1}
            onClick={() => step(1)}
          >
            <NavMark name="next" />
          </button>
        </div>
      </div>
      {open ? (
        <ImageLightbox
          images={shots}
          index={index}
          title={title}
          preloadNeighbors={false}
          onClose={() => setOpen(false)}
          onPrev={() => step(-1)}
          onNext={() => step(1)}
        />
      ) : null}
    </div>
  );
}
