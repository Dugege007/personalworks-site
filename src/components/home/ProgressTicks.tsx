import { useEffect, useMemo, useState } from "react";
import { workSections } from "../../content/site";

const sectionIds = ["sec-hero", ...workSections.map((item) => `sec-${item.id}`)];

export function ProgressTicks() {
  const [active, setActive] = useState("sec-hero");
  const ratio = useMemo(() => {
    const index = Math.max(0, sectionIds.indexOf(active));
    return (index + 1) / sectionIds.length;
  }, [active]);

  useEffect(() => {
    const nodes = sectionIds
      .map((id) => document.getElementById(id))
      .filter((node): node is HTMLElement => node !== null);

    // 以“谁包含视口水平中线”作为唯一判据，避免 IntersectionObserver 只回调越阈值条目时在交界处回跳。
    const syncActive = () => {
      const mid = window.innerHeight / 2;
      const current = nodes.find((node) => {
        const rect = node.getBoundingClientRect();
        return rect.top <= mid && rect.bottom > mid;
      });
      if (!current?.id) {
        return;
      }
      setActive((prev) => (prev === current.id ? prev : current.id));
    };

    syncActive();
    window.addEventListener("scroll", syncActive, { passive: true });
    window.addEventListener("resize", syncActive);
    return () => {
      window.removeEventListener("scroll", syncActive);
      window.removeEventListener("resize", syncActive);
    };
  }, []);

  return (
    <>
      <div className="ticks" aria-hidden="true">
        {sectionIds.map((id) => (
          <span key={id} className={`tick${id === active ? " is-on" : ""}`} />
        ))}
      </div>
      <div className="top-progress" style={{ transform: `scaleX(${ratio})` }} />
    </>
  );
}
