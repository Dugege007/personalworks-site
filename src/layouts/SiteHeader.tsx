import { useEffect, useState } from "react";
import { NavLink, Link, useLocation } from "react-router-dom";
import { iaOfSkin } from "../ia";
import { usePrefs } from "../prefs/PrefsProvider";
import { SkinPicker } from "./SkinPicker";

type SiteHeaderProps = {
  onNavigate?: () => void;
};

/**
 * 顶栏标识与抽屉均读当前 IA，不按皮肤 id 散落分支。
 */
export function SiteHeader({ onNavigate }: SiteHeaderProps) {
  const { currentSkin } = usePrefs();
  const location = useLocation();
  const ia = iaOfSkin(currentSkin);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [skinOpen, setSkinOpen] = useState(false);
  const [overHero, setOverHero] = useState(false);
  const [heroDark, setHeroDark] = useState(false);

  const closeAll = () => {
    setDrawerOpen(false);
    setSkinOpen(false);
  };

  const toggleDrawer = () => {
    setDrawerOpen((value) => {
      if (!value) {
        setSkinOpen(false);
      }
      return !value;
    });
  };

  const toggleSkin = () => {
    setSkinOpen((value) => {
      if (!value) {
        setDrawerOpen(false);
      }
      return !value;
    });
  };

  useEffect(() => {
    if (!skinOpen && !drawerOpen) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeAll();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [skinOpen, drawerOpen]);

  useEffect(() => {
    let io: IntersectionObserver | undefined;
    let mutation: MutationObserver | undefined;
    let raf = 0;
    let tries = 0;

    const detach = () => {
      io?.disconnect();
      mutation?.disconnect();
      io = undefined;
      mutation = undefined;
    };

    const attach = () => {
      const hero = document.getElementById("hero-bleed");
      if (!hero) {
        setOverHero(false);
        setHeroDark(false);
        return false;
      }
      const syncTone = () => setHeroDark(hero.dataset.tone === "dark");
      syncTone();
      io = new IntersectionObserver(
        ([entry]) => {
          setOverHero(Boolean(entry?.isIntersecting));
          syncTone();
        },
        { threshold: 0.28 },
      );
      io.observe(hero);
      mutation = new MutationObserver(syncTone);
      mutation.observe(hero, { attributes: true, attributeFilter: ["data-tone"] });
      return true;
    };

    const tick = () => {
      if (attach() || tries > 12) {
        return;
      }
      tries += 1;
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => {
      cancelAnimationFrame(raf);
      detach();
    };
  }, [location.pathname, ia.id]);

  const headerClass = [
    "header",
    overHero ? "is-over-hero" : "",
    overHero && heroDark ? "is-on-dark" : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <>
      <header className={headerClass}>
        <div className="header-start">
          <button
            className="header-btn menu-btn"
            type="button"
            aria-expanded={drawerOpen}
            aria-label={drawerOpen ? "关闭菜单" : "打开菜单"}
            onClick={toggleDrawer}
          >
            <span />
          </button>
          <Link to="/" className="brand" onClick={closeAll}>
            <span className="brand-cn">{currentSkin.brand.zh}</span>
            <span className="brand-en">{currentSkin.brand.deco}</span>
          </Link>
        </div>

        <div className="header-actions">
          <button
            className="header-btn"
            type="button"
            aria-expanded={skinOpen}
            aria-haspopup="dialog"
            aria-label="皮肤"
            onClick={toggleSkin}
          >
            <span className="skin-mark" aria-hidden="true">
              <i />
              <i />
              <i />
              <i />
            </span>
          </button>
          <button className="header-btn" type="button" aria-label="语言">
            <span className="locale-mark" aria-hidden="true">
              中
            </span>
          </button>
        </div>
      </header>

      <div
        className={`drawer${drawerOpen ? " is-open" : ""}`}
        hidden={!drawerOpen}
        aria-hidden={!drawerOpen}
      >
        {ia.nav.map((item) => (
          <NavLink
            key={item.id}
            to={item.path}
            end={item.path === "/"}
            onClick={() => {
              closeAll();
              onNavigate?.();
            }}
          >
            {item.label}
            <span>{item.labelEn}</span>
          </NavLink>
        ))}
      </div>

      <SkinPicker open={skinOpen} onClose={() => setSkinOpen(false)} />
    </>
  );
}
