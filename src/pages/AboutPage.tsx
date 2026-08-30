import { aboutFeatures, archiveIndexByNavId } from "../content/site";
import { lexicon } from "../content/lexicon";
import "../styles/placeholder.css";

/**
 * 关于页：按功能块说明本站如何搭起来，对应设计文档中的主路径决策。
 */
export function AboutPage() {
  return (
    <div className="page" data-theme={lexicon.about.key}>
      <div className="page-kicker">
        {archiveIndexByNavId(lexicon.about.key)} / {lexicon.about.deco}
      </div>
      <h1>{lexicon.about.zh}</h1>
      <p className="page-lead">
        层境 STRATA 是个人作品场。下面按搭建时真正卡住过的问题拆成若干功能，便于看清站点是怎么一层层收口的。
      </p>
      <div className="about-grid">
        {aboutFeatures.map((feature) => (
          <article className="about-card" key={feature.id}>
            <h2>{feature.title}</h2>
            <p className="about-lead">{feature.lead}</p>
            <ul>
              {feature.points.map((point) => (
                <li key={point}>{point}</li>
              ))}
            </ul>
          </article>
        ))}
      </div>
    </div>
  );
}
