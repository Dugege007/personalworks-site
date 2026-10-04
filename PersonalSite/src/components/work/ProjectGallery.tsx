import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { planAlbumPreview, type ProjectAlbumShot } from "../../content/projectAlbum";
import { readPhotoExif } from "../../content/photoExif";
import { assetUrl } from "../../lib/assets";
import { TermText } from "../TermText";
import { resolveLead, resolveMediaDescription } from "../../content/copyDisplay";
import { ImageLightbox, type LightboxShot } from "./ImageLightbox";
import { InlineWorkVideo } from "./InlineWorkVideo";
import "../../styles/photo-masonry.css";
import "../../styles/placeholder.css";
import "../../styles/project-gallery.css";

export type ProjectGalleryShot = ProjectAlbumShot;

export type ProjectGalleryItem = {
  id: string;
  title: string;
  date?: string;
  place?: string;
  summary?: string;
  href?: string;
  images: ProjectGalleryShot[];
  /** 灯箱序列；缺省时用 images。列表预览与灯箱均按 media 原序，含视频。 */
  shots?: ProjectGalleryShot[];
};

type ProjectGalleryProps = {
  projects: ProjectGalleryItem[];
  variant: "twin" | "photo" | "game";
  /** 详情页顶栏已有标题时关掉，类目列表仍显示。默认开。 */
  showHead?: boolean;
  /** 列表预览最多铺几格；超过则末格叠层并链到项目页。 */
  previewLimit?: number;
};

type OpenImage = {
  projectId: string;
  index: number;
};

function albumSequence(project: ProjectGalleryItem): ProjectGalleryShot[] {
  return project.shots ?? project.images;
}

function shotIndexInSequence(sequence: ProjectGalleryShot[], src: string): number {
  const index = sequence.findIndex((item) => item.src === src);
  return index >= 0 ? index : 0;
}

function AlbumFace({ image, bootFirst = false }: { image: ProjectGalleryShot; bootFirst?: boolean }) {
  const bootAttr = bootFirst ? { "data-boot-first": "" } : {};
  if (image.kind === "video" && !image.poster) {
    return <video src={assetUrl(image.src)} muted playsInline preload="metadata" {...bootAttr} />;
  }
  const src = image.kind === "video" && image.poster ? image.poster : image.src;
  return <img src={assetUrl(src)} alt="" loading={bootFirst ? "eager" : "lazy"} {...bootAttr} />;
}

/**
 * 数字孪生、摄影与游戏共用项目图集；各类型只切换版式，不复制内容池。
 */
