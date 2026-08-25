import { Link } from "react-router-dom";
import { placeholderGames } from "../content/site";
import "../styles/placeholder.css";

export function GamesMenuPage() {
  return (
    <div className="page" data-theme="games">
      <Link className="back" to="/games">
        ← 返回游戏开发
      </Link>
      <div className="page-kicker">03.1 / MENU</div>
      <h1>游戏选单</h1>
      <p className="page-lead">点击卡片进入游玩占位页。真实构建包将放到 COS，进入后再加载。</p>
      <div className="card-grid">
        {placeholderGames.map((game) => (
          <Link className="card" key={game.id} to={`/games/${game.id}`}>
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
