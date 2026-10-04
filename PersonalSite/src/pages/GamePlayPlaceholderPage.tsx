import { Link, useParams } from "react-router-dom";
import { resolveLead } from "../content/copyDisplay";
import { TermText } from "../components/TermText";
import { lexicon } from "../content/lexicon";
import { archiveSubIndex, playableGames } from "../content/site";
import { iaOfSkin } from "../ia";
import { hrefForDevelopChannel } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/placeholder.css";

export function GamePlayPlaceholderPage() {
  const { id } = useParams();
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const game = playableGames.find((item) => item.id === id);
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
        <p className="page-lead">编号 `{id}` 没有已登记的 WebGL 发布包。</p>
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
      {resolveLead(game.lead) ? (
        <p className="page-lead">
          <TermText text={resolveLead(game.lead)!} />
        </p>
      ) : null}
      <div className="play-stage">
        {game.coverSrc ? <img className="play-stage-cover" src={assetUrl(game.coverSrc)} alt="" /> : null}
        <div>
          <div className="accent-bar" />
          <p>WebGL 画布占位 · {game.titleEn}</p>
          {game.input ? <p>操作：{game.input}</p> : null}
          {game.sizeHint ? <p>{game.sizeHint}</p> : null}
          {game.saveMode ? <p>存档：{game.saveMode}</p> : null}
        </div>
      </div>
      <p className="note">
        游戏运行时仅在当前页面加载；返回选单后不继续保留多个运行实例。
      </p>
    </div>
  );
}