export function ProjectGallery({
  projects,
  variant,
  showHead = true,
  previewLimit,
}: ProjectGalleryProps) {
  const galleryRef = useRef<HTMLDivElement>(null);
  const videoProgressRef = useRef<Map<string, number>>(new Map());
  const [videoClock, setVideoClock] = useState<Record<string, number>>({});
  const [videoMuted, setVideoMuted] = useState(true);
  const [open, setOpen] = useState<OpenImage | null>(null);
  const activeProject = projects.find((project) => project.id === open?.projectId);
  const activeSequence = activeProject ? albumSequence(activeProject) : [];
  const activeImages = useMemo<LightboxShot[]>(
    () =>
      activeSequence.map((image) => ({
        src: image.src,
        label: image.label,
        kind: image.kind ?? "image",
        poster: image.poster,
        description: image.description,
        alt: `${activeProject?.title ?? ""} ${image.label}`,
        exif: variant === "photo" && image.kind !== "video" ? readPhotoExif(image.src) : undefined,
      })),
    [activeProject?.title, activeSequence, variant],
  );

  const close = useCallback(() => setOpen(null), []);
  const onVideoTime = useCallback((src: string, time: number) => {
    videoProgressRef.current.set(src, time);
    setVideoClock((current) => (current[src] === time ? current : { ...current, [src]: time }));
  }, []);
  const openShot = useCallback((projectId: string, index: number, resetVideo = false) => {
    const project = projects.find((item) => item.id === projectId);
    const shot = project ? albumSequence(project)[index] : undefined;
    if (resetVideo && shot?.kind === "video") {
      videoProgressRef.current.set(shot.src, 0);
      setVideoClock((current) => (current[shot.src] === 0 ? current : { ...current, [shot.src]: 0 }));
    }
    setOpen({ projectId, index });
  }, [projects]);
  const openBySrc = useCallback(
    (projectId: string, src: string, resetVideo = false) => {
      const project = projects.find((item) => item.id === projectId);
      if (!project) {
        return;
      }
      openShot(projectId, shotIndexInSequence(albumSequence(project), src), resetVideo);
    },
    [openShot, projects],
  );
  const step = useCallback(
    (delta: number) => {
      setOpen((current) => {
        if (!current) {
          return null;
        }
        const project = projects.find((item) => item.id === current.projectId);
        const count = project ? albumSequence(project).length : 0;
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
    <div
      className={`project-gallery is-${variant}${previewLimit != null ? " is-preview" : ""}`}
      ref={galleryRef}
    >
      {projects.map((project, projectIndex) => {
        const isTwinPreview = variant === "twin" && previewLimit != null;
        const preview = isTwinPreview
          ? planAlbumPreview(project.images.length, previewLimit)
          : { visibleCount: project.images.length, overflow: false };
        const tiles = project.images.slice(0, preview.visibleCount);
        const mainTile = tiles[0];
        const sideTiles = isTwinPreview ? tiles.slice(1) : [];
        const useMasonry = variant === "photo";
        const useTwinFlow = variant === "twin" && !isTwinPreview;
        const renderShot = (image: ProjectGalleryShot, index: number) => {
          const isStack = preview.overflow && index === tiles.length - 1 && Boolean(project.href);
          const bootFirst = projectIndex === 0 && index === 0;
          const bootAttr = bootFirst ? { "data-boot-first": "" } : {};
          const shotClass = `project-gallery-shot${isTwinPreview && index === 0 ? " is-main" : ""}${isStack ? " is-stack" : ""}`;
          if (isStack && project.href) {
            return (
              <Link
                className={shotClass}
                to={project.href}
                key={`${image.src}-stack`}
                aria-label={`进入${project.title}项目页`}
              >
                <span className="project-gallery-stack-leaf" aria-hidden="true" />
                <span className="project-gallery-stack-leaf" aria-hidden="true" />
                <AlbumFace image={image} bootFirst={bootFirst} />
              </Link>
            );
          }
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
                bootFirst={bootFirst}
                onOpen={() => openBySrc(project.id, image.src, true)}
              />
            );
          }
          return (
            <button
              className={shotClass}
              type="button"
              key={image.src}
              onClick={() => openBySrc(project.id, image.src)}
              aria-label={`查看${project.title} ${image.label}`}
            >
              <img src={assetUrl(image.src)} alt="" loading={bootFirst ? "eager" : "lazy"} {...bootAttr} />
            </button>
          );
        };
        return (
        <article className={`project-gallery-item${showHead ? "" : " is-bare"}`} key={project.id}>
          {showHead ? (
            <header className="project-gallery-head">
              <div>
                <h2>
                  {project.href ? <Link to={project.href}>{project.title}</Link> : project.title}
                </h2>
                {resolveLead(project.summary) ? (
                  <p>
                    <TermText text={resolveLead(project.summary)!} />
                  </p>
                ) : null}
              </div>
              {project.date || project.place ? (
                <small>
                  {[project.date, project.place].filter(Boolean).join(" / ")}
                </small>
              ) : null}
            </header>
          ) : null}
          <div
            className={
              useMasonry ? "photo-masonry" : useTwinFlow ? "project-gallery-flow" : "project-gallery-images"
            }
            data-count={useMasonry || useTwinFlow ? tiles.length : undefined}
          >
            {isTwinPreview && mainTile && sideTiles.length > 0 ? (
              <>
                {renderShot(mainTile, 0)}
                <div className="project-gallery-side">
                  {Array.from({ length: 3 }, (_, column) => (
                    <div className="project-gallery-col" key={column}>
                      {sideTiles.map((image, sideIndex) =>
                        sideIndex % 3 === column ? (
                          <div
                            className="project-gallery-pack"
                            data-side-index={sideIndex}
                            key={image.src}
                          >
                            {renderShot(image, sideIndex + 1)}
                          </div>
                        ) : null,
                      )}
                    </div>
                  ))}
                </div>
              </>
            ) : (
              tiles.map((image, index) => renderShot(image, index))
            )}
          </div>
        </article>
        );
      })}
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
