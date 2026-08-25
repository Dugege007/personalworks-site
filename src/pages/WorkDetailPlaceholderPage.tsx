import { Link, useLocation } from "react-router-dom";
import { findCategoryByPath, findCollectionByPath } from "../content/site";
import "../styles/placeholder.css";

/**
 * 细目详情占位：返回对应分类页，主题跟随细目集合。
 */
export function WorkDetailPlaceholderPage() {
  const location = useLocation();
  const segments = location.pathname.split("/").filter(Boolean);
  const id = segments[segments.length - 1];
  const category = findCategoryByPath(location.pathname);
  const collection = findCollectionByPath(location.pathname);
  const backPath = category?.path ?? "/";
  const backLabel = category?.title ?? "分类";

  return (
    <div className="page" data-theme={collection?.theme ?? category?.theme ?? "home"}>
      <Link className="back" to={backPath}>
        ← 返回{backLabel}
      </Link>
      <div className="page-kicker">ARCHIVE / {id || "unknown"}</div>
      <h1>{collection ? `${collection.title} · 作品详情占位` : "作品详情占位"}</h1>
      <p className="page-lead">
        编号 `{id}` 的正文、图组将在内容入库后替换。当前页只用于打通分类到细目的路径。
      </p>
      <div className="play-stage">
        <div>
          <div className="accent-bar" />
          <p>媒体框 · 待替换</p>
        </div>
      </div>
    </div>
  );
}
