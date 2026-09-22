import { useCallback, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { FilterRow } from "../components/work/FilterRow";
import { ImageLightbox, type LightboxShot } from "../components/work/ImageLightbox";
import { resolveMediaDescription, resolveMediaDisplayName } from "../content/copyDisplay";
import { lexicon } from "../content/lexicon";
import { readPhotoExif } from "../content/photoExif";
import { categories } from "../content/site";
import {
  collectPhotoFacets,
  filterPhotoFrames,
  listPublishedPhotoFrames,
  listPublishedPhotoWorks,
  sortPhotoFrames,
  type PhotoCatalogQuery,
} from "../content/works";
import { hrefForKind, hrefForWork } from "../ia/href";
import { iaOfSkin } from "../ia";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/develop-work.css";
import "../styles/placeholder.css";
import "../styles/photo-masonry.css";
import "../styles/project-gallery.css";

const photoRoot = `/${lexicon.photography.key}`;
const catalogPath = `${photoRoot}/${lexicon.photoCatalog.key}`;

const photoTypeEntries = [lexicon.landscapePhoto, lexicon.humanistPhoto, lexicon.portraitPhoto];

type FilterKey = "channel" | "year" | "place" | "tag";

/**
 * 读取可重复查询参数。
 */
function readList(params: URLSearchParams, key: FilterKey): string[] {
  return params.getAll(key);
}

/**
 * 切换某一维的取值；已选则移除，未选则追加。
 */
function toggleValue(params: URLSearchParams, key: FilterKey, value: string): URLSearchParams {
  const next = new URLSearchParams(params);
  const current = next.getAll(key);
  next.delete(key);
  const remaining = current.includes(value) ? current.filter((item) => item !== value) : [...current, value];
  for (const item of remaining) {
    next.append(key, item);
  }
  return next;
}

/**
 * 解析总览查询；缺省时间为最新优先。
 */
function parseQuery(params: URLSearchParams): PhotoCatalogQuery {
  const sort = params.get("sort") === "asc" ? "asc" : "desc";
  return {
    channels: readList(params, "channel"),
    years: readList(params, "year"),
    places: readList(params, "place"),
    tags: readList(params, "tag"),
    sort,
  };
}

/**
 * 摄影总览：无卡砌体 + 四维筛选；点一张打开当前筛选序列灯箱。
 */
export function PhotoCatalogPage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const category = categories.find((item) => item.id === lexicon.photography.key);
  const [params, setParams] = useSearchParams();
  const filterSignature = params.toString();
  const query = useMemo(() => parseQuery(new URLSearchParams(filterSignature)), [filterSignature]);
  const catalogFrom = filterSignature ? `${catalogPath}?${filterSignature}` : catalogPath;
  const publishedWorks = useMemo(() => listPublishedPhotoWorks(), []);
  const published = useMemo(() => listPublishedPhotoFrames(), []);
  const facets = useMemo(() => collectPhotoFacets(publishedWorks), [publishedWorks]);
  const visible = useMemo(
    () => sortPhotoFrames(filterPhotoFrames(published, query), query.sort),
    [published, query],
  );
  const hasFilter =
    query.channels.length > 0 || query.years.length > 0 || query.places.length > 0 || query.tags.length > 0;
  //TODO: 灯箱序号写入查询或哈希
  const [open, setOpen] = useState<{ index: number; filter: string } | null>(null);
  const openIndex = open && open.filter === filterSignature ? open.index : null;
  const current = openIndex != null ? visible[openIndex] : undefined;
  const lightboxImages = useMemo<LightboxShot[]>(
    () =>
      visible.map((frame) => {
        const label = resolveMediaDisplayName(frame.media);
        return {
          src: frame.src,
          label,
          alt: `${frame.work.title} ${label}`,
          description: frame.media.description,
          exif: readPhotoExif(frame.src),
        };
      }),
    [visible],
  );

  const typeCaptions = Object.fromEntries(photoTypeEntries.map((item) => [item.key, item.zh]));

  /**
   * 写入某一维的开关结果。
   */
  function handleToggle(key: FilterKey, value: string) {
    setParams(toggleValue(params, key, value), { replace: true });
  }

  /**
   * 在从前到后与从后到前之间切换。
   */
  function handleSortToggle() {
    const next = new URLSearchParams(params);
    next.set("sort", query.sort === "asc" ? "desc" : "asc");
    setParams(next, { replace: true });
  }

  /**
   * 清除类型、年份、地点、标签，保留排序。
   */
  function handleClear() {
    const next = new URLSearchParams();
    if (query.sort === "asc") {
      next.set("sort", "asc");
    }
    setParams(next, { replace: true });
  }

  /**
   * 打开当前筛选序列中的指定帧。
   */
  function openAt(index: number) {
    setOpen({ index, filter: filterSignature });
  }

  const close = useCallback(() => setOpen(null), []);
  const step = useCallback(
    (delta: number) => {
      setOpen((currentOpen) => {
        if (!currentOpen || currentOpen.filter !== filterSignature || visible.length === 0) {
          return null;
        }
        return {
          index: (currentOpen.index + delta + visible.length) % visible.length,
          filter: currentOpen.filter,
        };
      });
    },
    [filterSignature, visible.length],
  );

  return (
    <div className="develop-channel" data-theme={lexicon.photography.key}>
      <ChannelHead
        backTo={hrefForKind(lexicon.photography.key, ia.id)}
        backLabel={category?.title ?? lexicon.photography.zh}
        title={lexicon.photoCatalog.zh}
        lead="按题材、年份、地点与标签筛选已发布照片。未选某维即不限制。时间默认最新优先。"
      />
      <div className="develop-project-gallery">
      <div className="filter-board">
        <FilterRow
          label="类型"
          values={[...photoTypeEntries.map((item) => item.key)]}
          selected={query.channels}
          captions={typeCaptions}
          onToggle={(value) => handleToggle("channel", value)}
        />
        <FilterRow
          label="年份"
          values={facets.years}
          selected={query.years}
          onToggle={(value) => handleToggle("year", value)}
        />
        <FilterRow
          label="地点"
          values={facets.places}
          selected={query.places}
          onToggle={(value) => handleToggle("place", value)}
        />
        <FilterRow
          label="标签"
          values={facets.tags}
          selected={query.tags}
          onToggle={(value) => handleToggle("tag", value)}
        />
        <div className="filter-toolbar">
          <button type="button" className="filter-sort" onClick={handleSortToggle}>
            时间：{query.sort === "asc" ? "从前到后" : "从后到前"}
          </button>
          <span className="filter-count">{visible.length} 张</span>
          {hasFilter ? (
            <button type="button" className="filter-clear" onClick={handleClear}>
              清除筛选
            </button>
          ) : null}
        </div>
      </div>
      {visible.length === 0 ? (
        <p className="note">当前筛选没有照片。</p>
      ) : (
        <div className="photo-masonry" data-count={visible.length}>
          {visible.map((frame, index) => {
            const label = resolveMediaDisplayName(frame.media);
            const bootFirst = index === 0;
            return (
              <button
                className="project-gallery-shot"
                type="button"
                key={frame.src}
                onClick={() => openAt(index)}
                aria-label={`查看${frame.work.title} ${label}`}
              >
                <img
                  src={assetUrl(frame.src)}
                  alt=""
                  loading={bootFirst ? "eager" : "lazy"}
                  {...(bootFirst ? { "data-boot-first": "" } : {})}
                />
              </button>
            );
          })}
        </div>
      )}
      </div>
      {openIndex != null && current && lightboxImages.length > 0 ? (
        <ImageLightbox
          images={lightboxImages}
          index={openIndex}
          title={current.work.title}
          summary={resolveMediaDescription(
            { description: current.media.description },
            current.work.summary,
          )}
          viewAll={{
            href: hrefForWork(current.work, ia.id),
            title: current.work.title,
            from: catalogFrom,
          }}
          onClose={close}
          onPrev={() => step(-1)}
          onNext={() => step(1)}
        />
      ) : null}
    </div>
  );
}
