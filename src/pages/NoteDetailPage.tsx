import { Link, useParams } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { placeholderNotes } from "../content/site";
import "../styles/placeholder.css";

export function NoteDetailPage() {
  const { slug } = useParams();
  const note = placeholderNotes.find((item) => item.slug === slug);
  const theme = slug === lexicon.sketching.key ? lexicon.sketching.key : lexicon.notes.key;

  if (!note) {
    return (
      <div className="page" data-theme={theme}>
        <Link className="back" to={`/${lexicon.notes.key}`}>
          ← 返回{lexicon.notes.zh}
        </Link>
        <h1>这篇笔记还不存在</h1>
        <p className="page-lead">编号 `{slug}` 未写入内容表。</p>
      </div>
    );
  }

  return (
    <div className="page" data-theme={theme}>
      <Link className="back" to={`/${lexicon.notes.key}`}>
        ← 返回{lexicon.notes.zh}
      </Link>
      <div className="page-kicker">
        {note.slug === lexicon.sketching.key ? lexicon.sketching.deco : note.date}
      </div>
      <h1>{note.title}</h1>
      <p className="page-lead">{note.summary}</p>
      <p className="archive-body">{note.body}</p>
      {note.previewImages.length > 0 ? (
        <div className="media-grid" aria-label="文章配图">
          {note.previewImages.map((label) => (
            <figure className="media-frame" key={label}>
              <span>{label}</span>
            </figure>
          ))}
        </div>
      ) : null}
    </div>
  );
}
