import {
  useEffect,
  useRef,
  useState,
  type MouseEvent,
  type PointerEvent as ReactPointerEvent,
  type TransitionEvent,
} from "react";
import { createPortal } from "react-dom";
import { formatShotCaption } from "../../content/works";
import { pausePageLenis, resumePageLenis } from "../../hooks/useLenis";
import { assetUrl } from "../../lib/assets";

type LightboxImage = {
  src: string;
  alt: string;
  label: string;
};

type ImageLightboxProps = {
  images: LightboxImage[];
  index: number;
  title: string;
  summary?: string;
  onClose: () => void;
  onPrev: () => void;
  onNext: () => void;
};

type Pan = {
  x: number;
  y: number;
};

const WHEEL_NOTCH = 100;
const POINTER_MOVE_PX = 4;
const ZOOM = 1.5;
const ZERO_PAN: Pan = { x: 0, y: 0 };

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

function clampPan(pan: Pan, el: HTMLElement): Pan {
  const maxX = ((ZOOM - 1) / 2) * el.offsetWidth;
  const maxY = ((ZOOM - 1) / 2) * el.offsetHeight;
  return {
    x: Math.max(-maxX, Math.min(maxX, pan.x)),
    y: Math.max(-maxY, Math.min(maxY, pan.y)),
  };
}

function mapViewStyle(pan: Pan, el: HTMLElement | null) {
  const width = el?.offsetWidth || 1;
  const height = el?.offsetHeight || 1;
  return {
    left: `${(0.5 - (0.5 + pan.x / width) / ZOOM) * 100}%`,
    top: `${(0.5 - (0.5 + pan.y / height) / ZOOM) * 100}%`,
    width: `${100 / ZOOM}%`,
    height: `${100 / ZOOM}%`,
  };
}

function CloseMark() {
  return (
    <svg className="lrb-lightbox-orb-x" viewBox="0 0 20 20" aria-hidden="true">
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        d="M5.5 5.5 14.5 14.5M14.5 5.5 5.5 14.5"
      />
    </svg>
  );
}

/**
 * 效果图全屏查看：图左右圆钮翻页，右上角圆钮关闭，滚轮换张；
 * 点击图框内放大至 150%，可拖拽查看，再单击还原。
 */
