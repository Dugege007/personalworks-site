import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { archiveIndexByNavId } from "../content/site";
import { queryNotes } from "../ia/query";
import { assetUrl } from "../lib/assets";
import "../styles/placeholder.css";

export function NotesPage() {
  const notes = queryNotes({ source: "notes" });
  return (
    <div className="page" data-theme={lexicon.notes.key}>
      <div className="page-kicker">
        {archiveIndexByNavId(lexicon.notes.key)} / {lexicon.notes.deco}
      </div>
      <h1>{lexicon.notes.zh}</h1>
      <p className="page-lead">
        阅读向栏目，不进入主页滚动。列表从文章图组中最多取出三张作预览。手绘作为其中一篇收录。
      </p>
      <div className="note-list">
        {notes.map((note) => (
          <Link className="note-card" key={note.slug} to={`/${lexicon.notes.key}/${note.slug}`}>
            <small>{note.date}</small>
            <h2>{note.title}</h2>
            <p>{note.summary}</p>
            {note.previewImages.length > 0 ? (
              <div className="note-previews" aria-hidden="true">
                {note.previewImages.map((src) =>
                  src.includes("/") ? (
                    <img key={src} src={assetUrl(src)} alt="" />
                  ) : (
                    <span className="note-preview" key={src}>
                      {src}
                    </span>
                  ),
                )}
              </div>
            ) : null}
          </Link>
        ))}
      </div>
    </div>
  );
}
