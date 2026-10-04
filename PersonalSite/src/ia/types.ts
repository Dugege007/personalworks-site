import type { NavItem } from "../content/site";

export type IaId = string;

export type ContentQuery = {
  source: "works" | "games" | "notes" | "profile" | "resume" | "skills" | "about";
  channel?: string | string[];
  kind?: string;
  featured?: boolean;
  ids?: string[];
  limit?: number;
};

export type HomeBlock = {
  type: string;
  query?: ContentQuery;
  identity?: string;
  heroSrc?: string;
  heroYear?: string;
};

export type IaRecord = {
  id: IaId;
  nav: NavItem[];
  home: { blocks: HomeBlock[] };
  templates: Record<string, string>;
};
