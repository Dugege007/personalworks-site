import { type PointerEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { lexicon } from "../content/lexicon";
import { categories, gameProjects, playableGames } from "../content/site";
import { stockPlaceholderSrc } from "../content/stockMedia";
import {
  isDevelopWorkKind,
  kindEn,
  listDevelopChannelDoors,
  workIndexRoot,
  type DevelopChannelDoor,
} from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { comboRelay3s, fixedDraw, uniqueDraws } from "../lib/comboCycle";
import { useComboRelay, useComboShown } from "../lib/useComboRelay";
import { usePrefersReducedMotion } from "../hooks/usePrefersReducedMotion";
import { tx } from "../prefs/tx";
import { usePrefs } from "../prefs/PrefsProvider";
import { WorkDetailPage } from "./WorkDetailPage";
import "../styles/develop-work.css";

type WorkKindViewProps = {
  kind: string;
};

/**
 * `/work-index/:kind`：四门键进分类页，其余当作旧作品 id。
 */
export function WorkIndexSegmentPage() {
  const { kind } = useParams();
  if (kind && isDevelopWorkKind(kind)) {
    return <WorkKindPage kind={kind} />;
  }
  return <WorkDetailPage />;
}

/**
 * 显影作品第二层：大类分类页，只给细目入口，不摊开全部条目。
 */
export function WorkKindPage({ kind }: WorkKindViewProps) {
  const { locale } = usePrefs();
  const category = categories.find((item) => item.id === kind);
  const doors = listDevelopChannelDoors(kind);
  const title = tx({ "zh-CN": category?.title ?? kind, en: kindEn(kind) }, locale);
  const lead = category?.lead ?? "";
  const photoKind = kind === lexicon.photography.key;
  const gameKind = kind === lexicon.gameDev.key;

  return (
    <div className="develop-kind" data-theme={kind}>
      <ChannelHead backTo={workIndexRoot()} backLabel={lexicon.workIndex.zh} title={title} lead={lead}>
        {photoKind ? (
          <div className="develop-work-textlinks">
            <Link className="develop-work-textlink" to={`/${lexicon.photography.key}/${lexicon.photoCatalog.key}`}>
              {lexicon.photoCatalog.zh}
            </Link>
            <Link className="develop-work-textlink" to={`/${lexicon.photography.key}/${lexicon.photoShoots.key}`}>
              {lexicon.photoShoots.zh}
            </Link>
          </div>
        ) : null}
        {gameKind && playableGames.length > 0 ? (
          <Link
            className="develop-work-textlink"
            to={`${workIndexRoot()}/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`}
          >
            {lexicon.gameMenu.zh}
          </Link>
        ) : null}
      </ChannelHead>
      {gameKind ? (
        <div className="develop-game-projects">
          {playableGames.length === 0 ? (
            <p className="develop-channel-empty">当前没有已登记 WebGL 发布包，项目不会进入游玩路径。</p>
          ) : null}
          <ProjectGallery
            variant="game"
            projects={gameProjects.map((game) => ({
              id: game.id,
              title: game.title,
              date: game.capturedOn,
              place: game.place,
              summary: game.lead,
              images: game.screenshots,
            }))}
          />
        </div>
      ) : (
        <KindDoorRail doors={doors} />
      )}
    </div>
  );
}

/**
 * 细目门：3 秒换一格，本门近 5 张不重复，换张 0.9s 淡入。悬停只跳过该门。
 */
function KindDoorRail({ doors }: { doors: DevelopChannelDoor[] }) {
  const reduced = usePrefersReducedMotion();
  const { advance, bind } = useComboRelay(doors.length, comboRelay3s, reduced, (index) => {
    const door = doors[index];
    return Boolean(door && !door.comingSoon && (door.srcs?.length ?? 0) >= 2);
  });

  return (
    <div className="develop-kind-doors" data-count={doors.length}>
      {doors.map((door, index) => (
        <CyclingKindDoor key={door.channel} door={door} advance={advance[index] ?? 0} {...bind(index)} />
      ))}
    </div>
  );
}

function CyclingKindDoor({
  door,
  advance,
  onPointerEnter,
  onPointerLeave,
}: {
  door: DevelopChannelDoor;
  advance: number;
  onPointerEnter: (event: PointerEvent<HTMLElement>) => void;
  onPointerLeave: () => void;
}) {
  const srcs =
    door.srcs && door.srcs.length > 0
      ? door.srcs
      : uniqueDraws([fixedDraw(stockPlaceholderSrc(door.channel, 1))]);
  const { shown, stacked, fail } = useComboShown(srcs, advance, comboRelay3s.noRepeat);
  const copy = (
    <span className="develop-work-copy">
      <strong>{door.zh}</strong>
      <em>{door.deco}</em>
      {door.comingSoon ? <span>待收录</span> : null}
    </span>
  );
  const media =
    stacked.length > 0 ? (
      stacked.map((src) => (
        <img
          key={src}
          className={src === shown ? "is-on" : undefined}
          src={assetUrl(src)}
          alt=""
          onError={() => fail(src)}
        />
      ))
    ) : (
      <span className={`develop-work-well is-channel-${door.channel}`} aria-hidden="true" />
    );
  const className = `develop-kind-door is-cycling${door.comingSoon ? " is-soon" : ""}`;

  if (door.comingSoon || !door.href) {
    return (
      <div className={className} onPointerEnter={onPointerEnter} onPointerLeave={onPointerLeave}>
        {media}
        {copy}
      </div>
    );
  }

  return (
    <Link className={className} to={door.href} onPointerEnter={onPointerEnter} onPointerLeave={onPointerLeave}>
      {media}
      {copy}
    </Link>
  );
}
