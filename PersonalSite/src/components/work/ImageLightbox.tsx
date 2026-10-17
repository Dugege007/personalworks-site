import {
  useEffect,
  useLayoutEffect,
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
import { TermText } from "../TermText";
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
  /** 施工图等大图关掉相邻预解码，避免多张原图同时进主线程。 */
  preloadNeighbors?: boolean;
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

const SWIPE_PX = 56;
const FLICK_SPEED = 0.45;
const DOUBLE_TAP_MS = 300;
const DOUBLE_TAP_PX = 36;
const DOUBLE_TAP_ZOOM = 2;
const PINCH_FIT = 1.02;

type Finger = { x: number; y: number };

type PinchGesture = {
  startDist: number;
  startScale: number;
  startPan: Pan;
  startMidX: number;
  startMidY: number;
};

function fingerSpan(a: Finger, b: Finger) {
  return {
    dist: Math.hypot(b.x - a.x, b.y - a.y),
    midX: (a.x + b.x) / 2,
    midY: (a.y + b.y) / 2,
  };
}

/** 双指缩放：以按下时的中点为锚，并跟上中点的移动。 */
function pinchScale(el: HTMLElement, pinch: PinchGesture, dist: number, midX: number, midY: number) {
  const scale = Math.min(ZOOM_MAX, Math.max(1, pinch.startScale * (dist / pinch.startDist)));
  const anchored = zoomPanAt(
    el,
    pinch.startPan,
    Math.max(pinch.startScale, 1),
    scale,
    pinch.startMidX,
    pinch.startMidY,
  );
  return {
    scale,
    pan: clampPan(
      {
        x: anchored.x + (midX - pinch.startMidX),
        y: anchored.y + (midY - pinch.startMidY),
      },
      el,
      scale,
    ),
  };
}

type Rgb = { r: number; g: number; b: number };

let inkCanvas: HTMLCanvasElement | null = null;
let inkKey = "";
let probeCanvas: HTMLCanvasElement | null = null;

function relativeLuminance(color: Rgb) {
  const channel = (value: number) => {
    const s = value / 255;
    return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(color.r) + 0.7152 * channel(color.g) + 0.0722 * channel(color.b);
}

function parseCssColor(color: string): (Rgb & { a: number }) | null {
  if (!color || color === "transparent") {
    return null;
  }
  const canvas = probeCanvas ?? document.createElement("canvas");
  probeCanvas = canvas;
  canvas.width = 1;
  canvas.height = 1;
  const ctx = canvas.getContext("2d", { willReadFrequently: true });
  if (!ctx) {
    return null;
  }
  ctx.clearRect(0, 0, 1, 1);
  ctx.fillStyle = "#0000";
  ctx.fillStyle = color;
  ctx.fillRect(0, 0, 1, 1);
  const data = ctx.getImageData(0, 0, 1, 1).data;
  if (data[3] < 128) {
    return null;
  }
  return { r: data[0], g: data[1], b: data[2], a: data[3] / 255 };
}

function readMediaPixel(media: HTMLImageElement | HTMLVideoElement, clientX: number, clientY: number): Rgb | null {
  const rect = media.getBoundingClientRect();
  if (rect.width < 1 || rect.height < 1) {
    return null;
  }
  const u = (clientX - rect.left) / rect.width;
  const v = (clientY - rect.top) / rect.height;
  if (u < 0 || u > 1 || v < 0 || v > 1) {
    return null;
  }
  const sourceWidth = media instanceof HTMLVideoElement ? media.videoWidth : media.naturalWidth;
  const sourceHeight = media instanceof HTMLVideoElement ? media.videoHeight : media.naturalHeight;
  if (sourceWidth < 1 || sourceHeight < 1) {
    return null;
  }
  try {
    const canvas = inkCanvas ?? document.createElement("canvas");
    inkCanvas = canvas;
    const key = media instanceof HTMLImageElement ? media.currentSrc : "";
    if (inkKey !== key || media instanceof HTMLVideoElement) {
      const scale = Math.min(1, 240 / Math.max(sourceWidth, sourceHeight));
      canvas.width = Math.max(1, Math.round(sourceWidth * scale));
      canvas.height = Math.max(1, Math.round(sourceHeight * scale));
      const ctx = canvas.getContext("2d", { willReadFrequently: true });
      if (!ctx) {
        return null;
      }
      ctx.drawImage(media, 0, 0, canvas.width, canvas.height);
      if (media instanceof HTMLImageElement) {
        inkKey = key;
      }
    }
    const ctx = canvas.getContext("2d", { willReadFrequently: true });
    if (!ctx) {
      return null;
    }
    const px = Math.min(canvas.width - 1, Math.max(0, Math.round(u * (canvas.width - 1))));
    const py = Math.min(canvas.height - 1, Math.max(0, Math.round(v * (canvas.height - 1))));
    const data = ctx.getImageData(px, py, 1, 1).data;
    return { r: data[0], g: data[1], b: data[2] };
  } catch {
    return null;
  }
}

/** 黑字对比度更高时返回 true。分界约在相对亮度 0.179。 */
function indexOnLight(indexEl: HTMLElement) {
  const rect = indexEl.getBoundingClientRect();
  if (rect.width < 1 || rect.height < 1) {
    return false;
  }
  const y = rect.top + rect.height / 2;
  const samples = [0.15, 0.35, 0.5, 0.65, 0.85].map((t) => rect.left + rect.width * t);
  const lums: number[] = [];
  for (const x of samples) {
    const stack = document.elementsFromPoint(x, y);
    let color: Rgb | null = null;
    for (const el of stack) {
      if (!(el instanceof HTMLElement) || el.classList.contains("lrb-lightbox-index")) {
        continue;
      }
      const media = el.closest(".lrb-lightbox-photo");
      if (media instanceof HTMLImageElement || media instanceof HTMLVideoElement) {
        color = readMediaPixel(media, x, y);
        if (color) {
          break;
        }
      }
      let node: HTMLElement | null = el;
      while (node) {
        const parsed = parseCssColor(getComputedStyle(node).backgroundColor);
        if (parsed && parsed.a >= 0.5) {
          color = parsed;
          break;
        }
        if (node.classList.contains("lrb-lightbox")) {
          break;
        }
        node = node.parentElement;
      }
      if (color) {
        break;
      }
    }
    if (color) {
      lums.push(relativeLuminance(color));
    }
  }
  if (!lums.length) {
    return false;
  }
  const average = lums.reduce((sum, value) => sum + value, 0) / lums.length;
  return average > 0.179;
}

/** 邻图靠近当前图的一边，距当前图为该侧页边距再加 30px，即落在屏幕外 30px。 */
const PEER_OUTSET = 30;

function frameSideMargins(root: HTMLElement, stage: HTMLElement) {
  const rootRect = root.getBoundingClientRect();
  const stageRect = stage.getBoundingClientRect();
  const sheet = stage.parentElement;
  let shift = 0;
  if (sheet) {
    const transform = getComputedStyle(sheet).transform;
    if (transform && transform !== "none") {
      shift = new DOMMatrix(transform).m41;
    }
  }
  return {
    marginLeft: stageRect.left - shift - rootRect.left,
    marginRight: rootRect.right - (stageRect.right - shift),
  };
}

function placeNeighborFrames(root: HTMLElement, sheet: HTMLElement) {
  const stage = sheet.querySelector(".lrb-lightbox-stage");
  if (!(stage instanceof HTMLElement)) {
    return;
  }
  const { marginLeft, marginRight } = frameSideMargins(root, stage);
  sheet.style.setProperty("--peer-prev-gap", `${Math.max(0, marginLeft) + PEER_OUTSET}px`);
  sheet.style.setProperty("--peer-next-gap", `${Math.max(0, marginRight) + PEER_OUTSET}px`);
}

/** 邻张中心移到当前图中心所需的平移。 */
function slideStride(root: HTMLElement, sheet: HTMLElement, dir: "next" | "prev") {
  const stage = sheet.querySelector(".lrb-lightbox-stage");
  const peer = sheet.querySelector(
    dir === "next" ? ".lrb-lightbox-peer.is-next" : ".lrb-lightbox-peer.is-prev",
  );
  if (!(stage instanceof HTMLElement) || !(peer instanceof HTMLElement)) {
    return 0;
  }
  const { marginLeft, marginRight } = frameSideMargins(root, stage);
  const gap = (dir === "next" ? marginRight : marginLeft) + PEER_OUTSET;
  if (stage.offsetWidth < 1 || peer.offsetWidth < 1) {
    return 0;
  }
  return stage.offsetWidth / 2 + Math.max(0, gap) + peer.offsetWidth / 2;
}

function LightboxPeer({ shot, side, captionOn }: { shot: LightboxShot; side: "prev" | "next"; captionOn: boolean }) {
  const src = assetUrl(shot.src);
  const poster = shot.poster ? assetUrl(shot.poster) : undefined;
  const lead = resolveLead(shot.description);
  return (
    <div className={`lrb-lightbox-peer is-${side}`} aria-hidden="true">
      <div className="lrb-lightbox-picture">
        <figure className="lrb-lightbox-frame">
          {shot.kind === "video" ? (
            <video className="lrb-lightbox-photo is-video" src={src} poster={poster} muted playsInline preload="metadata" />
          ) : (
            <img className="lrb-lightbox-photo" src={src} alt="" draggable={false} />
          )}
        </figure>
      </div>
      {captionOn ? (
        <div className={`lrb-lightbox-dock${shot.exif ? " has-exif" : ""}`}>
          <PhotoExifStrip exif={shot.exif} />
          <div className="lrb-lightbox-copy">
            {shot.label ? (
              <div className="lrb-lightbox-names">
                <p className="lrb-lightbox-shot">{shot.label}</p>
              </div>
            ) : null}
            {lead ? (
              <p className="lrb-lightbox-lead">
                <TermText text={lead} />
              </p>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function outerBoxHeight(el: HTMLElement) {
  const style = getComputedStyle(el);
  return el.offsetHeight + parseFloat(style.marginTop) + parseFloat(style.marginBottom);
}

/**
 * 按灯箱内边距和底部说明栏，把图收进剩余区域。
 * 说明栏变高时只继续缩小图，避免宽度一变、高度来回跳。
 */
function fitLightboxPhoto(root: HTMLElement, dock: HTMLElement | null) {
  const style = getComputedStyle(root);
  const padX = parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
  const padY = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
  const contentW = Math.max(0, root.clientWidth - padX);
  const contentH = Math.max(0, root.clientHeight - padY);
  if (contentW < 1 || contentH < 1) {
    return;
  }
  root.style.setProperty("--lightbox-photo-max-inline", `${Math.round(contentW)}px`);
  const captionOff = root.classList.contains("is-caption-off");
  let photoH = contentH;
  for (let pass = 0; pass < 4; pass += 1) {
    root.style.setProperty("--lightbox-photo-max-block", `${Math.round(photoH)}px`);
    const used = !captionOff && dock ? outerBoxHeight(dock) : 0;
    const next = Math.max(0, contentH - used);
    if (next >= photoH - 0.5) {
      break;
    }
    photoH = next;
  }
  root.style.setProperty("--lightbox-photo-max-block", `${Math.round(photoH)}px`);
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
 * 全站共用大图灯箱：图框按横竖屏边距收进视口，保持比例；
 * 说明栏显隐后按剩余高度重算图的最大尺寸。
 * 横屏左右翻页；单击进入 150% 缩放。竖屏双击在原图与 2 倍间切换，双指在原图到 300% 间缩放。
 * 竖屏未放大时拖拽换张：邻张先停在旁边，松手收到正中后再换页。
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
  preloadNeighbors = true,
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
    allowZoom: boolean;
  } | null>(null);
  const [zoomed, setZoomed] = useState(false);
  const [zoomScale, setZoomScale] = useState(ZOOM_MIN);
  const [pan, setPan] = useState<Pan>(ZERO_PAN);
  const [panning, setPanning] = useState(false);
  const [zoomMotion, setZoomMotion] = useState(false);
  const [frameBox, setFrameBox] = useState({ w: 0, h: 0 });
  const [captionOn, setCaptionOn] = useState(true);
  const [swipeX, setSwipeX] = useState(0);
  const [swipeBack, setSwipeBack] = useState(false);
  const dockRef = useRef<HTMLDivElement>(null);
  const portraitRef = useRef(false);
  const fitRef = useRef<() => void>(() => {});
  const syncIndexInkRef = useRef<() => void>(() => {});
  const indexRef = useRef<HTMLParagraphElement>(null);
  const swipeRafRef = useRef(0);
  const pointersRef = useRef(new Map<number, Finger>());
  const pinchRef = useRef<PinchGesture | null>(null);
  const sheetRef = useRef<HTMLDivElement>(null);
  const settleRef = useRef<"next" | "prev" | null>(null);
  const flickRef = useRef({ x: 0, lastX: 0, t: 0 });
  const tapRef = useRef<{ t: number; x: number; y: number } | null>(null);
  const current = images[index];

  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.documentElement.dataset.lightbox = "open";
    pausePageLenis();
    closeRef.current?.focus();
    return () => {
      cancelAnimationFrame(swipeRafRef.current);
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
    setSwipeX(0);
    setSwipeBack(false);
    cancelAnimationFrame(swipeRafRef.current);
    pointersRef.current.clear();
    pinchRef.current = null;
    tapRef.current = null;
    settleRef.current = null;
  }, [index]);

  useEffect(() => {
    const query = window.matchMedia("(orientation: portrait)");
    const apply = () => {
      portraitRef.current = query.matches;
      if (!query.matches) {
        setSwipeX(0);
        setSwipeBack(false);
      }
    };
    apply();
    query.addEventListener("change", apply);
    return () => query.removeEventListener("change", apply);
  }, []);

  fitRef.current = () => {
    const root = rootRef.current;
    if (!root) {
      return;
    }
    fitLightboxPhoto(root, dockRef.current);
  };

  syncIndexInkRef.current = () => {
    const el = indexRef.current;
    if (!el) {
      return;
    }
    el.classList.toggle("is-on-light", indexOnLight(el));
  };

  useLayoutEffect(() => {
    const root = rootRef.current;
    if (!root) {
      return;
    }
    const run = () => {
      fitRef.current();
      const root = rootRef.current;
      const sheet = sheetRef.current;
      if (root && sheet) {
        placeNeighborFrames(root, sheet);
      }
      syncIndexInkRef.current();
    };
    run();
    const observer = new ResizeObserver(run);
    observer.observe(root);
    return () => observer.disconnect();
  }, [captionOn, index, current?.src, summary, title]);

  useLayoutEffect(() => {
    const root = rootRef.current;
    const sheet = sheetRef.current;
    if (root && sheet) {
      placeNeighborFrames(root, sheet);
    }
    syncIndexInkRef.current();
  }, [captionOn, index, zoomed, zoomScale, pan.x, pan.y, swipeX, current?.src]);

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
        const floor = portraitRef.current ? 1 : ZOOM_MIN;
        const to = Math.min(ZOOM_MAX, Math.max(floor, from - wheelDelta(event) * ZOOM_PER_PX));
        if (portraitRef.current && to <= PINCH_FIT) {
          zoomedRef.current = false;
          zoomScaleRef.current = ZOOM_MIN;
          panRef.current = ZERO_PAN;
          setZoomed(false);
          setZoomScale(ZOOM_MIN);
          setPan(ZERO_PAN);
          return;
        }
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
    if (!preloadNeighbors || images.length < 2) {
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
  }, [images, index, preloadNeighbors]);

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

  const capturePointer = (el: HTMLDivElement, pointerId: number) => {
    try {
      if (!el.hasPointerCapture(pointerId)) {
        el.setPointerCapture(pointerId);
      }
    } catch {
      // 指针已结束时忽略
    }
  };

  const leaveZoom = () => {
    zoomedRef.current = false;
    zoomScaleRef.current = ZOOM_MIN;
    panRef.current = ZERO_PAN;
    wheelCarryRef.current = 0;
    setZoomed(false);
    setZoomScale(ZOOM_MIN);
    setPan(ZERO_PAN);
    setPanning(false);
  };

  const onStagePointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) {
      return;
    }
    const target = event.target;
    const photo = target instanceof Element ? target.closest(".lrb-lightbox-photo") : null;
    const onVideo = photo instanceof HTMLVideoElement;
    if (onVideo) {
      const rect = photo.getBoundingClientRect();
      if (event.clientY > rect.bottom - 48) {
        return;
      }
    }
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
    const allowZoom = photo instanceof HTMLImageElement;
    if (pinchRef.current) {
      capturePointer(event.currentTarget, event.pointerId);
      return;
    }
    if (portraitRef.current && !isVideo && pointersRef.current.size >= 2) {
      const points = [...pointersRef.current.values()].slice(0, 2);
      const span = fingerSpan(points[0], points[1]);
      dragRef.current = null;
      setSwipeX(0);
      setSwipeBack(false);
      setZoomMotion(false);
      cancelAnimationFrame(swipeRafRef.current);
      pinchRef.current = {
        startDist: Math.max(span.dist, 1),
        startScale: zoomedRef.current ? zoomScaleRef.current : 1,
        startPan: zoomedRef.current ? { ...panRef.current } : { ...ZERO_PAN },
        startMidX: span.midX,
        startMidY: span.midY,
      };
      for (const id of pointersRef.current.keys()) {
        capturePointer(event.currentTarget, id);
      }
      return;
    }
    if (allowZoom) {
      event.preventDefault();
    }
    setZoomMotion(false);
    setSwipeBack(false);
    settleRef.current = null;
    flickRef.current = { x: 0, lastX: event.clientX, t: performance.now() };
    cancelAnimationFrame(swipeRafRef.current);
    dragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      panX: panRef.current.x,
      panY: panRef.current.y,
      moved: false,
      allowZoom,
    };
    if (zoomedRef.current || (allowZoom && portraitRef.current)) {
      capturePointer(event.currentTarget, event.pointerId);
    }
  };

  const onStagePointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (pointersRef.current.has(event.pointerId)) {
      pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
    }
    const pinch = pinchRef.current;
    if (pinch && pointersRef.current.size >= 2) {
      const points = [...pointersRef.current.values()].slice(0, 2);
      const span = fingerSpan(points[0], points[1]);
      const el = photoRef.current;
      if (!el || span.dist < 1) {
        return;
      }
      const next = pinchScale(el, pinch, span.dist, span.midX, span.midY);
      zoomScaleRef.current = next.scale;
      panRef.current = next.pan;
      setZoomMotion(false);
      if (next.scale <= 1.001) {
        zoomedRef.current = false;
        setZoomed(false);
        setZoomScale(1);
        setPan(ZERO_PAN);
      } else {
        zoomedRef.current = true;
        setZoomed(true);
        setZoomScale(next.scale);
        setPan(next.pan);
      }
      return;
    }
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
    if (!zoomedRef.current) {
      if (portraitRef.current && drag.moved && Math.abs(dx) > Math.abs(dy)) {
        const now = performance.now();
        const flick = flickRef.current;
        const dt = now - flick.t;
        flickRef.current =
          dt > 0 && dt < 80
            ? { x: (event.clientX - flick.lastX) / dt, lastX: event.clientX, t: now }
            : { x: 0, lastX: event.clientX, t: now };
        setSwipeX(dx);
        capturePointer(event.currentTarget, event.pointerId);
      } else if (Math.abs(dy) > Math.abs(dx)) {
        setSwipeX(0);
      }
      return;
    }
    if (!drag.moved || !drag.allowZoom) {
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

  const endStagePointer = (event: ReactPointerEvent<HTMLDivElement>, commit: boolean) => {
    pointersRef.current.delete(event.pointerId);
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    if (pinchRef.current && pointersRef.current.size < 2) {
      const scale = zoomScaleRef.current;
      pinchRef.current = null;
      dragRef.current = null;
      setPanning(false);
      if (scale <= PINCH_FIT) {
        leaveZoom();
      }
      return;
    }
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    dragRef.current = null;
    setPanning(false);
    const dx = event.clientX - drag.startX;
    const dy = event.clientY - drag.startY;
    if (portraitRef.current && !zoomedRef.current && drag.moved) {
      const dir = dx < 0 ? "next" : "prev";
      const root = rootRef.current;
      const sheet = sheetRef.current;
      const travel = root && sheet ? slideStride(root, sheet, dir) : 0;
      const flung =
        Math.abs(flickRef.current.x) > FLICK_SPEED &&
        Math.abs(dx) > 28 &&
        Math.sign(flickRef.current.x) === Math.sign(dx);
      const far = travel > 0 ? Math.abs(dx) > travel * 0.28 : Math.abs(dx) >= SWIPE_PX;
      const switched = commit && images.length > 1 && Math.abs(dx) > Math.abs(dy) && (flung || far);
      if (switched) {
        cancelAnimationFrame(swipeRafRef.current);
        const target = dir === "next" ? -travel : travel;
        const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (reduce || travel < 1 || Math.abs(dx - target) < 2) {
          settleRef.current = null;
          setSwipeBack(false);
          setSwipeX(0);
          if (dir === "next") {
            onNext();
          } else {
            onPrev();
          }
          return;
        }
        settleRef.current = dir;
        setSwipeBack(true);
        swipeRafRef.current = requestAnimationFrame(() => setSwipeX(target));
        return;
      }
      if (Math.hypot(dx, dy) > DOUBLE_TAP_PX) {
        if (Math.abs(dx) > Math.abs(dy)) {
          setSwipeBack(true);
          cancelAnimationFrame(swipeRafRef.current);
          swipeRafRef.current = requestAnimationFrame(() => setSwipeX(0));
        } else {
          setSwipeX(0);
        }
        return;
      }
      setSwipeX(0);
    }
    const tapped = !drag.moved || (portraitRef.current && Math.hypot(dx, dy) <= DOUBLE_TAP_PX);
    if (portraitRef.current && drag.allowZoom && tapped && commit) {
      const now = performance.now();
      const last = tapRef.current;
      const x = event.clientX;
      const y = event.clientY;
      if (last && now - last.t <= DOUBLE_TAP_MS && Math.hypot(x - last.x, y - last.y) <= DOUBLE_TAP_PX) {
        tapRef.current = null;
        const photo = photoRef.current;
        if (photo && zoomedRef.current && zoomScaleRef.current > PINCH_FIT) {
          setZoomMotion(true);
          leaveZoom();
        } else if (photo) {
          const pan = zoomPanAt(photo, ZERO_PAN, 1, DOUBLE_TAP_ZOOM, x, y);
          zoomedRef.current = true;
          zoomScaleRef.current = DOUBLE_TAP_ZOOM;
          panRef.current = pan;
          wheelCarryRef.current = 0;
          setPan(pan);
          setZoomScale(DOUBLE_TAP_ZOOM);
          setZoomed(true);
          setZoomMotion(true);
          setFrameBox({ w: photo.offsetWidth, h: photo.offsetHeight });
        }
      } else {
        tapRef.current = { t: now, x, y };
      }
      return;
    }
    if (!commit || drag.moved || !drag.allowZoom) {
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
      syncIndexInkRef.current();
    }
  };

  const onSheetTransitionEnd = (event: TransitionEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget || event.propertyName !== "transform") {
      return;
    }
    const dir = settleRef.current;
    if (dir) {
      settleRef.current = null;
      setSwipeBack(false);
      setSwipeX(0);
      if (dir === "next") {
        onNext();
      } else {
        onPrev();
      }
      return;
    }
    setSwipeBack(false);
    syncIndexInkRef.current();
  };

  const shotIndex = `${index + 1}/${images.length}`;
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
  const mediaStyle =
    zoomed && !isVideo ? { transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoomScale})` } : undefined;
  const sheetClass = ["lrb-lightbox-sheet", swipeBack ? "is-swipe-back" : ""].filter(Boolean).join(" ");
  const sheetStyle = swipeX !== 0 || swipeBack ? { transform: `translate(${swipeX}px, 0)` } : undefined;
  const prevShot = images.length > 1 && !zoomed ? images[(index - 1 + images.length) % images.length] : null;
  const nextShot = images.length > 1 && !zoomed ? images[(index + 1) % images.length] : null;

  return createPortal(
    <div
      ref={rootRef}
      className={captionOn ? "lrb-lightbox" : "lrb-lightbox is-caption-off"}
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <button className="lrb-lightbox-scrim" type="button" aria-label="关闭预览" onClick={onClose} />
      <div className="lrb-lightbox-board" onClick={stop}>
        <div
          ref={sheetRef}
          className={sheetClass}
          style={sheetStyle}
          onPointerDown={onStagePointerDown}
          onPointerMove={onStagePointerMove}
          onPointerUp={(event) => endStagePointer(event, true)}
          onPointerCancel={(event) => endStagePointer(event, false)}
          onTransitionEnd={onSheetTransitionEnd}
        >
          {prevShot ? <LightboxPeer shot={prevShot} side="prev" captionOn={captionOn} /> : null}
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
                  onLoadedMetadata={() => {
                    fitRef.current();
                    const root = rootRef.current;
                    const sheet = sheetRef.current;
                    if (root && sheet) {
                      placeNeighborFrames(root, sheet);
                    }
                    syncIndexInkRef.current();
                  }}
                />
              ) : (
                <img
                  ref={photoRef}
                  className={photoClass}
                  src={photoSrc}
                  alt={current.alt}
                  draggable={false}
                  style={mediaStyle}
                  onLoad={() => {
                    fitRef.current();
                    const root = rootRef.current;
                    const sheet = sheetRef.current;
                    if (root && sheet) {
                      placeNeighborFrames(root, sheet);
                    }
                    syncIndexInkRef.current();
                  }}
                  onDragStart={(event) => event.preventDefault()}
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
          {captionOn ? (
          <div
            ref={dockRef}
            className={`lrb-lightbox-dock${current.exif ? " has-exif" : ""}`}
          >
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
              {resolveLead(summary) ? (
                <p className="lrb-lightbox-lead">
                  <TermText text={resolveLead(summary)!} />
                </p>
              ) : null}
            </div>
          </div>
          ) : null}
          </div>
          {nextShot ? <LightboxPeer shot={nextShot} side="next" captionOn={captionOn} /> : null}
        </div>
      </div>
      <p ref={indexRef} className="lrb-lightbox-index" aria-live="polite">
        {shotIndex}
      </p>
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
      <button
        className="lrb-lightbox-orb is-info"
        type="button"
        aria-expanded={captionOn}
        aria-label={captionOn ? "隐藏信息" : "显示信息"}
        onClick={() => setCaptionOn((open) => !open)}
      >
        <span className="lrb-lightbox-orb-mark" aria-hidden="true">
          <NavMark name="info" />
        </span>
      </button>
    </div>,
    document.body,
  );
}
