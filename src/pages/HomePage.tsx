import { workSections } from "../content/site";
import { FooterContact } from "../components/home/FooterContact";
import { Hero } from "../components/home/Hero";
import { ProgressTicks } from "../components/home/ProgressTicks";
import { WorkSection } from "../components/home/WorkSection";
import "../styles/home.css";

export function HomePage() {
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
