import { filing } from "../../content/filing";
import { assetUrl } from "../../lib/assets";
import "../../styles/beian.css";

/**
 * 仅首页底部居中：工信部网站备案号与公安备案号并排；公安号前挂核发图标。
 */
export function HomeBeian() {
  const hasIcp = Boolean(filing.icp);
  const hasPolice = Boolean(filing.police);
  if (!hasIcp && !hasPolice) {
    return null;
  }

  const policeIcon = filing.policeIcon ? assetUrl(filing.policeIcon) : "";

  return (
    <p className="home-beian">
      {hasIcp ? (
        <a href={filing.icpHref} target="_blank" rel="noreferrer">
          {filing.icp}
        </a>
      ) : null}
      {hasIcp && hasPolice ? (
        <span className="home-beian-sep" aria-hidden="true">
          {"\u00a0\u00a0·\u00a0\u00a0"}
        </span>
      ) : null}
      {hasPolice ? (
        <a className="home-beian-police" href={filing.policeHref} target="_blank" rel="noreferrer">
          {policeIcon ? (
            <img
              className="home-beian-police-icon"
              src={policeIcon}
              alt=""
              width={16}
              height={17}
            />
          ) : null}
          {filing.police}
        </a>
      ) : null}
    </p>
  );
}
