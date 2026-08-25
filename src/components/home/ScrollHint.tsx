import { useEffect, useState } from "react";

export function ScrollHint() {
  const [gone, setGone] = useState(false);

  useEffect(() => {
    const onScroll = () => {
      setGone(window.scrollY > 72);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <div className={`scroll-hint${gone ? " is-gone" : ""}`} aria-hidden={gone}>
      <b>SCROLL</b>
      <div className="chevron" />
      <span>向下探索</span>
    </div>
  );
}
