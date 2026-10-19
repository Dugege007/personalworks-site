import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { FilterRow } from "../components/work/FilterRow";
import { lexicon } from "../content/lexicon";
import {
  disabledNoteTagFacets,
  filterNotesByTags,
  noteTagFacetValues,
  noteUntaggedKey,
  noteUntaggedLabel,
  occupiedNoteTags,
  parseNoteTagQuery,
  selectedNoteTagFacets,
  toggleNoteTagParam,
} from "../content/notes/tagFilter";
import { archiveIndexByNavId } from "../content/site";
import { queryNotes } from "../ia/query";
import { assetUrl } from "../lib/assets";
import "../styles/placeholder.css";

export function NotesPage() {
  const notes = useMemo(() => queryNotes({ source: "notes" }), []);
  const [params, setParams] = useSearchParams();
  const filterSignature = params.toString();
  const query = useMemo(() => parseNoteTagQuery(new URLSearchParams(filterSignature)), [filterSignature]);
  const tagValues = useMemo(() => noteTagFacetValues(notes), [notes]);
  const visible = useMemo(() => filterNotesByTags(notes, query), [notes, query]);
  const occupied = useMemo(() => occupiedNoteTags(visible), [visible]);
  const hasFilter = query.tags.length > 0 || query.untagged;

  /**
   * 点选自由标签或「无标签」。语义项不写入 tag 参数。
   */
  function handleToggle(value: string) {
    setParams(toggleNoteTagParam(params, value), { replace: true });
  }

  /**
   * 去掉标签筛选，列表恢复为全部心得。
   */
  function handleClear() {
    const next = new URLSearchParams(params);
    next.delete("tag");
    next.delete("untagged");
    setParams(next, { replace: true });
  }

  return (
    <div className="page" data-theme={lexicon.notes.key}>
      <div className="page-kicker">
        {archiveIndexByNavId(lexicon.notes.key)} / {lexicon.notes.deco}
      </div>
      <h1>{lexicon.notes.zh}</h1>
      <p className="page-lead">
        阅读向栏目，不进入主页滚动。列表从文章图组中最多取出三张作预览。手绘作为其中一篇收录。
      </p>
      {tagValues.length > 0 || hasFilter ? (
        <div className="filter-board">
          <FilterRow
            label="标签"
            values={tagValues}
            selected={selectedNoteTagFacets(query)}
            disabled={disabledNoteTagFacets(tagValues, occupied, query)}
            captions={{ [noteUntaggedKey]: noteUntaggedLabel }}
            onToggle={handleToggle}
          />
          <div className="filter-toolbar">
            <span className="filter-count">{visible.length} 篇</span>
            {hasFilter ? (
              <button type="button" className="filter-clear" onClick={handleClear}>
                清除筛选
              </button>
            ) : null}
          </div>
        </div>
      ) : null}
      {visible.length === 0 && hasFilter ? (
        <p className="note">当前筛选没有心得。</p>
      ) : (
        <div className="note-list">
          {visible.map((note) => (
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
      )}
    </div>
  );
}
