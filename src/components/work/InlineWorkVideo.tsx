import { useEffect, useRef } from "react";
import { assetUrl } from "../../lib/assets";

const LOOP_GAP_MS = 1000;

type InlineWorkVideoProps = {
  src: string;
  poster?: string;
  label: string;
  title: string;
  paused: boolean;
  syncTime?: number;
  className?: string;
  onOpen: () => void;
};

/**
 * 页内视频框：整框进入视口后自动播，离开即停；播完停 1 秒再从头循环。
 * 无条件静音，不提供音量控件；静音与音量只在灯箱 / 全屏里改。
 */
export function InlineWorkVideo({ src, poster, label, title, paused, syncTime, className, onOpen }: InlineWorkVideoProps) {
  const frameRef = useRef<HTMLButtonElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const visibleRef = useRef(false);
  const pausedRef = useRef(paused);
  const timerRef = useRef(0);
  pausedRef.current = paused;

  useEffect(() => {
    const video = videoRef.current;
    if (!video) {
      return;
    }
    const lockMuted = () => {
      if (!video.muted) {
        video.muted = true;
      }
    };
    lockMuted();
    video.addEventListener("volumechange", lockMuted);
    video.addEventListener("play", lockMuted);
    return () => {
      video.removeEventListener("volumechange", lockMuted);
      video.removeEventListener("play", lockMuted);
    };
  }, [src]);

  useEffect(() => {
    const video = videoRef.current;
    if (!video || syncTime == null || !Number.isFinite(syncTime)) {
      return;
    }
    if (Math.abs(video.currentTime - syncTime) > 0.05) {
      video.currentTime = syncTime;
    }
  }, [syncTime]);

  useEffect(() => {
    const stopLoopTimer = () => {
      if (timerRef.current) {
        window.clearTimeout(timerRef.current);
        timerRef.current = 0;
      }
    };

    const syncPlayback = () => {
      const video = videoRef.current;
      if (!video) {
        return;
      }
      if (!visibleRef.current || pausedRef.current) {
        stopLoopTimer();
        video.pause();
        return;
      }
      video.muted = true;
      void video.play().catch(() => undefined);
    };

    const frame = frameRef.current;
    if (!frame) {
      return;
    }
    const observer = new IntersectionObserver(
      ([entry]) => {
        visibleRef.current = Boolean(entry) && entry.intersectionRatio >= 1;
        syncPlayback();
      },
      { threshold: 1 },
    );
    observer.observe(frame);
    syncPlayback();
    return () => {
      stopLoopTimer();
      observer.disconnect();
    };
  }, [paused, src]);

  const onEnded = () => {
    if (timerRef.current) {
      window.clearTimeout(timerRef.current);
    }
    timerRef.current = window.setTimeout(() => {
      timerRef.current = 0;
      const video = videoRef.current;
      if (!video) {
        return;
      }
      video.currentTime = 0;
      if (visibleRef.current && !pausedRef.current) {
        video.muted = true;
        void video.play().catch(() => undefined);
      }
    }, LOOP_GAP_MS);
  };

  return (
    <button
      ref={frameRef}
      className={className ?? "project-gallery-shot"}
      type="button"
      onClick={onOpen}
      aria-label={`查看${title} ${label}`}
    >
      <video
        ref={videoRef}
        src={assetUrl(src)}
        poster={poster ? assetUrl(poster) : undefined}
        muted
        playsInline
        preload="metadata"
        onEnded={onEnded}
      />
      <span className="media-frame-kind" aria-hidden="true">
        <svg
          className="media-frame-kind-mark"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="m16 13 5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5" />
          <rect x="2" y="6" width="14" height="12" rx="2" />
        </svg>
        视频
      </span>
    </button>
  );
}
