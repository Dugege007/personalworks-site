import { Link } from "react-router-dom";
import type { ReactNode } from "react";

type ChannelHeadProps = {
  backTo: string;
  backLabel: string;
  title: string;
  lead?: string;
  children?: ReactNode;
};

/**
 * 显影作品树共用页头：返回、标题、导语。类目页与摄影详情共用，保证位置一致。
 */
export function ChannelHead({ backTo, backLabel, title, lead, children }: ChannelHeadProps) {
  return (
    <div className="develop-kind-head">
      <Link className="develop-work-back" to={backTo}>
        {backLabel}
      </Link>
      <h1>{title}</h1>
      {lead ? <p className="develop-kind-lead">{lead}</p> : null}
      {children}
    </div>
  );
}
