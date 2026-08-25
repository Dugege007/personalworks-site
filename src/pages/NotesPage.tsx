import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { notePreviewImages, placeholderNotes } from "../content/site";
import "../styles/placeholder.css";

export function NotesPage() {
  return (
    <div className="page" data-theme={lexicon.notes.key}>
      <div className="page-kicker">06 / {lexicon.notes.deco}</div>
      <h1>{lexicon.notes.zh}</h1>
      <p className="page-lead">
        阅读向栏目，不进入主页滚动。列表从文章图组中最多取出三张作预览。手绘作为其中一篇收录。
      </p>
      <div className="note-list">
        {placeholderNotes.map((note) => (
          <Link className="note-card" key={note.slug} to={`/${lexicon.notes.key}/${note.slug}`}>
            <small>{note.date}</small>
            <h2>{note.title}</h2>
            <p>{note.summary}</p>
            <div className="note-previews" aria-hidden="true">
              {notePreviewImages(note).map((label) => (
                <span className="note-preview" key={label}>
                  {label}
                </span>
              ))}
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
