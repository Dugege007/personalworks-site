import { Link, useParams } from "react-router-dom";
import { NoteMarkdown } from "../components/NoteMarkdown";
import { loadNote, noteTheme } from "../content/notes/read";
import { lexicon } from "../content/lexicon";
import "../styles/placeholder.css";

export function NoteDetailPage() {
  const { slug } = useParams();
  const note = loadNote(slug);
  const theme = noteTheme(slug);

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
      <NoteMarkdown markdown={note.body} />
    </div>
  );
}
