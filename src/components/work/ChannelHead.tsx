import { Link } from "react-router-dom";
import type { ReactNode } from "react";
import { TermText } from "../TermText";
import { resolveLead } from "../../content/copyDisplay";

type ChannelHeadProps = {
  backTo: string;
  backLabel: string;
  title: string;
  lead?: string;
  children?: ReactNode;
};

/**
 * 作品树共用页头：返回、标题、导语。类目页与各栏目项目详情共用，保证位置一致。
 */
export function ChannelHead({ backTo, backLabel, title, lead, children }: ChannelHeadProps) {
  const filledLead = resolveLead(lead);
  return (
    <div className="develop-kind-head">
      <Link className="develop-work-back" to={backTo}>
        {backLabel}
      </Link>
      <h1>{title}</h1>
      {filledLead ? (
        <p className="develop-kind-lead">
          <TermText text={filledLead} />
        </p>
      ) : null}
      {children}
    </div>
  );
}
