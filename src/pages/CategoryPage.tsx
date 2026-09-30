import { Link, Navigate } from "react-router-dom";
import { ConstructionSetList } from "../components/work/ConstructionSetList";
import { lexicon } from "../content/lexicon";
import { categories, type WorkCollection } from "../content/site";
import { stockPlaceholderSrc, workCoverSrc } from "../content/stockMedia";
import { TermText } from "../components/TermText";
import { resolveLead } from "../content/copyDisplay";
import { studioAliasForDisplay } from "../content/studios";
import { listPublishedWorks } from "../content/works";
import { iaOfSkin } from "../ia";
import { hrefForDevelopKind, hrefForPhotoCatalog } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";

type CategoryPageProps = {
  categoryId: string;
};

/**
 * 分类页：同门类下的细目列表。摄影只出门，不内嵌批次墙。
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

  const isPhoto = category.id === lexicon.photography.key;

  return (
    <div className="page" data-theme={category.theme}>
      <div className="page-kicker">
        {category.index} / {category.indexEn}
      </div>
      <h1>{category.title}</h1>
      {resolveLead(category.lead) ? (
        <p className="page-lead">
          <TermText text={resolveLead(category.lead)!} />
        </p>
      ) : null}
      {isPhoto ? (
        <>
          <Link className="archive-btn" to={`/${lexicon.photography.key}/${lexicon.photoCatalog.key}`}>
            浏览全部作品
            <span aria-hidden="true">→</span>
          </Link>
          <Link className="archive-textlink" to={`/${lexicon.photography.key}/${lexicon.photoShoots.key}`}>
            {lexicon.photoShoots.zh}
          </Link>
        </>
      ) : null}
      {isPhoto ? (
        <div className="card-grid" data-count={category.collections.length}>
          {category.collections.map((collection) => (
            <PhotoDoorCard collection={collection} key={collection.id} />
          ))}
        </div>
      ) : (
        category.collections.map((collection) => (
          <section key={collection.id} className="category-block" data-theme={collection.theme}>
            <div className="page-kicker">{collection.titleDeco}</div>
            <h2>{collection.title}</h2>
            {resolveLead(collection.lead) ? (
              <p>
                <TermText text={resolveLead(collection.lead)!} />
              </p>
            ) : null}
            {collection.comingSoon ? (
              <p className="note">位置已留，作品待收录。不进入详情占位。</p>
            ) : null}
            {collection.id === lexicon.landscapeCDs.key && !collection.comingSoon ? (
              <ConstructionSetList
                works={listPublishedWorks(collection.id)}
                hrefFor={(work) => `${collection.detailBase}/${work.id}`}
              />
            ) : (
            <div className="card-grid">
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
            )}
          </section>
        ))
      )}
      <p className="note">媒体地址走 VITE_ASSET_BASE；只展示已列入内容池的对象键。</p>
    </div>
  );
}

type PhotoDoorCardProps = {
  collection: WorkCollection;
};

/**
 * 摄影题材门：封面 + 名称，有成片则进总览筛选。
 */
function PhotoDoorCard({ collection }: PhotoDoorCardProps) {
  const works = collection.comingSoon ? [] : listPublishedWorks(collection.id);
  const cover = works[0] ? workCoverSrc(works[0], 0) : stockPlaceholderSrc(collection.id, 1);
  const lead = collection.comingSoon
    ? "位置已留，作品待收录。不进入详情占位。"
    : resolveLead(collection.lead);
  const body = (
    <>
      <figure className="card-cover">
        <img src={assetUrl(cover)} alt="" />
      </figure>
      <div className="card-body">
        <small>{collection.titleDeco}</small>
        <div>
          <h2>{collection.title}</h2>
          {lead ? <p>{lead}</p> : null}
        </div>
      </div>
    </>
  );

  if (collection.comingSoon) {
    return (
      <div className="card is-soon" data-theme={collection.theme}>
        {body}
      </div>
    );
  }

  return (
    <Link className="card" data-theme={collection.theme} to={hrefForPhotoCatalog(collection.id)}>
      {body}
    </Link>
  );
}
