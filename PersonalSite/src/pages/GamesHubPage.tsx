import { Link, Navigate } from "react-router-dom";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { lexicon } from "../content/lexicon";
import { categories, gameProjects, playableGames } from "../content/site";
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
        按项目浏览真实截图。本页不加载 WebGL，只有已有发布包的项目才进入游戏选单。
      </p>
      {playableGames.length > 0 ? (
        <Link className="archive-btn" to={`/${lexicon.gameDev.key}/${lexicon.gameMenu.key}`}>
          打开游戏选单
          <span aria-hidden="true">→</span>
        </Link>
      ) : (
        <p className="note">当前没有已登记 WebGL 发布包，项目不会进入游玩路径。</p>
      )}
      <ProjectGallery
        variant="game"
        projects={gameProjects.map((game) => ({
          id: game.id,
          title: game.title,
          date: game.capturedOn,
          place: game.place,
          summary: game.lead,
          images: game.screenshots,
        }))}
      />
    </div>
  );
}
