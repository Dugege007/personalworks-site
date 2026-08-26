import { Outlet, useLocation } from "react-router-dom";
import { resolveShellTheme } from "../content/site";
import { useLenis } from "../hooks/useLenis";
import { SiteHeader } from "./SiteHeader";
import "../styles/layout.css";
import "../styles/stage.css";

export function SiteShell() {
  useLenis();
  const location = useLocation();
  const theme = resolveShellTheme(location.pathname);

  return (
    <div className="shell" data-theme={theme}>
      <div className="grain" aria-hidden="true" />
      <div className="stage" aria-hidden="true" />
      <SiteHeader />
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}
