import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { FilterRow } from "../components/work/FilterRow";
import { resolveLead } from "../content/copyDisplay";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import { workCoverSrc } from "../content/stockMedia";
import {
  collectPhotoFacets,
  filterPhotoWorks,
  listPublishedPhotoWorks,
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

const photoTypeEntries = [lexicon.landscapePhoto, lexicon.humanistPhoto, lexicon.portraitPhoto];

type FilterKey = "channel" | "year" | "place";

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
    channels: readList(params, "channel"),
    years: readList(params, "year"),
    places: readList(params, "place"),
    tags: [],
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
  const hasFilter = query.channels.length > 0 || query.years.length > 0 || query.places.length > 0;
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
        lead="按年份、地点回看一次外出。可选含某题材。未选某维即不限制。"
      />
      <div className="develop-project-gallery">
        <div className="filter-board">
          <FilterRow
            label="题材"
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
          <div className="photo-masonry" data-count={visible.length}>
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
          </div>
        )}
      </div>
    </div>
  );
}
