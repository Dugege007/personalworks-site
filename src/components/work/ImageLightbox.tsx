import {
  useEffect,
  useRef,
  useState,
  type MouseEvent,
  type MutableRefObject,
  type PointerEvent as ReactPointerEvent,
  type TransitionEvent,
} from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import type { PhotoExif } from "../../content/photoExif";
import { pausePageLenis, resumePageLenis } from "../../hooks/useLenis";
import { resolveLead } from "../../content/copyDisplay";
import { assetUrl } from "../../lib/assets";
import { PhotoExifStrip } from "./ExifMarks";
import { NavMark } from "./NavMarks";
import "../../styles/image-lightbox.css";

export type LightboxShot = {
  src: string;
  alt: string;
  label: string;
  kind?: "image" | "video";
  poster?: string;
  description?: string;
  exif?: PhotoExif;
};

/** 总览灯箱回拍摄详情；详情灯箱不传。 */
export type LightboxViewAll = {
  href: string;
  title: string;
  from?: string;
};

type ImageLightboxProps = {
  images: LightboxShot[];
  index: number;
  title: string;
  summary?: string;
  /** 有值时右侧用「查看全部 {title}」替换只读拍摄名。 */
  viewAll?: LightboxViewAll;
  videoProgressRef?: MutableRefObject<Map<string, number>>;
  onVideoTime?: (src: string, time: number) => void;
  videoMuted?: boolean;
  onVideoMutedChange?: (muted: boolean) => void;
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
const ZOOM_MIN = 1.5;
const ZOOM_MAX = 3;
const ZOOM_PER_PX = 0.003;
/** 小地图按图框宽高比缩放：相对图框长边，不锁死宽度。 */
const MAP_SCALE = 0.28;
const MAP_LONG_MAX = 160;
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

function clampZoom(value: number) {
  return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, value));
}

function clampPan(pan: Pan, el: HTMLElement, zoom: number): Pan {
  const maxX = ((zoom - 1) / 2) * el.offsetWidth;
  const maxY = ((zoom - 1) / 2) * el.offsetHeight;
  return {
    x: Math.max(-maxX, Math.min(maxX, pan.x)),
    y: Math.max(-maxY, Math.min(maxY, pan.y)),
  };
}

function zoomPanAt(
  el: HTMLElement,
  pan: Pan,
  fromZoom: number,
  toZoom: number,
  clientX: number,
  clientY: number,
): Pan {
  const frame = el.closest(".lrb-lightbox-frame");
  const rect = (frame ?? el).getBoundingClientRect();
  const sx = clientX - (rect.left + rect.width / 2);
  const sy = clientY - (rect.top + rect.height / 2);
  const k = toZoom / fromZoom;
  return clampPan(
    {
      x: sx - (sx - pan.x) * k,
      y: sy - (sy - pan.y) * k,
    },
    el,
    toZoom,
  );
}

function mapViewStyle(pan: Pan, el: HTMLElement | null, zoom: number) {
  const width = el?.offsetWidth || 1;
  const height = el?.offsetHeight || 1;
  return {
    left: `${(0.5 - (0.5 + pan.x / width) / zoom) * 100}%`,
    top: `${(0.5 - (0.5 + pan.y / height) / zoom) * 100}%`,
    width: `${100 / zoom}%`,
    height: `${100 / zoom}%`,
  };
}

function mapBoxStyle(size: { w: number; h: number }) {
  if (size.w < 1 || size.h < 1) {
    return undefined;
  }
  const long = Math.max(size.w, size.h);
  const scale = Math.min(MAP_SCALE, MAP_LONG_MAX / long);
  return {
    width: size.w * scale,
    height: size.h * scale,
  };
}

/**
 * 全站共用大图灯箱：图框与说明栏随画心宽度收束；
 * 翻页与关闭钮锚在视口边缘，不随横幅/纵幅切换位移；
 * 张次编号锚在图框下方外侧；未缩放时滚轮换张；
 * 单击进入缩放态（150%），滚轮在 150%～300% 变焦，再单击退出。
 */
