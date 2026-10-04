import { workSections } from "../../content/site";
import { FooterContact } from "./FooterContact";
import { Hero } from "./Hero";
import { ProgressTicks } from "./ProgressTicks";
import { WorkSection } from "./WorkSection";
import "../../styles/home.css";
import type { IaRecord } from "../../ia/types";

type StrataHomeProps = {
  ia: IaRecord;
};

/**
 * 层境首页：形象框景 + 四段分类入口。ia 仅作渲染器契约，块顺序仍以本版式为准。
 */
export function StrataHome({ ia }: StrataHomeProps) {
  void ia;
  return (
    <div>
      <Hero />
      <ProgressTicks />
      {workSections.map((section, index) => (
        <WorkSection key={section.id} data={section} flip={index % 2 === 1} />
      ))}
      <FooterContact />
    </div>
  );
}
