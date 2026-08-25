import { Link, useParams } from "react-router-dom";
import { placeholderNotes } from "../content/site";
import "../styles/placeholder.css";

export function NoteDetailPage() {
  const { slug } = useParams();
  const note = placeholderNotes.find((item) => item.slug === slug);

  if (!note) {
    return (
      <div className="page" data-theme="notes">
        <Link className="back" to="/notes">
          ← 返回心得
        </Link>
        <h1>这篇笔记还不存在</h1>
        <p className="page-lead">编号 `{slug}` 未写入内容表。</p>
      </div>
    );
  }

  return (
    <div className="page" data-theme="notes">
      <Link className="back" to="/notes">
        ← 返回心得
      </Link>
      <div className="page-kicker">{note.date}</div>
      <h1>{note.title}</h1>
      <p className="page-lead">{note.summary}</p>
      <p>
        占位正文。后续用 Markdown 或内容文件替换。行宽与行高按阅读页控制，不再叠加测绘动效。
      </p>
    </div>
  );
}
