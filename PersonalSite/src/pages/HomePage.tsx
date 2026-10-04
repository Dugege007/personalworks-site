import type { ReactElement } from "react";
import { iaOfSkin } from "../ia";
import { DevelopHome } from "../components/home/DevelopHome";
import { StrataHome } from "../components/home/StrataHome";
import { usePrefs } from "../prefs/PrefsProvider";
import type { IaRecord } from "../ia/types";

const homeRendererDict: Record<string, (props: { ia: IaRecord }) => ReactElement> = {
  "strata-scroll": StrataHome,
  "develop-editorial": DevelopHome,
};

/**
 * 按当前 IA 的 templates.home 选渲染器，不按皮肤 id 分支。
 */
export function HomePage() {
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const Renderer = homeRendererDict[ia.templates.home] ?? StrataHome;
  return <Renderer ia={ia} />;
}
