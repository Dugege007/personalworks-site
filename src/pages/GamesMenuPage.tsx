import { Link, Navigate } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { archiveSubIndex, placeholderGames } from "../content/site";
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
      <p className="page-lead">点击卡片进入游玩占位页。真实构建包将放到 COS，进入后再加载。</p>
      <div className="card-grid">
        {placeholderGames.map((game, index) => (
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
              <p>{game.sizeHint}</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
