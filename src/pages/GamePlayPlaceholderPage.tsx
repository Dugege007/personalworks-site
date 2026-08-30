import { Link, useParams } from "react-router-dom";
import { lexicon } from "../content/lexicon";
import { archiveSubIndex, placeholderGames } from "../content/site";
import { iaOfSkin } from "../ia";
import { hrefForDevelopChannel } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";

export function GamePlayPlaceholderPage() {
  const { id } = useParams();
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const game = placeholderGames.find((item) => item.id === id);
  const menuPath =
    ia.id === "develop-editorial"
      ? hrefForDevelopChannel(lexicon.gameDev.key, lexicon.gameMenu.key)
      : `/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`;

  if (!game) {
    return (
      <div className="page" data-theme={lexicon.gamePlay.key}>
        <Link className="back" to={menuPath}>
          ← 返回选单
        </Link>
        <h1>该游戏未开放</h1>
        <p className="page-lead">编号 `{id}` 不在当前选单中。</p>
      </div>
    );
  }

  return (
    <div className="page" data-theme={lexicon.gamePlay.key} style={{ ["--game-accent" as string]: game.accent }}>
      <Link className="back" to={menuPath}>
        ← 返回选单
      </Link>
      <div className="page-kicker">
        {archiveSubIndex(lexicon.gameDev.key, 2)} / {lexicon.gamePlay.deco}
      </div>
      <h1>{game.title}</h1>
      <p className="page-lead">{game.lead}</p>
      <div className="play-stage">
        {game.coverSrc ? <img className="play-stage-cover" src={assetUrl(game.coverSrc)} alt="" /> : null}
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
