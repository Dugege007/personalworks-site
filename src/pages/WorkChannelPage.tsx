import { Navigate, useParams } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { CoverTile } from "../components/work/CoverTile";
import { lexicon } from "../content/lexicon";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { iaOfSkin } from "../ia";
import { hrefForWork, channelTitleZh } from "../ia/href";
import {
  hrefForDevelopKind,
  isChannelOfKind,
  isDevelopWorkKind,
  kindTitle,
  listDevelopChannelDoors,
} from "../ia/workTree";
import { listPublishedWorks, listWorkImages } from "../content/works";
import { stockPlaceholderSrc, workCoverSrc } from "../content/stockMedia";
import { resolveLead } from "../content/copyDisplay";
import { findCollectionByChannel, playableGames } from "../content/site";
import { LandscapeProjectBoard } from "../components/work/LandscapeProjectBoard";
import { tx } from "../prefs/tx";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/develop-work.css";

/**
 * 显影作品第三层：细目条目页。游戏选单列出可玩项。
 */
export function WorkChannelPage() {
  const { currentSkin, locale } = usePrefs();
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
        <ChannelHead
          backTo={hrefForDevelopKind(kind)}
          backLabel={kindTitle(kind)}
          title={tx({ "zh-CN": door.zh, en: door.en }, locale)}
          lead="位置已留，作品待收录。"
        />
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
        <ChannelHead
          backTo={hrefForDevelopKind(kind)}
          backLabel={kindTitle(kind)}
          title={tx({ "zh-CN": lexicon.gameMenu.zh, en: lexicon.gameMenu.en }, locale)}
          lead="点击进入游玩页后再加载。不登录、不付费。"
        />
        {playableGames.length === 0 ? (
          <p className="develop-channel-empty">当前没有已登记 WebGL 发布包的项目。</p>
        ) : (
        <div className="develop-channel-grid" data-count={playableGames.length}>
          {playableGames.map((game, index) => (
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
              {resolveLead(game.lead) ? <span>{resolveLead(game.lead)}</span> : null}
            </CoverTile>
          ))}
        </div>
        )}
      </div>
    );
  }

  const works = listPublishedWorks(channelKey);
  const isLandscapeBoard = channelKey === lexicon.landscapeRendering.key;
  const galleryVariant =
    channelKey === lexicon.digitalTwin.key
      ? "twin"
      : channelKey === lexicon.landscapePhoto.key ||
          channelKey === lexicon.humanistPhoto.key ||
          channelKey === lexicon.portraitPhoto.key
        ? "photo"
        : undefined;

  return (
    <div
      className={`develop-channel${isLandscapeBoard ? " is-landscape-board" : ""}`}
      data-theme={channelKey}
    >
      <ChannelHead
        backTo={hrefForDevelopKind(kind)}
        backLabel={kindTitle(kind)}
        title={tx({ "zh-CN": door?.zh ?? channelTitleZh(channelKey), en: door?.en }, locale)}
        lead={door?.lead}
      />
      {isLandscapeBoard && works.length === 0 ? (
        <p className="develop-channel-empty">这一细目还没有可展示的作品。</p>
      ) : null}
      {isLandscapeBoard && works.length > 0 ? <LandscapeProjectBoard works={works} /> : null}
      {galleryVariant && works.length > 0 ? (
        <div className="develop-project-gallery">
          <ProjectGallery
            variant={galleryVariant}
            projects={works.map((work) => ({
              id: work.id,
              title: work.title,
              date: work.capturedOn ?? work.startedOn ?? work.year,
              place: work.place,
              summary: work.summary,
              href: hrefForWork(work, ia.id),
              images: listWorkImages(work).flatMap((image) =>
                image.src ? [{ src: image.src, label: image.label }] : [],
              ),
            }))}
          />
        </div>
      ) : null}
      {isLandscapeBoard || galleryVariant ? null : works.length === 0 ? (
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
