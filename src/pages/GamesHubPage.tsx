import { Link, Navigate } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { categories } from "../content/site";
import { iaOfSkin } from "../ia";
import { hrefForDevelopKind } from "../ia/workTree";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";

export function GamesHubPage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const category = categories.find((item) => item.id === lexicon.gameDev.key);

  if (ia.id === "develop-editorial") {
    return <Navigate to={hrefForDevelopKind(lexicon.gameDev.key)} replace />;
  }

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
