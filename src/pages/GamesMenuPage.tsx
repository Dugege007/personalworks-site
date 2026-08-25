import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { placeholderGames } from "../content/site";
import "../styles/placeholder.css";

export function GamesMenuPage() {
  return (
    <div className="page" data-theme={lexicon.gameMenu.key}>
      <Link className="back" to={`/${lexicon.gameDev.key}`}>
        ← 返回{lexicon.gameDev.zh}
      </Link>
      <div className="page-kicker">03.1 / {lexicon.gameMenu.deco}</div>
      <h1>{lexicon.gameMenu.zh}</h1>
      <p className="page-lead">点击卡片进入游玩占位页。真实构建包将放到 COS，进入后再加载。</p>
      <div className="card-grid">
        {placeholderGames.map((game) => (
          <Link className="card" key={game.id} to={`/${lexicon.gameDev.key}/${game.id}`}>
            <small>{game.titleEn}</small>
            <div>
              <h2>{game.title}</h2>
              <p>{game.lead}</p>
            </div>
            <p>{game.sizeHint}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
