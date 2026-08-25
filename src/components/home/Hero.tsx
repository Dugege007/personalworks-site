import { useEffect, useRef } from "react";
import { profile } from "../../content/site";
import { usePrefersReducedMotion } from "../../hooks/usePrefersReducedMotion";
import { ScrollHint } from "./ScrollHint";

export function Hero() {
  const wrapRef = useRef<HTMLDivElement>(null);
  const spotRef = useRef<HTMLDivElement>(null);
  const reduced = usePrefersReducedMotion();

  useEffect(() => {
    const wrap = wrapRef.current;
    const spot = spotRef.current;
    if (!wrap || !spot || reduced) {
      return;
    }
    // 令牌关闭光斑时不绑定指针跟踪
    const spotOn =
      Number.parseFloat(
        getComputedStyle(document.documentElement).getPropertyValue("--motion-spot"),
      ) > 0;
    if (!spotOn) {
      return;
    }
    const fine = window.matchMedia("(pointer: fine)").matches;
    if (!fine) {
      spot.style.opacity = "0";
      return;
    }

    const onMove = (event: MouseEvent) => {
      const rect = wrap.getBoundingClientRect();
      spot.style.left = `${event.clientX - rect.left}px`;
      spot.style.top = `${event.clientY - rect.top}px`;
    };

    wrap.addEventListener("mousemove", onMove);
    return () => wrap.removeEventListener("mousemove", onMove);
  }, [reduced]);

  return (
    <section className="hero" id="sec-hero">
      <div className="portrait-wrap" ref={wrapRef}>
        <span className="corner corner-tl" />
        <span className="corner corner-tr" />
        <span className="corner corner-bl" />
        <span className="corner corner-br" />
        <div className="portrait-frame">
          <div className="portrait-grid" />
          <div className="portrait-spot" ref={spotRef} />
          <div className="portrait-caption">PORTRAIT / 形象占位</div>
        </div>
      </div>

      <div className="hero-copy">
        <div className="kicker reveal is-in">
          <i />
          00 / STRATA
        </div>
        <h1 className="hero-name reveal is-in">{profile.name}</h1>
        <div className="hero-en">{profile.nameEn}</div>
        <div className="hero-id">{profile.identity}</div>
        <p className="hero-bio">{profile.bio}</p>
        <div className="contacts">
          {profile.contacts.map((item) => (
            <a key={item.label} className="contact-row" href={item.href}>
              <span>{item.label}</span>
              <strong>{item.value}</strong>
              <em>{item.note}</em>
            </a>
          ))}
        </div>
      </div>
      <ScrollHint />
    </section>
  );
}
