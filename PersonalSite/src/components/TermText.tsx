import { Fragment, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { glossaryBook } from "../content/glossaryDoc";
import { annotateTerms, tipParts, type GlossaryEntry, type TipPart } from "../content/glossary";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/term-tip.css";

const HOVER_MS = 500;
const BRIDGE_MS = 120;

type TermTextProps = {
  text: string;
};

type TermLabelProps = {
  label: string;
  entry: GlossaryEntry;
  plain?: boolean;
  className?: string;
};

type Place = "above" | "below";

type TipBox = {
  top: number;
  left: number;
  place: Place;
};

/**
 * 在允许的说明文字里标出名词，并展开对应提示。
 */
export function TermText({ text }: TermTextProps) {
  const pieces = annotateTerms(text, glossaryBook);
  return (
    <>
      {pieces.map((piece, index) =>
        piece.kind === "text" ? (
          <Fragment key={index}>{piece.text}</Fragment>
        ) : (
          <TermMark key={`${piece.text}-${index}`} label={piece.text} entry={piece.entry} />
        ),
      )}
    </>
  );
}

/**
 * 已知名词的提示。技能名走这条，不在正文里再扫一遍。
 */
export function TermLabel({ label, entry, plain, className }: TermLabelProps) {
  return <TermMark label={label} entry={entry} plain={plain} className={className} />;
}

function TermMark({
  label,
  entry,
  plain,
  className,
}: {
  label: string;
  entry: GlossaryEntry;
  plain?: boolean;
  className?: string;
}) {
  const { locale } = usePrefs();
  const tipId = useId();
  const wrapRef = useRef<HTMLSpanElement>(null);
  const tipRef = useRef<HTMLSpanElement>(null);
  const hoverTimer = useRef<number>(0);
  const closeTimer = useRef<number>(0);
  const [open, setOpen] = useState(false);
  const [pinned, setPinned] = useState(false);
  const [box, setBox] = useState<TipBox | null>(null);
  const parts = tipParts(entry, locale);
  const measureRef = useRef<() => void>(() => undefined);

  const clearHover = () => {
    window.clearTimeout(hoverTimer.current);
  };

  const clearClose = () => {
    window.clearTimeout(closeTimer.current);
  };

  const close = () => {
    clearHover();
    clearClose();
    setOpen(false);
    setPinned(false);
  };

  const openNow = (pin: boolean) => {
    clearHover();
    clearClose();
    setOpen(true);
    setPinned(pin);
  };

  useEffect(() => {
    return () => {
      clearHover();
      clearClose();
    };
  }, []);

  useEffect(() => {
    if (!pinned) {
      return;
    }
    const onPointer = (event: MouseEvent) => {
      const target = event.target as Node;
      if (wrapRef.current?.contains(target) || tipRef.current?.contains(target)) {
        return;
      }
      close();
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        close();
      }
    };
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [pinned]);

  useLayoutEffect(() => {
    if (!open) {
      setBox(null);
      return;
    }
    const measure = () => {
      const anchor = wrapRef.current?.getBoundingClientRect();
      const tip = tipRef.current;
      if (!anchor || !tip) {
        return;
      }
      const header = readHeaderHeight();
      const gap = 8;
      const tipHeight = tip.offsetHeight;
      const tipWidth = tip.offsetWidth;
      const place: Place = anchor.top - header < tipHeight + gap ? "below" : "above";
      let left = anchor.left + anchor.width / 2 - tipWidth / 2;
      const margin = 8;
      const maxLeft = window.innerWidth - tipWidth - margin;
      left = Math.min(Math.max(left, margin), Math.max(margin, maxLeft));
      const top = place === "above" ? anchor.top - gap - tipHeight : anchor.bottom + gap;
      setBox({ top, left, place });
    };
    measureRef.current = measure;
    measure();
    window.addEventListener("resize", measure);
    window.addEventListener("scroll", measure, true);
    return () => {
      window.removeEventListener("resize", measure);
      window.removeEventListener("scroll", measure, true);
    };
  }, [open, parts, entry.image]);

  const scheduleOpen = () => {
    if (open || pinned) {
      clearClose();
      return;
    }
    clearHover();
    hoverTimer.current = window.setTimeout(() => setOpen(true), HOVER_MS);
  };

  const scheduleClose = () => {
    clearHover();
    if (pinned) {
      return;
    }
    clearClose();
    closeTimer.current = window.setTimeout(() => setOpen(false), BRIDGE_MS);
  };

  return (
    <span
      className="term-wrap"
      ref={wrapRef}
      onMouseEnter={scheduleOpen}
      onMouseLeave={scheduleClose}
    >
      <button
        type="button"
        className={["term-mark", plain ? "is-plain" : "", className].filter(Boolean).join(" ")}
        aria-expanded={open}
        aria-controls={open ? tipId : undefined}
        onFocus={scheduleOpen}
        onBlur={scheduleClose}
        onClick={(event) => {
          event.preventDefault();
          event.stopPropagation();
          if (open && pinned) {
            close();
            return;
          }
          openNow(true);
        }}
      >
        {label}
      </button>
      {open
        ? createPortal(
            <span
              id={tipId}
              ref={tipRef}
              className={`term-tip is-fixed is-${box?.place ?? "above"}`}
              style={{
                top: box?.top ?? 0,
                left: box?.left ?? 0,
                visibility: box ? "visible" : "hidden",
              }}
              onMouseEnter={clearClose}
            >
              <span className="term-tip-card">
                {entry.image ? (
                  <img
                    className="term-tip-img"
                    src={`/glossary/${encodeURIComponent(entry.image)}`}
                    alt={entry.term}
                    onLoad={() => measureRef.current()}
                  />
                ) : null}
                <span className="term-tip-body">
                  {parts.map((part, index) => (
                    <TipFragment key={index} part={part} />
                  ))}
                </span>
              </span>
            </span>,
            document.body,
          )
        : null}
    </span>
  );
}

function TipFragment({ part }: { part: TipPart }) {
  if (part.kind === "bold") {
    return <strong>{part.text}</strong>;
  }
  if (part.kind === "link") {
    if (part.href.startsWith("/") && !part.href.startsWith("//")) {
      return (
        <Link className="term-tip-link" to={part.href}>
          {part.text}
        </Link>
      );
    }
    return (
      <a className="term-tip-link" href={part.href} target="_blank" rel="noopener noreferrer">
        {part.text}
      </a>
    );
  }
  return <span>{part.text}</span>;
}

function readHeaderHeight(): number {
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--header-h").trim();
  const value = Number.parseFloat(raw);
  return Number.isFinite(value) ? value : 0;
}
