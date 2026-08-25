import { Navigate, Route, Routes, useParams } from "react-router-dom";
import { SiteShell } from "./layouts/SiteShell";
import { AboutPage } from "./pages/AboutPage";
import { CategoryPage } from "./pages/CategoryPage";
import { GamePlayPlaceholderPage } from "./pages/GamePlayPlaceholderPage";
import { GamesHubPage } from "./pages/GamesHubPage";
import { GamesMenuPage } from "./pages/GamesMenuPage";
import { HomePage } from "./pages/HomePage";
import { NoteDetailPage } from "./pages/NoteDetailPage";
import { NotesPage } from "./pages/NotesPage";
import { WorkDetailPage } from "./pages/WorkDetailPage";

type RedirectIdProps = {
  to: (id: string) => string;
};

function RedirectId({ to }: RedirectIdProps) {
  const { id } = useParams();
  return <Navigate to={to(id ?? "")} replace />;
}

export function App() {
  return (
    <Routes>
      <Route element={<SiteShell />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/twin-sim" element={<CategoryPage categoryId="twin-sim" />} />
        <Route path="/twin-sim/twin/:id" element={<WorkDetailPage />} />
        <Route path="/twin-sim/factory/:id" element={<WorkDetailPage />} />
        <Route path="/landscape" element={<CategoryPage categoryId="landscape" />} />
        <Route path="/landscape/render/:id" element={<WorkDetailPage />} />
        <Route path="/landscape/construction/:id" element={<WorkDetailPage />} />
        <Route path="/photo" element={<CategoryPage categoryId="photo" />} />
        <Route path="/photo/landscape/:id" element={<WorkDetailPage />} />
        <Route path="/photo/portrait/:id" element={<WorkDetailPage />} />
        <Route path="/games" element={<GamesHubPage />} />
        <Route path="/games/menu" element={<GamesMenuPage />} />
        <Route path="/games/:id" element={<GamePlayPlaceholderPage />} />
        <Route path="/notes" element={<NotesPage />} />
        <Route path="/notes/:slug" element={<NoteDetailPage />} />
        <Route path="/about" element={<AboutPage />} />
        <Route path="/digital-twin" element={<Navigate to="/twin-sim" replace />} />
        <Route path="/digital-twin/:id" element={<RedirectId to={(id) => `/twin-sim/twin/${id}`} />} />
        <Route path="/factory-sim" element={<Navigate to="/twin-sim" replace />} />
        <Route path="/factory-sim/:id" element={<RedirectId to={(id) => `/twin-sim/factory/${id}`} />} />
        <Route path="/landscape-render" element={<Navigate to="/landscape" replace />} />
        <Route path="/landscape-render/:id" element={<RedirectId to={(id) => `/landscape/render/${id}`} />} />
        <Route path="/construction" element={<Navigate to="/landscape" replace />} />
        <Route path="/construction/:id" element={<RedirectId to={(id) => `/landscape/construction/${id}`} />} />
        <Route path="/landscape-photo" element={<Navigate to="/photo" replace />} />
        <Route path="/landscape-photo/:id" element={<RedirectId to={(id) => `/photo/landscape/${id}`} />} />
        <Route path="/portrait" element={<Navigate to="/photo" replace />} />
        <Route path="/portrait/:id" element={<RedirectId to={(id) => `/photo/portrait/${id}`} />} />
        <Route path="/sketch" element={<Navigate to="/notes/sketches" replace />} />
        <Route path="/sketch/:id" element={<Navigate to="/notes/sketches" replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
