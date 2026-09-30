import { useCallback, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { FilterRow } from "../components/work/FilterRow";
import { PhotoMasonry } from "../components/work/PhotoMasonry";
import { ImageLightbox, type LightboxShot } from "../components/work/ImageLightbox";
import { resolveMediaDescription, resolveMediaDisplayName } from "../content/copyDisplay";
import { lexicon } from "../content/lexicon";
import { readPhotoExif } from "../content/photoExif";
import { categories } from "../content/site";
import {
  collectPhotoFacets,
  orderPhotoCustomTags,
  photoTagResourceCounts,
  disabledPhotoFacetValues,
  disabledPrimaryFacets,
  filterPhotoFrames,
  isPhotoDefaultTag,
  listPublishedPhotoFrames,
  listPublishedPhotoWorks,
  occupiedPhotoFacetsFromFrames,
  photoTypeChipLabel,
  photoUntaggedKey,
  primaryFacetValues,
  readPhotoThemes,
  selectedPrimaryFacets,
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

const photoTypeEntries = [
  lexicon.landscapePhoto,
  lexicon.humanistPhoto,
  lexicon.portraitPhoto,
  lexicon.gamePhoto,
  lexicon.aiPhoto,
];

type FilterKey = "year" | "place" | "tag";

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
    themes: readPhotoThemes(params),
    years: readList(params, "year"),
    places: readList(params, "place"),
    tags: readList(params, "tag"),
    untagged: params.get("untagged") === "1",
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
  const occupied = useMemo(() => occupiedPhotoFacetsFromFrames(visible), [visible]);
  const catalogOccupied = useMemo(() => occupiedPhotoFacetsFromFrames(published), [published]);
  const primaryValues = primaryFacetValues(catalogOccupied.untagged);
  const customTags = useMemo(
    () => orderPhotoCustomTags(facets.tags, photoTagResourceCounts(publishedWorks)),
    [facets.tags, publishedWorks],
  );
  const hasFilter =
    query.themes.length > 0 ||
    query.years.length > 0 ||
    query.places.length > 0 ||
    query.tags.length > 0 ||
    query.untagged;
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

  const typeCaptions = {
    ...Object.fromEntries(photoTypeEntries.map((item) => [item.key, photoTypeChipLabel(item.zh)])),
    展馆: "展馆",
    随拍: "随拍",
    [photoUntaggedKey]: "无标签",
  };

  /**
   * 写入某一维的开关结果。
   */
  /**
   * 类型第一行：冻结类型、默认自由标签，以及「无标签」。
   */
  function handlePrimaryToggle(value: string) {
    if (value === photoUntaggedKey) {
      const next = new URLSearchParams(params);
      if (query.untagged) {
        next.delete("untagged");
      } else {
        next.set("untagged", "1");
      }
      setParams(next, { replace: true });
      return;
    }
    handleToggle(isPhotoDefaultTag(value) ? "tag" : "theme", value);
  }

  function handleToggle(key: FilterKey | "theme", value: string) {
    if (key === "theme") {
      const remaining = query.themes.includes(value)
        ? query.themes.filter((item) => item !== value)
        : [...query.themes, value];
      const next = new URLSearchParams(params);
      next.delete("theme");
      next.delete("channel");
      for (const item of remaining) {
        next.append("theme", item);
      }
      setParams(next, { replace: true });
      return;
    }
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
      />
      <div className="develop-project-gallery">
      <div className="filter-board">
        <FilterRow
          label="类型"
          values={primaryValues}
          selected={selectedPrimaryFacets(query)}
          disabled={disabledPrimaryFacets(primaryValues, occupied, query)}
          captions={typeCaptions}
          onToggle={handlePrimaryToggle}
        />
        <FilterRow
          label=""
          values={customTags}
          selected={query.tags}
          disabled={disabledPhotoFacetValues(customTags, occupied.tags, query.tags)}
          onToggle={(value) => handleToggle("tag", value)}
        />
        <FilterRow
          label="年份"
          values={facets.years}
          selected={query.years}
          disabled={disabledPhotoFacetValues(facets.years, occupied.years, query.years)}
          onToggle={(value) => handleToggle("year", value)}
        />
        <FilterRow
          label="地点"
          values={facets.places}
          selected={query.places}
          disabled={disabledPhotoFacetValues(facets.places, occupied.places, query.places)}
          onToggle={(value) => handleToggle("place", value)}
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
        <PhotoMasonry count={visible.length}>
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
        </PhotoMasonry>
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
