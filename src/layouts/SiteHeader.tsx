import { useEffect, useState } from "react";
import { NavLink, Link } from "react-router-dom";
import { navItems, profile } from "../content/site";
import { SkinPicker } from "./SkinPicker";

type SiteHeaderProps = {
  onNavigate?: () => void;
};

export function SiteHeader({ onNavigate }: SiteHeaderProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [skinOpen, setSkinOpen] = useState(false);

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

  return (
    <>
      <header className="header">
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
            <span className="brand-cn">{profile.siteLabel}</span>
            <span className="brand-en">{profile.siteLabelEn}</span>
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
        {navItems.map((item) => (
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
