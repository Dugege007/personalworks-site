import { useLayoutEffect } from "react";
import { Outlet, useLocation } from "react-router-dom";
import { resolveShellTheme } from "../content/site";
import { scrollPageToTop, unlockPageForLightbox, useLenis } from "../hooks/useLenis";
import { SiteHeader } from "./SiteHeader";
import "../styles/layout.css";
import "../styles/stage.css";

export function SiteShell() {
  useLenis();
  const location = useLocation();
  const theme = resolveShellTheme(location.pathname);

  useLayoutEffect(() => {
    // 灯箱内的链接会直接换页。先解开滚动锁再滚顶，避免锁还在时复位被吃掉。
    unlockPageForLightbox();
    scrollPageToTop();
  }, [location.pathname]);

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
