import { Link } from "react-router-dom";
import "../styles/placeholder.css";

export function GamesHubPage() {
  return (
    <div className="page" data-theme="games">
      <div className="page-kicker">03 / GAME DEV</div>
      <h1>游戏开发</h1>
      <p className="page-lead">
        可玩的展示向小游戏。本页不加载任何 WebGL。选择游戏后，再进入对应游玩页。
      </p>
      <Link className="archive-btn" to="/games/menu">
        打开游戏选单
        <span aria-hidden="true">→</span>
      </Link>
      <p className="note">不登录、不付费。存档只留在访客浏览器里。</p>
    </div>
  );
}
