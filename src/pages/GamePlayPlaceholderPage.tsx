import { Link, useParams } from "react-router-dom";
import { placeholderGames } from "../content/site";
import "../styles/placeholder.css";

export function GamePlayPlaceholderPage() {
  const { id } = useParams();
  const game = placeholderGames.find((item) => item.id === id);

  if (!game) {
    return (
      <div className="page" data-theme="games">
        <Link className="back" to="/games/menu">
          ← 返回选单
        </Link>
        <h1>该游戏未开放</h1>
        <p className="page-lead">编号 `{id}` 不在当前选单中。</p>
      </div>
    );
  }

  return (
    <div className="page" data-theme="games" style={{ ["--game-accent" as string]: game.accent }}>
      <Link className="back" to="/games/menu">
        ← 返回选单
      </Link>
      <div className="page-kicker">03.2 / PLAY</div>
      <h1>{game.title}</h1>
      <p className="page-lead">{game.lead}</p>
      <div className="play-stage">
        <div>
          <div className="accent-bar" />
          <p>WebGL 画布占位 · {game.titleEn}</p>
          <p>操作：{game.input}</p>
          <p>{game.sizeHint}</p>
          <p>存档：{game.saveMode}</p>
        </div>
      </div>
      <p className="note">
        存档将写入浏览器 IndexedDB（Unity PlayerPrefs）。清除站点数据即丢失。本页第一批不加载真实构建。
      </p>
    </div>
  );
}
