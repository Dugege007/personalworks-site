import { Link, Navigate } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import { stockPlaceholderSrc, workCoverSrc } from "../content/stockMedia";
import { resolveLead } from "../content/copyDisplay";
import { studioAliasForDisplay } from "../content/studios";
import { listPublishedWorks } from "../content/works";
import { iaOfSkin } from "../ia";
import { hrefForDevelopKind } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";
import "../styles/photo-masonry.css";

type CategoryPageProps = {
  categoryId: string;
};

/**
 * 分类页：同门类下的细目列表，封面进入各自详情。
 */
export function CategoryPage({ categoryId }: CategoryPageProps) {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const category = categories.find((item) => item.id === categoryId);

  if (ia.id === "develop-editorial") {
    return <Navigate to={hrefForDevelopKind(categoryId)} replace />;
  }

  if (!category) {
    return (
      <div className="page">
        <h1>栏目不存在</h1>
        <p className="page-lead">未找到对应分类。</p>
      </div>
    );
  }

  return (
    <div className="page" data-theme={category.theme}>
      <div className="page-kicker">
        {category.index} / {category.indexEn}
      </div>
      <h1>{category.title}</h1>
      {resolveLead(category.lead) ? <p className="page-lead">{resolveLead(category.lead)}</p> : null}
      {category.id === lexicon.photography.key ? (
        <Link className="archive-btn" to={`/${lexicon.photography.key}/${lexicon.photoCatalog.key}`}>
          浏览全部作品
          <span aria-hidden="true">→</span>
        </Link>
      ) : null}
      {category.collections.map((collection) => (
        <section key={collection.id} className="category-block" data-theme={collection.theme}>
          <div className="page-kicker">{collection.titleDeco}</div>
          <h2>{collection.title}</h2>
          {resolveLead(collection.lead) ? <p>{resolveLead(collection.lead)}</p> : null}
          {collection.comingSoon ? (
            <p className="note">位置已留，作品待收录。不进入详情占位。</p>
          ) : null}
          <div
            className={category.id === lexicon.photography.key ? "photo-masonry" : "card-grid"}
            data-count={
              category.id === lexicon.photography.key
                ? collection.comingSoon
                  ? collection.frames.length
                  : listPublishedWorks(collection.id).length
                : undefined
            }
          >
            {collection.comingSoon
              ? collection.frames.map((label, index) => (
                  <div className="card is-soon" key={label}>
                    <figure className="card-cover">
                      <img src={assetUrl(stockPlaceholderSrc(collection.id, index + 1))} alt="" />
                    </figure>
                    <div className="card-body">
                      <small>
                        0{index + 1} / {collection.titleDeco}
                      </small>
                      <div>
                        <h2>{label}</h2>
                        <p>待收录。</p>
                      </div>
                    </div>
                  </div>
                ))
              : listPublishedWorks(collection.id).map((work, index) => {
                  const studio = studioAliasForDisplay(work);
                  return (
                  <Link className="card" key={work.id} to={`${collection.detailBase}/${work.id}`}>
                    <figure className="card-cover">
                      <img src={assetUrl(workCoverSrc(work, index))} alt="" />
                    </figure>
                    <div className="card-body">
                      <small>
                        0{index + 1} / {work.year}
                        {studio ? ` / ${studio}` : ""}
                      </small>
                      <div>
                        <h2>{work.title}</h2>
                        {resolveLead(work.summary) ? <p>{resolveLead(work.summary)}</p> : null}
                      </div>
                    </div>
                  </Link>
                  );
                })}
          </div>
        </section>
      ))}
      <p className="note">媒体地址走 VITE_ASSET_BASE；只展示已列入内容池的对象键。</p>
    </div>
  );
}
