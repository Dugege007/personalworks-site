import { Navigate, Route, Routes, useParams } from "react-router-dom";
import { lexicon } from "./content/lexicon";
import { photoWorkChannels } from "./content/works";
import { hrefForPhotoCatalog } from "./ia/workTree";
import { SiteShell } from "./layouts/SiteShell";
import { AboutPage } from "./pages/AboutPage";
import { CategoryPage } from "./pages/CategoryPage";
import { GamePlayPlaceholderPage } from "./pages/GamePlayPlaceholderPage";
import { GamesHubPage } from "./pages/GamesHubPage";
import { GamesMenuPage } from "./pages/GamesMenuPage";
import { HomePage } from "./pages/HomePage";
import { NoteDetailPage } from "./pages/NoteDetailPage";
import { NotesPage } from "./pages/NotesPage";
import { PhotoCatalogPage } from "./pages/PhotoCatalogPage";
import { PhotoShootsPage } from "./pages/PhotoShootsPage";
import { ResumePage } from "./pages/ResumePage";
import { SkillsPage } from "./pages/SkillsPage";
import { WorkChannelPage } from "./pages/WorkChannelPage";
import { WorkDetailPage } from "./pages/WorkDetailPage";
import { WorkIndexPage } from "./pages/WorkIndexPage";
import { WorkIndexSegmentPage } from "./pages/WorkKindPage";

type RedirectIdProps = {
  to: (id: string) => string;
};

function RedirectId({ to }: RedirectIdProps) {
  const { id } = useParams();
  return <Navigate to={to(id ?? "")} replace />;
}

/**
 * 旧摄影细目列表直达带筛选的总览，避免先闪条目墙。
 */
function RedirectPhotoIndexChannel() {
  const { channel } = useParams();
  if (channel && (photoWorkChannels as readonly string[]).includes(channel)) {
    return <Navigate to={hrefForPhotoCatalog(channel)} replace />;
  }
  return <WorkChannelPage />;
}

const twinSim = `/${lexicon.twinAndSim.key}`;
const digitalTwin = `${twinSim}/${lexicon.digitalTwin.key}`;
const lineSim = `${twinSim}/${lexicon.lineSimulation.key}`;
const landscapeArch = `/${lexicon.landscapeArch.key}`;
const landscapeRendering = `${landscapeArch}/${lexicon.landscapeRendering.key}`;
const landscapeCDs = `${landscapeArch}/${lexicon.landscapeCDs.key}`;
const photo = `/${lexicon.photography.key}`;
const landscapePhoto = `${photo}/${lexicon.landscapePhoto.key}`;
const humanistPhoto = `${photo}/${lexicon.humanistPhoto.key}`;
const portraitPhoto = `${photo}/${lexicon.portraitPhoto.key}`;
const gameDev = `/${lexicon.gameDev.key}`;
const gameMenu = `${gameDev}/${lexicon.gameMenu.key}`;

