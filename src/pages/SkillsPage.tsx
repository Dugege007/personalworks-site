import { lexicon } from "../content/lexicon";
import "../styles/placeholder.css";

/**
 * 技能页最小闭环。领域手风琴留给后续阶段。
 */
export function SkillsPage() {
  return (
    <div className="page" data-theme={lexicon.profileSkills.key}>
      <h1>{lexicon.profileSkills.zh}</h1>
      <p className="page-lead">数字孪生与产线仿真、景观设计、游戏开发与摄影。条目与证明句后续按领域展开。</p>
    </div>
  );
}
