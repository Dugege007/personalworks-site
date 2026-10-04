import { type PointerEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { ConstructionSetList } from "../components/work/ConstructionSetList";
import { lexicon } from "../content/lexicon";
import { categories, type WorkCollection } from "../content/site";
import { stockPlaceholderSrc, workCoverSrc } from "../content/stockMedia";
import { TermText } from "../components/TermText";
import { resolveLead } from "../content/copyDisplay";
import { studioAliasForDisplay } from "../content/studios";
import { listPublishedWorks, drawOfImage } from "../content/works";
import { iaOfSkin } from "../ia";
import { framesForPhotoDoor, hrefForDevelopKind, hrefForPhotoDoor } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { comboRelay3s, fixedDraw, uniqueDraws, type DrawSrc } from "../lib/comboCycle";
import { useComboRelay, useComboShown } from "../lib/useComboRelay";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
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
        <PhotoDoorGrid collections={category.collections} />
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

/**
 * 层境摄影五门：与显影同一套 3 秒换格规则。
 */
function PhotoDoorGrid({ collections }: { collections: WorkCollection[] }) {
  const reduced = usePrefersReducedMotion();
  const pools = collections.map((collection) =>
    uniqueDraws(framesForPhotoDoor(collection).map((frame) => drawOfImage(frame.media))),
  );
  const { advance, bind } = useComboRelay(collections.length, comboRelay3s, reduced, (index) => {
    return !collections[index]?.comingSoon && (pools[index]?.length ?? 0) >= 2;
  });

  return (
    <div className="card-grid" data-count={collections.length}>
      {collections.map((collection, index) => (
        <PhotoDoorCard
          key={collection.id}
          collection={collection}
          pool={pools[index] ?? []}
          advance={advance[index] ?? 0}
          {...bind(index)}
        />
      ))}
    </div>
  );
}

type PhotoDoorCardProps = {
  collection: WorkCollection;
  pool: DrawSrc[];
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLElement>) => void;
  onPointerLeave: () => void;
};

/**
 * 摄影题材门：封面按本门图池轮换，有成片则进总览筛选。
 */
function PhotoDoorCard({ collection, pool, advance, onPointerEnter, onPointerLeave }: PhotoDoorCardProps) {
  const srcs = pool.length > 0 ? pool : uniqueDraws([fixedDraw(stockPlaceholderSrc(collection.theme, 1))]);
  const { shown, stacked, fail } = useComboShown(srcs, advance, comboRelay3s.noRepeat);
  const lead = collection.comingSoon
    ? "位置已留，作品待收录。不进入详情占位。"
    : resolveLead(collection.lead);
  const body = (
    <>
      <figure className="card-cover is-cycling">
        {stacked.map((src) => (
          <img
            key={src}
            className={src === shown ? "is-on" : undefined}
            src={assetUrl(src)}
            alt=""
            onError={() => fail(src)}
          />
        ))}
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
      <div className="card is-soon" data-theme={collection.theme} onPointerEnter={onPointerEnter} onPointerLeave={onPointerLeave}>
        {body}
      </div>
    );
  }

  return (
    <Link
      className="card"
      data-theme={collection.theme}
      to={hrefForPhotoDoor(collection)}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {body}
    </Link>
  );
}
