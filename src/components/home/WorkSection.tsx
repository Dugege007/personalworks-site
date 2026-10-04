import { useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import gsap from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";
import type { WorkSection as WorkSectionData } from "../../content/site";
import { TermText } from "../TermText";
import { resolveLead } from "../../content/copyDisplay";
import { firstPhotoHomeSrc } from "../../content/works";
import { lexicon } from "../../content/lexicon";
import { homeFrameSrcs } from "../../content/stockMedia";
import { usePrefersReducedMotion } from "../../hooks/usePrefersReducedMotion";
import { assetUrl } from "../../lib/assets";

gsap.registerPlugin(ScrollTrigger);

type WorkSectionProps = {
  data: WorkSectionData;
  flip: boolean;
};

export function WorkSection({ data, flip }: WorkSectionProps) {
  const rootRef = useRef<HTMLElement>(null);
  const reduced = usePrefersReducedMotion();

  useEffect(() => {
    const root = rootRef.current;
    if (!root || reduced) {
      return;
    }

    const ctx = gsap.context(() => {
      gsap.from(root.querySelectorAll(".work-copy, .frame"), {
        y: 32,
        opacity: 0,
        duration: 0.8,
        stagger: 0.08,
        ease: "power2.out",
        scrollTrigger: {
          trigger: root,
          start: "top 78%",
          once: true,
        },
      });
    }, root);

    return () => ctx.revert();
  }, [reduced]);

  return (
    <section
      ref={rootRef}
      className={`work${flip ? " is-flip" : ""}`}
      id={`sec-${data.id}`}
      data-theme={data.theme}
    >
      <div className="work-copy">
        <div className="kicker">
          <i />
          {data.index} / {data.indexEn}
        </div>
        <h2>{data.title}</h2>
        <p className="work-lead">
          {resolveLead(data.lead) ? <TermText text={resolveLead(data.lead)!} /> : data.lead}
        </p>
        <Link className="archive-btn" to={data.path}>
          进入档案
          <span aria-hidden="true">→</span>
        </Link>
      </div>
      <div className="frames">
        {data.frames.map((label, index) => {
          const src =
            data.id === lexicon.photography.key
              ? (firstPhotoHomeSrc(label) ?? homeFrameSrcs[data.id]?.[index])
              : homeFrameSrcs[data.id]?.[index];
          return (
            <div className="frame" key={label}>
              {src ? <img src={assetUrl(src)} alt="" /> : null}
              <em>0{index + 1}</em>
              <strong>{label}</strong>
            </div>
          );
        })}
      </div>
    </section>
  );
}
