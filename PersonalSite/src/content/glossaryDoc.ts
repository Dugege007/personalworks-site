import glossaryMd from "../../文稿/glossary.md?raw";
import { parseGlossary, type GlossaryBook } from "./glossary";

/** 名词表正本。文稿保存后随热更新重读。 */
export const glossaryBook: GlossaryBook = parseGlossary(glossaryMd);
