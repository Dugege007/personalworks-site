import { Navigate, useSearchParams } from "react-router-dom";
import { CoverTile } from "../components/work/CoverTile";
import { lexicon } from "../content/lexicon";
import { stockPlaceholderSrc } from "../content/stockMedia";
import { channelsOfKind } from "../ia/query";
import {
  hrefForDevelopKind,
  isDevelopWorkKind,
  listDevelopKindDoors,
} from "../ia/workTree";
import "../styles/develop-work.css";

/**
 * 显影作品第一层：四门非对称画册，不列细目条目。
 */
export function WorkIndexPage() {
  const [params] = useSearchParams();
  const kind = params.get("kind");
  if (kind && isDevelopWorkKind(kind)) {
    return <Navigate to={hrefForDevelopKind(kind)} replace />;
  }

  const doors = listDevelopKindDoors();

  return (
    <div className="develop-work-gate" data-theme={lexicon.workIndex.key}>
      <h1 className="develop-work-visually-hidden">{lexicon.workIndex.zh}</h1>
      {doors.map((door, index) => (
        <CoverTile
          key={door.kind}
          className={`develop-work-gate-cell is-${index + 1}`}
          wellClass={`develop-work-well is-kind-${door.kind}`}
          to={door.href}
          src={door.coverSrc}
          fallbackSrc={stockPlaceholderSrc(
            door.kind === lexicon.gameDev.key
              ? lexicon.gameDev.key
              : (channelsOfKind(door.kind)[0] ?? door.kind),
            1,
          )}
        >
          <strong>{door.zh}</strong>
          <em>{door.deco}</em>
        </CoverTile>
      ))}
    </div>
  );
}
