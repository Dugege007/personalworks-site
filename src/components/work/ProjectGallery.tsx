import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { readPhotoExif } from "../../content/photoExif";
import { assetUrl } from "../../lib/assets";
import { resolveLead, resolveMediaDescription } from "../../content/copyDisplay";
import { ImageLightbox, type LightboxShot } from "./ImageLightbox";
import { InlineWorkVideo } from "./InlineWorkVideo";
import "../../styles/photo-masonry.css";
import "../../styles/project-gallery.css";

export type ProjectGalleryShot = {
  src: string;
  label: string;
  kind?: "image" | "video";
  poster?: string;
  description?: string;
};

export type ProjectGalleryItem = {
  id: string;
  title: string;
  date?: string;
  place?: string;
  summary?: string;
  href?: string;
  images: ProjectGalleryShot[];
};

type ProjectGalleryProps = {
  projects: ProjectGalleryItem[];
  variant: "twin" | "photo" | "game";
  /** 详情页顶栏已有标题时关掉，类目列表仍显示。默认开。 */
  showHead?: boolean;
};

type OpenImage = {
  projectId: string;
  index: number;
};

/**
 * 数字孪生、摄影与游戏共用项目图集；各类型只切换版式，不复制内容池。
 */
export function ProjectGallery({ projects, variant, showHead = true }: ProjectGalleryProps) {
  const galleryRef = useRef<HTMLDivElement>(null);
  const videoProgressRef = useRef<Map<string, number>>(new Map());
  const [videoClock, setVideoClock] = useState<Record<string, number>>({});
  const [videoMuted, setVideoMuted] = useState(true);
  const [open, setOpen] = useState<OpenImage | null>(null);
  const activeProject = projects.find((project) => project.id === open?.projectId);
  const activeImages = useMemo<LightboxShot[]>(
    () =>
      activeProject?.images.map((image) => ({
        src: image.src,
        label: image.label,
        kind: image.kind ?? "image",
        poster: image.poster,
        description: image.description,
        alt: `${activeProject.title} ${image.label}`,
        exif: variant === "photo" && image.kind !== "video" ? readPhotoExif(image.src) : undefined,
      })) ?? [],
    [activeProject, variant],
  );

  const close = useCallback(() => setOpen(null), []);
  const onVideoTime = useCallback((src: string, time: number) => {
    videoProgressRef.current.set(src, time);
    setVideoClock((current) => (current[src] === time ? current : { ...current, [src]: time }));
  }, []);
  const openShot = useCallback((projectId: string, index: number, resetVideo = false) => {
    const project = projects.find((item) => item.id === projectId);
    const shot = project?.images[index];
    if (resetVideo && shot?.kind === "video") {
      videoProgressRef.current.set(shot.src, 0);
      setVideoClock((current) => (current[shot.src] === 0 ? current : { ...current, [shot.src]: 0 }));
    }
    setOpen({ projectId, index });
  }, [projects]);
  const step = useCallback(
    (delta: number) => {
      setOpen((current) => {
        if (!current) {
          return null;
        }
        const project = projects.find((item) => item.id === current.projectId);
        const count = project?.images.length ?? 0;
        if (count === 0) {
          return current;
        }
        return { ...current, index: (current.index + delta + count) % count };
      });
    },
    [projects],
  );

  useEffect(() => {
    const gallery = galleryRef.current;
    if (!gallery) {
      return;
    }
    const items = [...gallery.querySelectorAll<HTMLElement>(".project-gallery-item")];
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      items.forEach((item) => item.classList.add("is-visible"));
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) {
            return;
          }
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        });
      },
      { rootMargin: "0px 0px -8% 0px", threshold: 0.12 },
    );
    items.forEach((item) => observer.observe(item));
    return () => observer.disconnect();
  }, [projects, variant]);

  return (
    <div className={`project-gallery is-${variant}`} ref={galleryRef}>
      {projects.map((project) => (
        <article className={`project-gallery-item${showHead ? "" : " is-bare"}`} key={project.id}>
          {showHead ? (
            <header className="project-gallery-head">
              <div>
                <h2>
                  {project.href ? <Link to={project.href}>{project.title}</Link> : project.title}
                </h2>
                {resolveLead(project.summary) ? <p>{resolveLead(project.summary)}</p> : null}
              </div>
              {project.date || project.place ? (
                <small>
                  {[project.date, project.place].filter(Boolean).join(" / ")}
                </small>
              ) : null}
            </header>
          ) : null}
          <div
            className={variant === "photo" ? "photo-masonry" : "project-gallery-images"}
            data-count={variant === "photo" ? project.images.length : undefined}
          >
            {project.images.map((image, index) => {
              const shotClass = `project-gallery-shot${variant === "twin" && index === 0 ? " is-main" : ""}`;
              if (image.kind === "video") {
                return (
                  <InlineWorkVideo
                    key={image.src}
                    className={shotClass}
                    src={image.src}
                    poster={image.poster}
                    label={image.label}
                    title={project.title}
                    paused={open !== null}
                    syncTime={videoClock[image.src]}
                    onOpen={() => openShot(project.id, index, true)}
                  />
                );
              }
              return (
                <button
                  className={shotClass}
                  type="button"
                  key={image.src}
                  onClick={() => openShot(project.id, index)}
                  aria-label={`查看${project.title} ${image.label}`}
                >
                  <img src={assetUrl(image.src)} alt="" loading="lazy" />
                </button>
              );
            })}
          </div>
        </article>
      ))}
      {open && activeProject && activeImages.length > 0 ? (
        <ImageLightbox
          images={activeImages}
          index={open.index}
          title={activeProject.title}
          summary={resolveMediaDescription(
            { description: activeImages[open.index]?.description },
            activeProject.summary,
          )}
          videoProgressRef={videoProgressRef}
          onVideoTime={onVideoTime}
          videoMuted={videoMuted}
          onVideoMutedChange={setVideoMuted}
          onClose={close}
          onPrev={() => step(-1)}
          onNext={() => step(1)}
        />
      ) : null}
    </div>
  );
}
