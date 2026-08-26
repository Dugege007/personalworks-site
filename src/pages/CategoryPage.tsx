import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import { listPublishedWorks } from "../content/works";
import "../styles/placeholder.css";

type CategoryPageProps = {
  categoryId: string;
};

/**
 * 分类页：同门类下的细目列表，封面进入各自详情。
 */
export function CategoryPage({ categoryId }: CategoryPageProps) {
  const category = categories.find((item) => item.id === categoryId);

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
      <p className="page-lead">{category.lead}</p>
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
          <p>{collection.lead}</p>
          {collection.comingSoon ? (
            <p className="note">位置已留，作品待收录。不进入详情占位。</p>
          ) : null}
          <div className="card-grid">
            {collection.comingSoon
              ? collection.frames.map((label, index) => (
                  <div className="card is-soon" key={label}>
                    <small>
                      0{index + 1} / {collection.titleDeco}
                    </small>
                    <div>
                      <h2>{label}</h2>
                      <p>待收录。</p>
                    </div>
                  </div>
                ))
              : listPublishedWorks(collection.id).map((work, index) => (
                  <Link className="card" key={work.id} to={`${collection.detailBase}/${work.id}`}>
                    <small>
                      0{index + 1} / {work.year}
                    </small>
                    <div>
                      <h2>{work.title}</h2>
                      <p>{work.summary}</p>
                    </div>
                  </Link>
                ))}
          </div>
        </section>
      ))}
      <p className="note">详情为占位正文与框景；媒体地址走 VITE_ASSET_BASE，本轮不加载原片。</p>
    </div>
  );
}