export function App() {
  return (
    <Routes>
      <Route element={<SiteShell />}>
        <Route path="/" element={<HomePage />} />
        <Route path={`/${lexicon.workIndex.key}`} element={<WorkIndexPage />} />
        <Route path={`/${lexicon.workIndex.key}/:kind/:channel/:id`} element={<WorkDetailPage />} />
        <Route
          path={`/${lexicon.workIndex.key}/${lexicon.photography.key}/:channel`}
          element={<RedirectPhotoIndexChannel />}
        />
        <Route path={`/${lexicon.workIndex.key}/:kind/:channel`} element={<WorkChannelPage />} />
        <Route path={`/${lexicon.workIndex.key}/:kind`} element={<WorkIndexSegmentPage />} />
        <Route path={`/${lexicon.profileResume.key}`} element={<ResumePage />} />
        <Route path={`/${lexicon.profileSkills.key}`} element={<SkillsPage />} />
        <Route path={twinSim} element={<CategoryPage categoryId={lexicon.twinAndSim.key} />} />
        <Route path={`${digitalTwin}/:id`} element={<WorkDetailPage />} />
        <Route path={`${lineSim}/:id`} element={<WorkDetailPage />} />
        <Route path={landscapeArch} element={<CategoryPage categoryId={lexicon.landscapeArch.key} />} />
        <Route path={`${landscapeRendering}/:id`} element={<WorkDetailPage />} />
        <Route path={`${landscapeCDs}/:id`} element={<WorkDetailPage />} />
        <Route path={photo} element={<CategoryPage categoryId={lexicon.photography.key} />} />
        <Route path={`${photo}/${lexicon.photoCatalog.key}`} element={<PhotoCatalogPage />} />
        <Route path={`${photo}/${lexicon.photoShoots.key}`} element={<PhotoShootsPage />} />
        <Route path={`${landscapePhoto}/:id`} element={<WorkDetailPage />} />
        <Route path={`${humanistPhoto}/:id`} element={<WorkDetailPage />} />
        <Route path={`${portraitPhoto}/:id`} element={<WorkDetailPage />} />
        <Route path={gameDev} element={<GamesHubPage />} />
        <Route path={gameMenu} element={<GamesMenuPage />} />
        <Route path={`${gameDev}/:id`} element={<GamePlayPlaceholderPage />} />
        <Route path={`/${lexicon.notes.key}`} element={<NotesPage />} />
        <Route path={`/${lexicon.notes.key}/:slug`} element={<NoteDetailPage />} />
        <Route path={`/${lexicon.about.key}`} element={<AboutPage />} />
        <Route path="/twin-sim/twin/:id" element={<RedirectId to={(id) => `${digitalTwin}/${id}`} />} />
        <Route path="/twin-sim/factory/:id" element={<RedirectId to={(id) => `${lineSim}/${id}`} />} />
        <Route path="/digital-twin" element={<Navigate to={twinSim} replace />} />
        <Route path="/digital-twin/:id" element={<RedirectId to={(id) => `${digitalTwin}/${id}`} />} />
        <Route path="/factory-sim" element={<Navigate to={twinSim} replace />} />
        <Route path="/factory-sim/:id" element={<RedirectId to={(id) => `${lineSim}/${id}`} />} />
        <Route path="/landscape" element={<Navigate to={landscapeArch} replace />} />
        <Route path="/landscape/render/:id" element={<RedirectId to={(id) => `${landscapeRendering}/${id}`} />} />
        <Route path="/landscape/construction/:id" element={<RedirectId to={(id) => `${landscapeCDs}/${id}`} />} />
        <Route path="/landscape-render" element={<Navigate to={landscapeArch} replace />} />
        <Route path="/landscape-render/:id" element={<RedirectId to={(id) => `${landscapeRendering}/${id}`} />} />
        <Route path="/construction" element={<Navigate to={landscapeArch} replace />} />
        <Route path="/construction/:id" element={<RedirectId to={(id) => `${landscapeCDs}/${id}`} />} />
        <Route path="/photo/landscape/:id" element={<RedirectId to={(id) => `${landscapePhoto}/${id}`} />} />
        <Route path="/photo/portrait/:id" element={<RedirectId to={(id) => `${portraitPhoto}/${id}`} />} />
        <Route path="/landscape-photo" element={<Navigate to={photo} replace />} />
        <Route path="/landscape-photo/:id" element={<RedirectId to={(id) => `${landscapePhoto}/${id}`} />} />
        <Route path="/portrait" element={<Navigate to={photo} replace />} />
        <Route path="/portrait/:id" element={<RedirectId to={(id) => `${portraitPhoto}/${id}`} />} />
        <Route path="/games" element={<Navigate to={gameDev} replace />} />
        <Route path="/games/menu" element={<Navigate to={gameMenu} replace />} />
        <Route path="/games/:id" element={<RedirectId to={(id) => `${gameDev}/${id}`} />} />
        <Route path="/sketch" element={<Navigate to={`/${lexicon.notes.key}/${lexicon.sketching.key}`} replace />} />
        <Route path="/sketch/:id" element={<Navigate to={`/${lexicon.notes.key}/${lexicon.sketching.key}`} replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