export function ImageLightbox({
  images,
  index,
  title,
  summary,
  viewAll,
  videoProgressRef,
  onVideoTime,
  videoMuted = true,
  onVideoMutedChange,
  onClose,
  onPrev,
  onNext,
}: ImageLightboxProps) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const photoRef = useRef<HTMLImageElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const onVideoTimeRef = useRef(onVideoTime);
  onVideoTimeRef.current = onVideoTime;
  const wheelCarryRef = useRef(0);
  const zoomedRef = useRef(false);
  const zoomScaleRef = useRef(ZOOM_MIN);
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
  const [zoomScale, setZoomScale] = useState(ZOOM_MIN);
  const [pan, setPan] = useState<Pan>(ZERO_PAN);
  const [panning, setPanning] = useState(false);
  const [zoomMotion, setZoomMotion] = useState(false);
  const [frameBox, setFrameBox] = useState({ w: 0, h: 0 });
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
    zoomScaleRef.current = ZOOM_MIN;
    panRef.current = ZERO_PAN;
    dragRef.current = null;
    setZoomed(false);
    setZoomScale(ZOOM_MIN);
    setPan(ZERO_PAN);
    setPanning(false);
    setZoomMotion(false);
    setFrameBox({ w: 0, h: 0 });
  }, [index]);

  useEffect(() => {
    const item = images[index];
    if (!item || item.kind !== "video") {
      return;
    }
    const video = videoRef.current;
    if (!video) {
      return;
    }
    const start = videoProgressRef?.current.get(item.src) ?? 0;
    const report = () => {
      const time = video.currentTime;
      videoProgressRef?.current.set(item.src, time);
      onVideoTimeRef.current?.(item.src, time);
    };
    const apply = () => {
      const time = Number.isFinite(start) ? Math.max(0, start) : 0;
      if (Math.abs(video.currentTime - time) > 0.05) {
        video.currentTime = time;
      }
      video.pause();
    };
    const onPause = () => {
      if (!video.seeking) {
        report();
      }
    };
    video.muted = videoMuted;
    const onVolumeChange = () => {
      onVideoMutedChange?.(video.muted);
    };
    video.addEventListener("pause", onPause);
    video.addEventListener("volumechange", onVolumeChange);
    if (video.readyState >= 1) {
      apply();
    } else {
      video.addEventListener("loadedmetadata", apply);
    }
    return () => {
      video.removeEventListener("loadedmetadata", apply);
      video.removeEventListener("pause", onPause);
      video.removeEventListener("volumechange", onVolumeChange);
      video.pause();
      report();
    };
  }, [images, index, videoProgressRef]);

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
      if (zoomedRef.current) {
        const el = photoRef.current;
        if (!el) {
          return;
        }
        const from = zoomScaleRef.current;
        const to = clampZoom(from - wheelDelta(event) * ZOOM_PER_PX);
        if (to === from) {
          return;
        }
        const nextPan = zoomPanAt(el, panRef.current, from, to, event.clientX, event.clientY);
        zoomScaleRef.current = to;
        panRef.current = nextPan;
        setZoomScale(to);
        setPan(nextPan);
        return;
      }
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
    if (images.length < 2) {
      return;
    }
    const neighbors = [images[(index - 1 + images.length) % images.length], images[(index + 1) % images.length]];
    const currentSrc = images[index]?.src;
    for (const shot of neighbors) {
      if (!shot || shot.kind === "video" || shot.src === currentSrc) {
        continue;
      }
      const preload = new Image();
      preload.src = assetUrl(shot.src);
    }
  }, [images, index]);

  useEffect(() => {
    if (!zoomed) {
      return;
    }
    const el = photoRef.current;
    if (!el) {
      return;
    }
    const sync = () => {
      const next = clampPan(panRef.current, el, zoomScaleRef.current);
      panRef.current = next;
      setPan(next);
      setFrameBox({ w: el.offsetWidth, h: el.offsetHeight });
    };
    sync();
    const observer = new ResizeObserver(sync);
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
    const next = clampPan({ x: drag.panX + dx, y: drag.panY + dy }, el, zoomScaleRef.current);
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
      zoomScaleRef.current = ZOOM_MIN;
      panRef.current = ZERO_PAN;
      wheelCarryRef.current = 0;
      setPan(ZERO_PAN);
      setZoomScale(ZOOM_MIN);
      setZoomed(true);
      setZoomMotion(true);
      const photo = photoRef.current;
      if (photo) {
        setFrameBox({ w: photo.offsetWidth, h: photo.offsetHeight });
      }
      return;
    }
    zoomedRef.current = false;
    zoomScaleRef.current = ZOOM_MIN;
    panRef.current = ZERO_PAN;
    wheelCarryRef.current = 0;
    setZoomed(false);
    setZoomScale(ZOOM_MIN);
    setPan(ZERO_PAN);
    setZoomMotion(true);
  };

  const onPhotoTransitionEnd = (event: TransitionEvent<HTMLImageElement>) => {
    if (event.propertyName === "transform") {
      setZoomMotion(false);
    }
  };

  const shotIndex = `(${index + 1}/${images.length})`;
  const isVideo = current.kind === "video";
  const photoSrc = assetUrl(current.src);
  const posterSrc = current.poster ? assetUrl(current.poster) : undefined;
  const frameClass = [
    "lrb-lightbox-frame",
    isVideo ? "is-video" : "",
    zoomed ? "is-zoomed" : "",
    panning ? "is-panning" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const photoClass = ["lrb-lightbox-photo", isVideo ? "is-video" : "", zoomMotion ? "is-motion" : ""]
    .filter(Boolean)
    .join(" ");
  const photoStyle = zoomed
    ? { transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoomScale})` }
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
      <div className="lrb-lightbox-board" onClick={stop}>
        <div className="lrb-lightbox-stage">
          <div className="lrb-lightbox-picture">
            <figure className={frameClass}>
              {isVideo ? (
                <video
                  ref={videoRef}
                  className={photoClass}
                  src={photoSrc}
                  poster={posterSrc}
                  muted={videoMuted}
                  controls
                  playsInline
                  preload="metadata"
                />
              ) : (
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
              )}
              {zoomed && !isVideo ? (
                <div className="lrb-lightbox-map" style={mapBoxStyle(frameBox)} aria-hidden="true">
                  <img src={photoSrc} alt="" draggable={false} />
                  <span className="lrb-lightbox-map-view" style={mapViewStyle(pan, photoRef.current, zoomScale)} />
                </div>
              ) : null}
            </figure>
          </div>
          <div className={`lrb-lightbox-dock${current.exif ? " has-exif" : ""}`}>
            <PhotoExifStrip exif={current.exif} />
            <div className="lrb-lightbox-copy">
              {current.label || viewAll || (title && title !== current.label) ? (
                <div className="lrb-lightbox-names">
                  {current.label ? <p className="lrb-lightbox-shot">{current.label}</p> : null}
                  {viewAll ? (
                    <Link
                      className="lrb-lightbox-title lrb-lightbox-viewall"
                      to={viewAll.href}
                      state={viewAll.from ? { from: viewAll.from } : undefined}
                    >
                      查看全部 {viewAll.title}
                    </Link>
                  ) : title && title !== current.label ? (
                    <p className="lrb-lightbox-title">{title}</p>
                  ) : null}
                </div>
              ) : null}
              {resolveLead(summary) ? <p className="lrb-lightbox-lead">{resolveLead(summary)}</p> : null}
            </div>
          </div>
        </div>
        <p className="lrb-lightbox-index" aria-live="polite">
          {shotIndex}
        </p>
      </div>
      {images.length > 1 ? (
        <>
          <button
            className="lrb-lightbox-orb is-prev"
            type="button"
            aria-label="上一张"
            onClick={onPrev}
          >
            <span className="lrb-lightbox-orb-mark" aria-hidden="true">
              <NavMark name="prev" />
            </span>
          </button>
          <button
            className="lrb-lightbox-orb is-next"
            type="button"
            aria-label="下一张"
            onClick={onNext}
          >
            <span className="lrb-lightbox-orb-mark" aria-hidden="true">
              <NavMark name="next" />
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
        <span className="lrb-lightbox-orb-mark" aria-hidden="true">
          <NavMark name="close" />
        </span>
      </button>
    </div>,
    document.body,
  );
}
