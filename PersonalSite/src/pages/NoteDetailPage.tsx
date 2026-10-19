import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { NoteMarkdown } from "../components/NoteMarkdown";
import { lexicon } from "../content/lexicon";
import { loadNote, noteTheme } from "../content/notes/read";
import { displayNoteTimestamp, noteTimestampDateTime } from "../content/notes/select";
import { noteTagSearch, noteTagsOf } from "../content/notes/tagFilter";
import type { NoteCard } from "../content/site";
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

  const heading = note.mainTitle?.trim() || note.title;
  const subtitle = note.subtitle?.trim() ?? "";

  return (
    <div className="page" data-theme={theme}>
      <Link className="back" to={`/${lexicon.notes.key}`}>
        ← 返回{lexicon.notes.zh}
      </Link>
      {note.slug === lexicon.sketching.key ? (
        <div className="page-kicker">{lexicon.sketching.deco}</div>
      ) : null}
      <h1>{heading}</h1>
      {subtitle ? <p className="note-subtitle">{subtitle}</p> : null}
      <NoteTimes key={note.slug} note={note} />
      <NoteTags tags={note.tags} />
      <p className="page-lead">{note.summary}</p>
      <NoteMarkdown markdown={note.body} />
    </div>
  );
}

/**
 * 正文标签芯片。没有标签时不渲染。点击后打开带该标签筛选的列表。
 */
function NoteTags({ tags }: { tags: NoteCard["tags"] }) {
  const values = noteTagsOf({ tags });
  if (values.length === 0) {
    return null;
  }
  return (
    <nav className="note-tags" aria-label="正文标签">
      {values.map((tag) => (
        <Link
          key={tag}
          className="note-tag"
          to={`/${lexicon.notes.key}${noteTagSearch({ tags: [tag], untagged: false })}`}
        >
          {tag}
        </Link>
      ))}
    </nav>
  );
}

/**
 * 详情页时间行：创建时间与最近修改时间。点最近修改展开全部修订。
 */
function NoteTimes({ note }: { note: NoteCard }) {
  const [open, setOpen] = useState(false);
  const createdRaw = note.createdAt?.trim() || note.date;
  const revisedRaw = note.revisedAt?.trim() || createdRaw;
  const revisionListId = `note-revisions-${note.slug}`;
  // 索引按从早到晚追加。展开时最近一条在前，和「最近修改」对得上。
  const revisions = [...(note.revisions ?? [])].reverse();

  return (
    <>
      <div className="note-meta">
        <span className="note-meta-item">
          <span className="note-meta-label">创建</span>
          <time dateTime={noteTimestampDateTime(createdRaw)}>{displayNoteTimestamp(createdRaw)}</time>
        </span>
        <button
          type="button"
          className="note-revised"
          aria-expanded={open}
          aria-controls={revisionListId}
          onClick={() => setOpen((current) => !current)}
        >
          <span className="note-meta-label">最近修改</span>
          <time dateTime={noteTimestampDateTime(revisedRaw)}>{displayNoteTimestamp(revisedRaw)}</time>
          <span className="note-visually-hidden">{open ? "，收起修订记录" : "，展开修订记录"}</span>
        </button>
      </div>
      <div className="note-revisions" id={revisionListId} hidden={!open}>
        <ol>
          {revisions.length === 0 ? (
            <li className="note-revision-empty">尚无修订</li>
          ) : (
            revisions.map((item, index) => (
              <li key={`${item.at}-${index}`}>
                <time dateTime={noteTimestampDateTime(item.at)}>{displayNoteTimestamp(item.at)}</time>
                <span className={item.summary ? undefined : "is-blank"}>{item.summary || "（无说明）"}</span>
              </li>
            ))
          )}
        </ol>
      </div>
    </>
  );
}
