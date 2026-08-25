import { Outlet, useLocation } from "react-router-dom";
import { findNavByPath } from "../content/site";
import { useLenis } from "../hooks/useLenis";
import { SiteHeader } from "./SiteHeader";
import "../styles/layout.css";

export function SiteShell() {
  useLenis();
  const location = useLocation();
  const current = findNavByPath(location.pathname);

  return (
    <div className="shell" data-theme={current.theme}>
      <div className="grain" aria-hidden="true" />
      <SiteHeader />
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}
