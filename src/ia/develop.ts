import { lexicon } from "../content/lexicon";
import { profile } from "../content/site";
import type { IaRecord } from "./types";

export const developIa: IaRecord = {
  id: "develop-editorial",
  nav: [
    { id: "home", label: "主页", labelEn: lexicon.homePage.deco, path: "/", theme: "home" },
    {
      id: lexicon.profileResume.key,
      label: lexicon.profileResume.zh,
      labelEn: lexicon.profileResume.deco,
      path: `/${lexicon.profileResume.key}`,
      theme: lexicon.profileResume.key,
    },
    {
      id: lexicon.profileSkills.key,
      label: lexicon.profileSkills.zh,
      labelEn: lexicon.profileSkills.deco,
      path: `/${lexicon.profileSkills.key}`,
      theme: lexicon.profileSkills.key,
    },
    {
      id: lexicon.workIndex.key,
      label: lexicon.workIndex.zh,
      labelEn: lexicon.workIndex.deco,
      path: `/${lexicon.workIndex.key}`,
      theme: lexicon.workIndex.key,
    },
    {
      id: lexicon.notes.key,
      label: lexicon.notes.zh,
      labelEn: lexicon.notes.deco,
      path: `/${lexicon.notes.key}`,
      theme: lexicon.notes.key,
    },
    {
      id: lexicon.about.key,
      label: lexicon.about.zh,
      labelEn: lexicon.about.deco,
      path: `/${lexicon.about.key}`,
      theme: lexicon.about.key,
    },
  ],
  home: {
    blocks: [
      {
        type: "hero-bleed",
        query: { source: "profile" },
        identity: `${lexicon.digitalTwin.zh} · ${lexicon.landscapeArch.zh} · ${lexicon.photography.zh}`,
        heroSrc: profile.homeHeroSrc,
        heroYear: profile.homeHeroYear,
      },
      { type: "intro-portrait", query: { source: "profile" } },
      { type: "waypoint-slices" },
      { type: "selected-frames", query: { source: "works", featured: true, limit: 5 } },
      { type: "notes-tease", query: { source: "notes", limit: 1 } },
      { type: "contact-close", query: { source: "profile" } },
    ],
  },
  templates: {
    home: "develop-editorial",
    workIndex: "work-index",
    resume: "profile-resume",
    skills: "profile-skills",
    photoCatalog: "catalog",
    detail: "bleed-detail",
  },
};
