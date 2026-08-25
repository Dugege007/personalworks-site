import { Link } from "react-router-dom";
import { categories } from "../content/site";
import "../styles/placeholder.css";

type CategoryPageProps = {
  categoryId: string;
};

/**
 * 分类页：同门类下的两个或三个细目列表，封面进入各自详情。
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
      {category.collections.map((collection) => (
        <section key={collection.id} className="category-block" data-theme={collection.theme}>
          <h2>{collection.title}</h2>
          <p>{collection.lead}</p>
          {collection.comingSoon ? (
            <p className="note">位置已留，作品待收录。不进入详情占位。</p>
          ) : null}
          <div className="card-grid">
            {collection.frames.map((label, index) => {
              const card = (
                <>
                  <small>
                    0{index + 1} / {collection.comingSoon ? "SOON" : "SAMPLE"}
                  </small>
                  <div>
                    <h2>{label}</h2>
                    <p>{collection.comingSoon ? "待收录。" : "待替换作品。点击进入详情占位。"}</p>
                  </div>
                </>
              );
              if (collection.comingSoon) {
                return (
                  <div className="card is-soon" key={label}>
                    {card}
                  </div>
                );
              }
              return (
                <Link className="card" key={label} to={`${collection.detailBase}/sample-${index + 1}`}>
                  {card}
                </Link>
              );
            })}
          </div>
        </section>
      ))}
      <p className="note">第一批仅验证分类到细目的路径，不加载真实媒体。</p>
    </div>
  );
}
