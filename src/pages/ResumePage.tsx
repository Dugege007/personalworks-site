import { lexicon } from "../content/lexicon";
import { profile } from "../content/site";
import "../styles/placeholder.css";

/**
 * 简历页最小闭环。分栏细排留给后续阶段。
 */
export function ResumePage() {
  return (
    <div className="page" data-theme={lexicon.profileResume.key}>
      <h1>{lexicon.profileResume.zh}</h1>
      <p className="page-lead">{profile.identity.split(" / ").join(" · ")}</p>
      {profile.bio.map((line) => (
        <p key={line} className="archive-body">
          {line}
        </p>
      ))}
    </div>
  );
}
