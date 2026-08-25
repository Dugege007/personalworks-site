import { Link } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import "../styles/placeholder.css";

export function GamesHubPage() {
  const category = categories.find((item) => item.id === lexicon.gameDev.key);

  return (
    <div className="page" data-theme={lexicon.gameDev.key}>
      <div className="page-kicker">
        {category?.index} / {category?.indexEn}
      </div>
      <h1>{category?.title ?? "游戏开发"}</h1>
      <p className="page-lead">
        可玩的展示向小游戏。本页不加载任何 WebGL。选择游戏后，再进入对应游玩页。
      </p>
      <Link className="archive-btn" to={`/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`}>
        打开游戏选单
        <span aria-hidden="true">→</span>
      </Link>
      <p className="note">不登录、不付费。存档只留在访客浏览器里。</p>
    </div>
  );
}
