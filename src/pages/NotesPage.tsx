import { Link } from "react-router-dom";
import { placeholderNotes } from "../content/site";
import "../styles/placeholder.css";

export function NotesPage() {
  return (
    <div className="page" data-theme="notes">
      <div className="page-kicker">10 / NOTES</div>
      <h1>心得</h1>
      <p className="page-lead">阅读向栏目，不进入主页滚动。第一批为占位摘要。</p>
      <div className="card-grid">
        {placeholderNotes.map((note) => (
          <Link className="card" key={note.slug} to={`/notes/${note.slug}`}>
            <small>{note.date}</small>
            <div>
              <h2>{note.title}</h2>
              <p>{note.summary}</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