export function ImageLightbox({
  images,
  index,
  title,
  summary,
  onClose,
  onPrev,
  onNext,
}: ImageLightboxProps) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const photoRef = useRef<HTMLImageElement>(null);
  const wheelCarryRef = useRef(0);
  const zoomedRef = useRef(false);
  const panRef = useRef<Pan>(ZERO_PAN);
  const dragRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    panX: number;
    panY: number;
    moved: boolean;
  } | null>(null);
  const [zoomed, setZoomed] = useState(false);
  const [pan, setPan] = useState<Pan>(ZERO_PAN);
  const [panning, setPanning] = useState(false);
  const [zoomMotion, setZoomMotion] = useState(false);
  const current = images[index];

  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.documentElement.dataset.lightbox = "open";
    pausePageLenis();
    closeRef.current?.focus();
    return () => {
      document.body.style.overflow = previous;
      delete document.documentElement.dataset.lightbox;
      resumePageLenis();
    };
  }, []);

  useEffect(() => {
    wheelCarryRef.current = 0;
    zoomedRef.current = false;
    panRef.current = ZERO_PAN;
    dragRef.current = null;
    setZoomed(false);
    setPan(ZERO_PAN);
    setPanning(false);
    setZoomMotion(false);
  }, [index]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
      if (event.key === "ArrowLeft") {
        event.preventDefault();
        onPrev();
      }
      if (event.key === "ArrowRight") {
        event.preventDefault();
        onNext();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, onPrev, onNext]);

  useEffect(() => {
    const root = rootRef.current;
    if (!root) {
      return;
    }
    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      event.stopPropagation();
      if (images.length < 2) {
        return;
      }
      wheelCarryRef.current += wheelDelta(event);
      if (wheelCarryRef.current >= WHEEL_NOTCH) {
        wheelCarryRef.current = 0;
        onNext();
        return;
      }
      if (wheelCarryRef.current <= -WHEEL_NOTCH) {
        wheelCarryRef.current = 0;
        onPrev();
      }
    };
    root.addEventListener("wheel", onWheel, { passive: false });
    return () => root.removeEventListener("wheel", onWheel);
  }, [images.length, onNext, onPrev]);

  useEffect(() => {
    if (!zoomed) {
      return;
    }
    const el = photoRef.current;
    if (!el) {
      return;
    }
    const observer = new ResizeObserver(() => {
      const next = clampPan(panRef.current, el);
      panRef.current = next;
      setPan(next);
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [zoomed, current?.src]);

  if (!current) {
    return null;
  }

  const stop = (event: MouseEvent) => {
    event.stopPropagation();
  };

  const onPhotoPointerDown = (event: ReactPointerEvent<HTMLImageElement>) => {
    if (event.button !== 0) {
      return;
    }
    event.preventDefault();
    setZoomMotion(false);
    dragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      panX: panRef.current.x,
      panY: panRef.current.y,
      moved: false,
    };
    if (zoomedRef.current) {
      event.currentTarget.setPointerCapture(event.pointerId);
    }
  };

  const onPhotoPointerMove = (event: ReactPointerEvent<HTMLImageElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    const dx = event.clientX - drag.startX;
    const dy = event.clientY - drag.startY;
    if (!drag.moved && dx * dx + dy * dy > POINTER_MOVE_PX * POINTER_MOVE_PX) {
      drag.moved = true;
      if (zoomedRef.current) {
        setPanning(true);
      }
    }
    if (!zoomedRef.current || !drag.moved) {
      return;
    }
    const el = photoRef.current;
    if (!el) {
      return;
    }
    const next = clampPan({ x: drag.panX + dx, y: drag.panY + dy }, el);
    panRef.current = next;
    setPan(next);
  };

  const endPhotoPointer = (event: ReactPointerEvent<HTMLImageElement>, commit: boolean) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    dragRef.current = null;
    setPanning(false);
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    if (!commit || drag.moved) {
      return;
    }
    if (!zoomedRef.current) {
      zoomedRef.current = true;
      panRef.current = ZERO_PAN;
      setPan(ZERO_PAN);
      setZoomed(true);
      setZoomMotion(true);
      return;
    }
    zoomedRef.current = false;
    panRef.current = ZERO_PAN;
    setZoomed(false);
    setPan(ZERO_PAN);
    setZoomMotion(true);
  };

  const onPhotoTransitionEnd = (event: TransitionEvent<HTMLImageElement>) => {
    if (event.propertyName === "transform") {
      setZoomMotion(false);
    }
  };

  const shotLine = formatShotCaption(index, images.length, current.label);
  const photoSrc = assetUrl(current.src);
  const frameClass = [
    "lrb-lightbox-frame",
    zoomed ? "is-zoomed" : "",
    panning ? "is-panning" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const photoClass = ["lrb-lightbox-photo", zoomMotion ? "is-motion" : ""]
    .filter(Boolean)
    .join(" ");
  const photoStyle = zoomed
    ? { transform: `translate(${pan.x}px, ${pan.y}px) scale(${ZOOM})` }
    : undefined;

  return createPortal(
    <div
      ref={rootRef}
      className="lrb-lightbox"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <button className="lrb-lightbox-scrim" type="button" aria-label="关闭预览" onClick={onClose} />
      <div className="lrb-lightbox-stage" onClick={stop}>
        <div className="lrb-lightbox-picture">
          <figure className={frameClass}>
            <img
              ref={photoRef}
              className={photoClass}
              src={photoSrc}
              alt={current.alt}
              draggable={false}
              style={photoStyle}
              onDragStart={(event) => event.preventDefault()}
              onPointerDown={onPhotoPointerDown}
              onPointerMove={onPhotoPointerMove}
              onPointerUp={(event) => endPhotoPointer(event, true)}
              onPointerCancel={(event) => endPhotoPointer(event, false)}
              onTransitionEnd={onPhotoTransitionEnd}
            />
            {zoomed ? (
              <div className="lrb-lightbox-map" aria-hidden="true">
                <img src={photoSrc} alt="" draggable={false} />
                <span className="lrb-lightbox-map-view" style={mapViewStyle(pan, photoRef.current)} />
              </div>
            ) : null}
          </figure>
          {images.length > 1 ? (
            <>
              <button
                className="lrb-lightbox-orb is-prev"
                type="button"
                aria-label="上一张"
                onClick={onPrev}
              >
                <span className="lrb-lightbox-orb-mark" aria-hidden="true">
                  ‹
                </span>
              </button>
              <button
                className="lrb-lightbox-orb is-next"
                type="button"
                aria-label="下一张"
                onClick={onNext}
              >
                <span className="lrb-lightbox-orb-mark" aria-hidden="true">
                  ›
                </span>
              </button>
            </>
          ) : null}
          <button
            ref={closeRef}
            className="lrb-lightbox-orb is-close"
            type="button"
            aria-label="关闭"
            onClick={onClose}
          >
            <CloseMark />
          </button>
        </div>
        <div className="lrb-lightbox-dock">
          <p className="lrb-lightbox-shot" aria-live="polite">
            {shotLine}
          </p>
          <p className="lrb-lightbox-title">{title}</p>
          {summary ? <p className="lrb-lightbox-lead">{summary}</p> : null}
        </div>
      </div>
    </div>,
    document.body,
  );
}
