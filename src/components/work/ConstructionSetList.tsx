import { Link } from "react-router-dom";
import { constructionSheets } from "../../content/constructionSheet";
import { studioAliasForDisplay } from "../../content/studios";
import { workCoverSrc } from "../../content/stockMedia";
import type { WorkRecord } from "../../content/works";
import { assetUrl } from "../../lib/assets";
import "../../styles/construction-sheets.css";

type ConstructionSetListProps = {
  works: WorkRecord[];
  hrefFor: (work: WorkRecord) => string;
};

/**
 * 景观施工图项目目录：每套只放封面，图在格内按原比例，不裁成封面墙。
 */
export function ConstructionSetList({ works, hrefFor }: ConstructionSetListProps) {
  if (works.length === 0) {
    return null;
  }

  return (
    <div className="cds-register">
      {works.map((work, index) => {
        const studio = studioAliasForDisplay(work);
        const count = constructionSheets(work.media).length;
        const src = workCoverSrc(work, index);
        return (
          <Link className="cds-register-row" key={`${work.channel}-${work.id}`} to={hrefFor(work)}>
            <span className="cds-register-sheet">
              <img
                src={assetUrl(src)}
                alt=""
                decoding="async"
                loading={index === 0 ? "eager" : "lazy"}
                fetchPriority={index === 0 ? "high" : "low"}
              />
            </span>
            <span className="cds-register-copy">
              <strong>{work.title}</strong>
              <span className="cds-register-meta">
                <span>{work.year}</span>
                {studio ? <span>{studio}</span> : null}
                <span>{count} 张</span>
              </span>
            </span>
          </Link>
        );
      })}
    </div>
  );
}
