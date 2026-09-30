import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { FilterRow } from "../components/work/FilterRow";
import { PhotoMasonry } from "../components/work/PhotoMasonry";
import { resolveLead } from "../content/copyDisplay";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import { workCoverSrc } from "../content/stockMedia";
import {
  collectPhotoFacets,
  orderPhotoCustomTags,
  photoTagResourceCounts,
  disabledPhotoFacetValues,
  disabledPrimaryFacets,
  filterPhotoWorks,
  isPhotoDefaultTag,
  listPublishedPhotoWorks,
  occupiedPhotoFacetsFromWorks,
  photoTypeChipLabel,
  photoUntaggedKey,
  primaryFacetValues,
  readPhotoThemes,
  selectedPrimaryFacets,
  sortPhotoWorks,
  type PhotoCatalogQuery,
} from "../content/works";
import { hrefForKind, hrefForWork } from "../ia/href";
import { hrefForPhotoShoots } from "../ia/workTree";
import { iaOfSkin } from "../ia";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/develop-work.css";
import "../styles/placeholder.css";
import "../styles/photo-masonry.css";

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
 * 解析拍摄列表查询；不含张级标签。缺省时间为最新优先。
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
 * 主题总览：批次卡瀑布流 + 少维筛选；点卡进入该次拍摄详情。
 * //TODO: 历史重复批次合并为一次外出
 */
export function PhotoShootsPage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const category = categories.find((item) => item.id === lexicon.photography.key);
  const [params, setParams] = useSearchParams();
  const filterSignature = params.toString();
  const query = useMemo(() => parseQuery(new URLSearchParams(filterSignature)), [filterSignature]);
  const shootsFrom = filterSignature ? `${hrefForPhotoShoots()}?${filterSignature}` : hrefForPhotoShoots();
  const published = useMemo(() => listPublishedPhotoWorks(), []);
  const facets = useMemo(() => collectPhotoFacets(published), [published]);
  const visible = useMemo(() => sortPhotoWorks(filterPhotoWorks(published, query), query.sort), [published, query]);
  const occupied = useMemo(() => occupiedPhotoFacetsFromWorks(visible), [visible]);
  const hasFilter =
    query.themes.length > 0 ||
    query.years.length > 0 ||
    query.places.length > 0 ||
    query.tags.length > 0 ||
    query.untagged;
  const catalogOccupied = useMemo(() => occupiedPhotoFacetsFromWorks(published), [published]);
  const primaryValues = primaryFacetValues(catalogOccupied.untagged);
  const customTags = useMemo(
    () => orderPhotoCustomTags(facets.tags, photoTagResourceCounts(published)),
    [facets.tags, published],
  );
  const typeCaptions = {
    ...Object.fromEntries(photoTypeEntries.map((item) => [item.key, photoTypeChipLabel(item.zh)])),
    展馆: "展馆",
    随拍: "随拍",
    [photoUntaggedKey]: "无标签",
  };

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

  /**
   * 写入某一维的开关结果。
   */
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
   * 清除题材、年份、地点，保留排序。
   */
  function handleClear() {
    const next = new URLSearchParams();
    if (query.sort === "asc") {
      next.set("sort", "asc");
    }
    setParams(next, { replace: true });
  }

  return (
    <div className="develop-channel" data-theme={lexicon.photography.key}>
      <ChannelHead
        backTo={hrefForKind(lexicon.photography.key, ia.id)}
        backLabel={category?.title ?? lexicon.photography.zh}
        title={lexicon.photoShoots.zh}
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
            <span className="filter-count">{visible.length} 次</span>
            {hasFilter ? (
              <button type="button" className="filter-clear" onClick={handleClear}>
                清除筛选
              </button>
            ) : null}
          </div>
        </div>
        {visible.length === 0 ? (
          <p className="note">当前筛选没有主题。</p>
        ) : (
          <PhotoMasonry count={visible.length}>
            {visible.map((work, index) => (
              <Link
                className="card"
                key={work.id}
                to={hrefForWork(work, ia.id)}
                state={{ from: shootsFrom }}
              >
                <figure className="card-cover">
                  <img
                    src={assetUrl(workCoverSrc(work, index))}
                    alt=""
                    {...(index === 0 ? { "data-boot-first": "" } : {})}
                  />
                </figure>
                <div className="card-body">
                  <small>
                    {work.year}
                    {work.place ? ` / ${work.place}` : ""}
                  </small>
                  <div>
                    <h2>{work.title}</h2>
                    {resolveLead(work.summary) ? <p>{resolveLead(work.summary)}</p> : null}
                  </div>
                </div>
              </Link>
            ))}
          </PhotoMasonry>
        )}
      </div>
    </div>
  );
}
