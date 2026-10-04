import { useLayoutEffect, useRef, useState, type ReactNode } from "react";
import type { PhotoExif } from "../../content/photoExif";

type MarkName = "camera" | "lens" | "focal" | "aperture" | "shutter" | "iso";

const MARK_LABEL: Record<MarkName, string> = {
  camera: "相机",
  lens: "镜头",
  focal: "焦距",
  aperture: "光圈",
  shutter: "快门",
  iso: "ISO",
};

type MarkDef = { viewBox: string; inner: ReactNode };

const MARK_DEF: Record<MarkName, MarkDef> = {
  camera: {
    viewBox: "0 0 512 512",
    inner: (
      <>
        <circle cx="256" cy="272" r="64" />
        <path d="M432 144h-59c-3 0-6.72-1.94-9.62-5l-25.94-40.94a15.5 15.5 0 0 0-1.37-1.85C327.11 85.76 315 80 302 80h-92c-13 0-25.11 5.76-34.07 16.21a15.5 15.5 0 0 0-1.37 1.85l-25.94 41c-2.22 2.42-5.34 5-8.62 5v-8a16 16 0 0 0-16-16h-24a16 16 0 0 0-16 16v8h-4a48.05 48.05 0 0 0-48 48V384a48.05 48.05 0 0 0 48 48h352a48.05 48.05 0 0 0 48-48V192a48.05 48.05 0 0 0-48-48M256 368a96 96 0 1 1 96-96 96.11 96.11 0 0 1-96 96" />
      </>
    ),
  },
  lens: {
    viewBox: "0 0 24 24",
    inner: (
      <>
        <circle cx="3.55" cy="12" r="2.35" />
        <rect x="4.7" y="9.55" width="2.6" height="4.9" rx="0.35" />
        <rect x="7.1" y="8.2" width="3.35" height="7.6" rx="0.4" />
        <rect x="10.25" y="7.45" width="1.2" height="9.1" rx="0.25" />
        <rect x="11.3" y="8.05" width="3.15" height="7.9" rx="0.4" />
        <rect x="14.2" y="7.25" width="3.15" height="9.5" rx="0.45" />
        <circle cx="18.65" cy="12" r="5.1" />
      </>
    ),
  },
  focal: {
    viewBox: "0 0 24 24",
    inner: (
      <path d="M3 3h7v2H5v5H3ZM14 3h7v7h-2V5h-5ZM3 14h2v5h5v2H3ZM19 14h2v7h-7v-2h5ZM10.75 8h2.5v2.75H16v2.5h-2.75V16h-2.5v-2.75H8v-2.5h2.75Z" />
    ),
  },
  aperture: {
    viewBox: "0 0 24 24",
    inner: (
      <path d="M21.96 12.87A10 10 0 0 1 17.74 20.19L11.86 14.7 13.97 13.84ZM16.23 21.06A10 10 0 0 1 7.77 21.06L9.59 13.23 11.39 14.63ZM6.26 20.19A10 10 0 0 1 2.04 12.87L9.74 10.53 9.42 12.79ZM2.04 11.13A10 10 0 0 1 6.26 3.81L12.14 9.3 10.03 10.16ZM7.77 2.94A10 10 0 0 1 16.23 2.94L14.41 10.77 12.61 9.37ZM17.74 3.81A10 10 0 0 1 21.96 11.13L14.26 13.47 14.58 11.21Z" />
    ),
  },
  shutter: {
    viewBox: "0 0 512 512",
    inner: (
      <path d="M415.7 427.13c-8.74-76.89-43.83-108.76-69.46-132C328.52 279 320 270.61 320 256c0-14.41 8.49-22.64 26.16-38.44 25.93-23.17 61.44-54.91 69.56-132.84a47 47 0 0 0-12-36.26A50.3 50.3 0 0 0 366.39 32H145.61a50.34 50.34 0 0 0-37.39 16.46 47.05 47.05 0 0 0-11.94 36.26c8.09 77.68 43.47 109.19 69.3 132.19C183.42 232.8 192 241.09 192 256c0 15.1-8.6 23.56-26.5 39.75-25.5 23.1-60.5 54.73-69.2 131.38a46.6 46.6 0 0 0 11.7 36.2A50.44 50.44 0 0 0 145.61 480h220.78A50.44 50.44 0 0 0 404 463.33a46.6 46.6 0 0 0 11.7-36.2M343.3 432H169.13c-15.6 0-20-18-9.06-29.16C186.55 376 240 356.78 240 326V224c0-19.85-38-35-61.51-67.2-3.88-5.31-3.49-12.8 6.37-12.8h142.73c8.41 0 10.22 7.43 6.4 12.75C310.82 189 272 204.05 272 224v102c0 30.53 55.71 47 80.4 76.87 9.95 12.04 6.47 29.13-9.1 29.13" />
    ),
  },
  iso: {
    viewBox: "0 0 512 512",
    inner: (
      <path d="M256 32A224 224 0 0 0 97.61 414.39 224 224 0 1 0 414.39 97.61 222.53 222.53 0 0 0 256 32M64 256c0-105.87 86.13-192 192-192v384c-105.87 0-192-86.13-192-192" />
    ),
  },
};

