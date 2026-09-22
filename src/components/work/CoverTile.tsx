import { useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { assetUrl } from "../../lib/assets";

type CoverTileProps = {
  to?: string;
  src?: string;
  fallbackSrc?: string;
  className: string;
  wellClass?: string;
  children: ReactNode;
  disabled?: boolean;
  bootFirst?: boolean;
};

/**
 * 画册格：有封面则铺图，失败则试栏目 stock，再不行走井底。
 */
export function CoverTile({
  to,
  src,
  fallbackSrc,
  className,
  wellClass = "develop-work-well",
  children,
  disabled,
  bootFirst = false,
}: CoverTileProps) {
  const [failed, setFailed] = useState(false);
  const [fallbackFailed, setFallbackFailed] = useState(false);
  const primary = src && !failed ? assetUrl(src) : "";
  const fallback = !primary && fallbackSrc && !fallbackFailed ? assetUrl(fallbackSrc) : "";
  const href = primary || fallback;
  const inner = (
    <>
      {href ? (
        <img
          src={href}
          alt=""
          {...(bootFirst ? { "data-boot-first": "" } : {})}
          onError={() => {
            if (primary) {
              setFailed(true);
            } else {
              setFallbackFailed(true);
            }
          }}
        />
      ) : (
        <span className={wellClass} aria-hidden="true" />
      )}
      <span className="develop-work-copy">{children}</span>
    </>
  );

  if (disabled || !to) {
    return <div className={`${className} is-soon`}>{inner}</div>;
  }

  return (
    <Link className={className} to={to}>
      {inner}
    </Link>
  );
}
