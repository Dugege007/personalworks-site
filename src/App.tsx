import { Navigate, Route, Routes } from "react-router-dom";
import { SiteShell } from "./layouts/SiteShell";
import { ArchivePlaceholderPage } from "./pages/ArchivePlaceholderPage";
import { GamePlayPlaceholderPage } from "./pages/GamePlayPlaceholderPage";
import { GamesHubPage } from "./pages/GamesHubPage";
import { GamesMenuPage } from "./pages/GamesMenuPage";
import { HomePage } from "./pages/HomePage";
import { NoteDetailPage } from "./pages/NoteDetailPage";
import { NotesPage } from "./pages/NotesPage";
import { WorkDetailPlaceholderPage } from "./pages/WorkDetailPlaceholderPage";

const archiveRoutes = [
  "/digital-twin",
  "/factory-sim",
  "/landscape-render",
  "/construction",
  "/landscape-photo",
  "/portrait",
  "/sketch",
] as const;

export function App() {
  return (
    <Routes>
      <Route element={<SiteShell />}>
        <Route path="/" element={<HomePage />} />
        {archiveRoutes.map((path) => (
          <Route key={path} path={path} element={<ArchivePlaceholderPage />} />
        ))}
        {archiveRoutes.map((path) => (
          <Route
            key={`${path}-id`}
            path={`${path}/:id`}
            element={<WorkDetailPlaceholderPage listPath={path} />}
          />
        ))}
        <Route path="/games" element={<GamesHubPage />} />
        <Route path="/games/menu" element={<GamesMenuPage />} />
        <Route path="/games/:id" element={<GamePlayPlaceholderPage />} />
        <Route path="/notes" element={<NotesPage />} />
        <Route path="/notes/:slug" element={<NoteDetailPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
