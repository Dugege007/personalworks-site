import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { archiveIndexByNavId, categories } from "../content/site";
import {
  collectPhotoFacets,
  filterPhotoWorks,
  listPublishedPhotoWorks,
  sortPhotoWorks,
  type PhotoCatalogQuery,
} from "../content/works";
import { workCoverSrc } from "../content/stockMedia";
import { assetUrl } from "../lib/assets";
import "../styles/placeholder.css";
import "../styles/photo-masonry.css";

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
 * 类型键到中文名。
 */
function channelTitle(key: string): string {
  return photoTypeEntries.find((item) => item.key === key)?.zh ?? key;
}

type FilterRowProps = {
  label: string;
  values: string[];
  selected: string[];
  captions?: Record<string, string>;
  onToggle: (value: string) => void;
};

/**
 * 一行筛选项，形态接近视频站的分类条。
 */
function FilterRow({ label, values, selected, captions, onToggle }: FilterRowProps) {
  if (values.length === 0) {
    return null;
  }
  return (
    <div className="filter-row">
      <span className="filter-row-label">{label}</span>
      <div className="filter-chips">
        {values.map((value) => {
          const on = selected.includes(value);
          return (
            <button
              key={value}
              type="button"
              className={on ? "filter-chip is-on" : "filter-chip"}
              aria-pressed={on}
              onClick={() => onToggle(value)}
            >
              {captions?.[value] ?? value}
            </button>
          );
        })}
      </div>
    </div>
  );
}

/**
 * 摄影总览：四维筛选 + 时间正倒序，卡片进入既有详情。
 */
export function PhotoCatalogPage() {
  const category = categories.find((item) => item.id === lexicon.photography.key);
  const [params, setParams] = useSearchParams();
  const query = parseQuery(params);
  const published = useMemo(() => listPublishedPhotoWorks(), []);
  const facets = useMemo(() => collectPhotoFacets(published), [published]);
  const visible = useMemo(
    () => sortPhotoWorks(filterPhotoWorks(published, query), query.sort),
    [published, query],
  );
  const hasFilter =
    query.channels.length > 0 || query.years.length > 0 || query.places.length > 0 || query.tags.length > 0;

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

  return (
    <div className="page" data-theme={lexicon.photography.key}>
      <Link className="back" to={photoRoot}>
        ← 返回{category?.title ?? lexicon.photography.zh}
      </Link>
      <div className="page-kicker">
        {category?.index ?? archiveIndexByNavId(lexicon.photography.key)} / {lexicon.photoCatalog.deco}
      </div>
      <h1>{lexicon.photoCatalog.zh}</h1>
      <p className="page-lead">按类型、年份、地点与标签筛选。未选某维即不限制。时间默认最新优先。</p>
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
          <span className="filter-count">{visible.length} 条</span>
          {hasFilter ? (
            <button type="button" className="filter-clear" onClick={handleClear}>
              清除筛选
            </button>
          ) : null}
        </div>
      </div>
      {visible.length === 0 ? (
        <p className="note">当前筛选没有作品。</p>
      ) : (
        <div className="photo-masonry" data-count={visible.length}>
          {visible.map((work) => (
            <Link
              className="card"
              key={`${work.channel}-${work.id}`}
              to={`${photoRoot}/${work.channel}/${work.id}`}
              state={{ from: `${catalogPath}${params.toString() ? `?${params.toString()}` : ""}` }}
            >
              <figure className="card-cover">
                <img src={assetUrl(workCoverSrc(work))} alt="" />
              </figure>
              <div className="card-body">
                <small>
                  {work.year}
                  {work.place ? ` / ${work.place}` : ""} / {channelTitle(work.channel)}
                </small>
                <div>
                  <h2>{work.title}</h2>
                  <p>{work.summary}</p>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
