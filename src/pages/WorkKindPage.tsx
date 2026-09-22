import { Link, useParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { CoverTile } from "../components/work/CoverTile";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { lexicon } from "../content/lexicon";
import { categories, gameProjects, playableGames } from "../content/site";
import { stockPlaceholderSrc } from "../content/stockMedia";
import {
  isDevelopWorkKind,
  kindEn,
  listDevelopChannelDoors,
  workIndexRoot,
} from "../ia/workTree";
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
  const doorCount = doors.length;

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
        <div className="develop-kind-doors" data-count={doorCount}>
          {doors.map((door) => (
            <CoverTile
              key={door.channel}
              className="develop-kind-door"
              wellClass={`develop-work-well is-channel-${door.channel}`}
              to={door.comingSoon ? undefined : door.href}
              src={door.coverSrc}
              fallbackSrc={
                door.channel === lexicon.gameMenu.key
                  ? stockPlaceholderSrc(lexicon.gameDev.key, 1)
                  : stockPlaceholderSrc(door.channel, 1)
              }
              disabled={door.comingSoon}
            >
              <strong>{door.zh}</strong>
              <em>{door.deco}</em>
              {door.comingSoon ? <span>待收录</span> : null}
            </CoverTile>
          ))}
        </div>
      )}
    </div>
  );
}