function ExifMark({ name }: { name: MarkName }) {
  const mark = MARK_DEF[name];
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox={mark.viewBox} fill="currentColor" aria-hidden="true">
      {mark.inner}
    </svg>
  );
}

function ExifChip({ name, value }: { name: MarkName; value: string }) {
  return (
    <span
      className="lrb-lightbox-exif-chip"
      data-exif={name}
      aria-label={name === "iso" ? value : `${MARK_LABEL[name]} ${value}`}
    >
      <span className="lrb-lightbox-exif-mark">
        <ExifMark name={name} />
      </span>
      <span className="lrb-lightbox-exif-value">{value}</span>
    </span>
  );
}

function ExifGearRow({ items }: { items: { name: "camera" | "lens"; value: string }[] }) {
  const rowRef = useRef<HTMLDivElement>(null);
  const [cameraWide, setCameraWide] = useState(false);

  useLayoutEffect(() => {
    const row = rowRef.current;
    if (!row || !items.some((item) => item.name === "camera") || !items.some((item) => item.name === "lens")) {
      setCameraWide(false);
      return;
    }

    const measure = () => {
      const camera = row.querySelector<HTMLElement>('[data-exif="camera"]');
      const value = camera?.querySelector<HTMLElement>(".lrb-lightbox-exif-value");
      const mark = camera?.querySelector<HTMLElement>(".lrb-lightbox-exif-mark");
      if (!camera || !value) {
        setCameraWide(false);
        return;
      }

      const styles = getComputedStyle(row);
      const gap = Number.parseFloat(styles.columnGap) || 0;
      const column = (row.clientWidth - gap * 3) / 4;
      const needed = (mark?.getBoundingClientRect().width ?? 0) + 7 + value.scrollWidth;
      setCameraWide(needed > column + 1);
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(row);
    return () => observer.disconnect();
  }, [items.map((item) => `${item.name}:${item.value}`).join("|")]);

  return (
    <div className={`lrb-lightbox-exif-gear${cameraWide ? " is-camera-wide" : ""}`} ref={rowRef}>
      {items.map((item) => (
        <ExifChip key={item.name} name={item.name} value={item.value} />
      ))}
    </div>
  );
}

export function PhotoExifStrip({ exif }: { exif?: PhotoExif }) {
  if (!exif) return null;

  const gear = (
    [
      exif.camera ? { name: "camera" as const, value: exif.camera } : null,
      exif.lens ? { name: "lens" as const, value: exif.lens } : null,
    ] as const
  ).filter((item): item is { name: "camera" | "lens"; value: string } => item !== null);

  const exposure = (
    [
      exif.focalLength ? { name: "focal" as const, value: exif.focalLength } : null,
      exif.aperture ? { name: "aperture" as const, value: exif.aperture } : null,
      exif.shutter ? { name: "shutter" as const, value: exif.shutter } : null,
      exif.iso ? { name: "iso" as const, value: `ISO ${exif.iso}` } : null,
    ] as const
  ).filter((item): item is { name: "focal" | "aperture" | "shutter" | "iso"; value: string } => item !== null);

  if (gear.length === 0 && exposure.length === 0) return null;

  return (
    <div className="lrb-lightbox-exif">
      {gear.length > 0 ? <ExifGearRow items={gear} /> : null}
      {exposure.length > 0 ? (
        <div className="lrb-lightbox-exif-exposure">
          {exposure.map((item) => (
            <ExifChip key={item.name} name={item.name} value={item.value} />
          ))}
        </div>
      ) : null}
    </div>
  );
}
