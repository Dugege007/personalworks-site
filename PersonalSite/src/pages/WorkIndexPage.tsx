import { type PointerEvent } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { stockPlaceholderSrc } from "../content/stockMedia";
import { channelsOfKind } from "../ia/query";
import {
  hrefForDevelopKind,
  isDevelopWorkKind,
  listDevelopKindDoors,
  type DevelopKindDoor,
} from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { comboRelay4s, fixedDraw, uniqueDraws } from "../lib/comboCycle";
import { useComboRelay, useComboShown } from "../lib/useComboRelay";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
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

  return <WorkGate />;
}

/**
 * 四门共用一个 4 秒计时器；到点只切一门，从左到右。
 * 鼠标停在某一门上时跳过该门。图池不足两张的门不占这一轮。
 */
function WorkGate() {
  const reduced = usePrefersReducedMotion();
  const doors = listDevelopKindDoors();
  const { advance, bind } = useComboRelay(doors.length, comboRelay4s, reduced, (index) => {
    return (doors[index]?.srcs.length ?? 0) >= 2;
  });

  return (
    <div className="develop-work-gate" data-theme={lexicon.workIndex.key}>
      <h1 className="develop-work-visually-hidden">{lexicon.workIndex.zh}</h1>
      {doors.map((door, index) => (
        <KindDoorCard
          key={door.kind}
          door={door}
          index={index}
          advance={advance[index] ?? 0}
          {...bind(index)}
        />
      ))}
    </div>
  );
}

type KindDoorCardProps = {
  door: DevelopKindDoor;
  index: number;
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLElement>) => void;
  onPointerLeave: () => void;
};

/**
 * 门类图卡：四门点名换图。本门近 5 张不重复。换张为 0.9s 淡入。
 */
function KindDoorCard({ door, index, advance, onPointerEnter, onPointerLeave }: KindDoorCardProps) {
  const fallback = stockPlaceholderSrc(
    door.kind === lexicon.gameDev.key
      ? lexicon.gameDev.key
      : (channelsOfKind(door.kind)[0] ?? door.kind),
    1,
  );
  const srcs = door.srcs.length > 0 ? door.srcs : uniqueDraws([fixedDraw(fallback)]);
  const { shown, stacked, fail } = useComboShown(srcs, advance, comboRelay4s.noRepeat);

  return (
    <Link
      className={`develop-work-gate-cell is-${index + 1}`}
      to={door.href}
      data-kind={door.kind}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {stacked.length > 0 ? (
        stacked.map((src) => (
          <img
            key={src}
            className={src === shown ? "is-on" : undefined}
            src={assetUrl(src)}
            alt=""
            {...(index === 0 && src === shown ? { "data-boot-first": "" } : {})}
            onError={() => fail(src)}
          />
        ))
      ) : (
        <span className={`develop-work-well is-kind-${door.kind}`} aria-hidden="true" />
      )}
      <span className="develop-work-copy">
        <strong>{door.zh}</strong>
        <em>{door.deco}</em>
      </span>
    </Link>
  );
}
