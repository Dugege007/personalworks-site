import { useState } from "react";
import { NavLink, Link } from "react-router-dom";
import { navItems, profile } from "../content/site";

type SiteHeaderProps = {
  onNavigate?: () => void;
};

export function SiteHeader({ onNavigate }: SiteHeaderProps) {
  const [open, setOpen] = useState(false);

  const close = () => setOpen(false);

  return (
    <>
      <header className="header">
        <Link to="/" className="brand" onClick={close}>
          <span className="brand-cn">{profile.siteLabel}</span>
          <span className="brand-en">{profile.siteLabelEn}</span>
        </Link>

        <nav className="nav-desktop" aria-label="站点栏目">
          {navItems.map((item) => (
            <NavLink
              key={item.id}
              to={item.path}
              end={item.path === "/"}
              className={({ isActive }) => `nav-link${isActive ? " is-active" : ""}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <button
          className="menu-btn"
          type="button"
          aria-expanded={open}
          aria-label={open ? "关闭菜单" : "打开菜单"}
          onClick={() => setOpen((value) => !value)}
        >
          <span />
        </button>
      </header>

      <div
        className={`drawer${open ? " is-open" : ""}`}
        hidden={!open}
        aria-hidden={!open}
      >
        {navItems.map((item) => (
          <NavLink
            key={item.id}
            to={item.path}
            end={item.path === "/"}
            onClick={() => {
              close();
              onNavigate?.();
            }}
          >
            {item.label}
            <span>{item.labelEn}</span>
          </NavLink>
        ))}
      </div>
    </>
  );
}
