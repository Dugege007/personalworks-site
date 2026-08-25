import { Link, useParams } from "react-router-dom";
import { findNavByPath } from "../content/site";
import "../styles/placeholder.css";

type WorkDetailPlaceholderPageProps = {
  listPath: string;
};

export function WorkDetailPlaceholderPage({ listPath }: WorkDetailPlaceholderPageProps) {
  const { id } = useParams();
  const nav = findNavByPath(listPath);

  return (
    <div className="page" data-theme={nav.theme}>
      <Link className="back" to={listPath}>
        ← 返回{nav.label}
      </Link>
      <div className="page-kicker">ARCHIVE / {id ?? "unknown"}</div>
      <h1>作品详情占位</h1>
      <p className="page-lead">
        编号 `{id}` 的正文、图组将在内容入库后替换。当前页只用于打通列表到详情的路径。
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
