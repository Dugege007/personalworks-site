import { Link, Navigate, useParams } from "react-router-dom";
import { CoverTile } from "../components/work/CoverTile";
import { lexicon } from "../content/lexicon";
import { iaOfSkin } from "../ia";
import { hrefForWork, channelTitleZh } from "../ia/href";
import {
  hrefForDevelopKind,
  isChannelOfKind,
  isDevelopWorkKind,
  kindTitle,
  listDevelopChannelDoors,
} from "../ia/workTree";
import { listPublishedWorks } from "../content/works";
import { stockPlaceholderSrc, workCoverSrc } from "../content/stockMedia";
import { findCollectionByChannel, placeholderGames } from "../content/site";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/develop-work.css";

/**
 * 显影作品第三层：细目条目页。游戏选单列出可玩项。
 */
export function WorkChannelPage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const { kind, channel } = useParams();

  if (!kind || !isDevelopWorkKind(kind) || !isChannelOfKind(kind, channel)) {
    return <Navigate to={kind && isDevelopWorkKind(kind) ? hrefForDevelopKind(kind) : `/${lexicon.workIndex.key}`} replace />;
  }

  const channelKey = channel ?? "";
  const door = listDevelopChannelDoors(kind).find((item) => item.channel === channelKey);
  const isGameMenu = kind === lexicon.gameDev.key && channelKey === lexicon.gameMenu.key;

  if (door?.comingSoon) {
    return (
      <div className="develop-channel" data-theme={channelKey}>
        <Link className="develop-work-back" to={hrefForDevelopKind(kind)}>
          {kindTitle(kind)}
        </Link>
        <p className="develop-kind-en">{door.deco}</p>
        <h1>{door.zh}</h1>
        <p className="develop-kind-lead">位置已留，作品待收录。</p>
        <div className="develop-channel-grid" data-count={3}>
          {(findCollectionByChannel(channelKey)?.frames ?? ["场景", "角色", "光影"]).map((label, index) => (
            <CoverTile
              key={label}
              className={`develop-channel-cell is-${index + 1}`}
              wellClass={`develop-work-well is-channel-${channelKey}`}
              src={stockPlaceholderSrc(channelKey, index + 1)}
              disabled
            >
              <strong>{label}</strong>
              <em>待收录</em>
            </CoverTile>
          ))}
        </div>
      </div>
    );
  }

  if (isGameMenu) {
    return (
      <div className="develop-channel" data-theme={lexicon.gameMenu.key}>
        <Link className="develop-work-back" to={hrefForDevelopKind(kind)}>
          {kindTitle(kind)}
        </Link>
        <p className="develop-kind-en">{lexicon.gameMenu.en}</p>
        <h1>{lexicon.gameMenu.zh}</h1>
        <p className="develop-kind-lead">点击进入游玩页后再加载。不登录、不付费。</p>
        <div className="develop-channel-grid" data-count={placeholderGames.length}>
          {placeholderGames.map((game, index) => (
            <CoverTile
              key={game.id}
              className={`develop-channel-cell is-${index + 1}`}
              wellClass={`develop-work-well is-game is-${index + 1}`}
              to={`/${lexicon.gameDev.key}/${game.id}`}
              src={game.coverSrc}
              fallbackSrc={stockPlaceholderSrc(lexicon.gameDev.key, index + 1)}
            >
              <strong>{game.title}</strong>
              <em>{game.titleEn}</em>
              <span>{game.lead}</span>
            </CoverTile>
          ))}
        </div>
      </div>
    );
  }

  const works = listPublishedWorks(channelKey);

  return (
    <div className="develop-channel" data-theme={channelKey}>
      <Link className="develop-work-back" to={hrefForDevelopKind(kind)}>
        {kindTitle(kind)}
      </Link>
      <p className="develop-kind-en">{door?.deco ?? channelTitleZh(channelKey)}</p>
      <h1>{door?.zh ?? channelTitleZh(channelKey)}</h1>
      {door?.lead ? <p className="develop-kind-lead">{door.lead}</p> : null}
      {works.length === 0 ? (
        <p className="develop-channel-empty">这一细目还没有可展示的作品。</p>
      ) : (
        <div className="develop-channel-grid" data-count={works.length}>
          {works.map((work, index) => {
            const src = workCoverSrc(work, index);
            return (
              <CoverTile
                key={`${work.channel}-${work.id}`}
                className={`develop-channel-cell is-${index + 1}`}
                wellClass={`develop-work-well is-channel-${work.channel}`}
                to={hrefForWork(work, ia.id)}
                src={src}
                fallbackSrc={stockPlaceholderSrc(work.channel, index + 1)}
              >
                <strong>{work.title}</strong>
                <em>{work.year}</em>
              </CoverTile>
            );
          })}
        </div>
      )}
    </div>
  );
}
