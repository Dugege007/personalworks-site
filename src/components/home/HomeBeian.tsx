import { filing } from "../../content/filing";
import "../../styles/beian.css";

/**
 * 仅首页底部居中：工信部网站备案号；公安号有值才并排。
 */
export function HomeBeian() {
  const hasIcp = Boolean(filing.icp);
  const hasPolice = Boolean(filing.police);
  if (!hasIcp && !hasPolice) {
    return null;
  }

  return (
    <p className="home-beian">
      {hasIcp ? (
        <a href={filing.icpHref} target="_blank" rel="noreferrer">
          {filing.icp}
        </a>
      ) : null}
      {hasIcp && hasPolice ? <span aria-hidden="true"> · </span> : null}
      {hasPolice ? (
        <a href={filing.policeHref} target="_blank" rel="noreferrer">
          {filing.police}
        </a>
      ) : null}
    </p>
  );
}
