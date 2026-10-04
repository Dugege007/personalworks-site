import { lexicon } from "../content/lexicon";
import { navItems } from "../content/site";
import type { IaRecord } from "./types";

export const strataIa: IaRecord = {
  id: "strata-archive",
  nav: navItems,
  home: {
    blocks: [{ type: "hero" }, { type: "work-sections" }, { type: "contact" }],
  },
  templates: {
    home: "strata-scroll",
    category: "category-page",
    detail: "archive-detail",
  },
};

export const strataHomePath = "/";
export const strataKindPathDict: Record<string, string> = {
  [lexicon.twinAndSim.key]: `/${lexicon.twinAndSim.key}`,
  [lexicon.gameDev.key]: `/${lexicon.gameDev.key}`,
  [lexicon.landscapeArch.key]: `/${lexicon.landscapeArch.key}`,
  [lexicon.photography.key]: `/${lexicon.photography.key}`,
};
