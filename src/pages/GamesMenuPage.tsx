import { Link, Navigate } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { archiveSubIndex, playableGames } from "../content/site";
import { stockPlaceholderSrc } from "../content/stockMedia";
import { iaOfSkin } from "../ia";
import { hrefForDevelopChannel } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";

export function GamesMenuPage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);

  if (ia.id === "develop-editorial") {
    return <Navigate to={hrefForDevelopChannel(lexicon.gameDev.key, lexicon.gameMenu.key)} replace />;
  }
  return (
    <div className="page" data-theme={lexicon.gameMenu.key}>
      <Link className="back" to={`/${lexicon.gameDev.key}`}>
        ← 返回{lexicon.gameDev.zh}
      </Link>
      <div className="page-kicker">
        {archiveSubIndex(lexicon.gameDev.key, 1)} / {lexicon.gameMenu.deco}
      </div>
      <h1>{lexicon.gameMenu.zh}</h1>
      <p className="page-lead">这里只列出已登记 WebGL 发布包的项目，进入项目后才加载运行时。</p>
      {playableGames.length === 0 ? (
        <p className="note">当前没有可玩的项目。截图项目仍可在游戏开发页浏览。</p>
      ) : (
        <div className="card-grid">
          {playableGames.map((game, index) => (
            <Link className="card" key={game.id} to={`/${lexicon.gameDev.key}/${game.id}`}>
              <figure className="card-cover">
                <img src={assetUrl(game.coverSrc || stockPlaceholderSrc(lexicon.gameDev.key, index + 1))} alt="" />
              </figure>
              <div className="card-body">
                <small>{game.titleEn}</small>
                <div>
                  <h2>{game.title}</h2>
                  <p>{game.lead}</p>
                </div>
                {game.sizeHint ? <p>{game.sizeHint}</p> : null}
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
