import { defaultSchema, type Schema } from "hast-util-sanitize";
import rehypeKatex from "rehype-katex";
import { transformerCopyButton } from "@rehype-pretty/transformers";
import rehypePrettyCode from "rehype-pretty-code";
import rehypeSanitize from "rehype-sanitize";
import rehypeSlug from "rehype-slug";
import rehypeStringify from "rehype-stringify";
import remarkCjkFriendly from "remark-cjk-friendly/parseOnly";
import remarkCjkFriendlyGfmStrikethrough from "remark-cjk-friendly-gfm-strikethrough/parseOnly";
import remarkGfm from "remark-gfm";
import remarkMath from "remark-math";
import remarkParse from "remark-parse";
import remarkRehype from "remark-rehype";
import { unified } from "unified";

const mathTagNames = [
  "math",
  "semantics",
  "annotation",
  "mrow",
  "mi",
  "mo",
  "mn",
  "ms",
  "mtext",
  "mspace",
  "mfrac",
  "msqrt",
  "mroot",
  "msup",
  "msub",
  "msubsup",
  "munder",
  "mover",
  "munderover",
  "mmultiscripts",
  "mprescripts",
  "none",
  "mtable",
  "mtr",
  "mtd",
  "mstyle",
  "mpadded",
  "menclose",
  "mphantom",
  "mglyph",
];

/**
 * KaTeX 的 MathML 靠这些属性表达上下标、根号和矩阵，缺了无障碍树就不完整。
 * 视觉布局则主要靠 span 的 class 与 style，以及根号用的 svg/path。
 */
const mathAttributes = [
  "className",
  "style",
  "xmlns",
  "display",
  "displaystyle",
  "encoding",
  "mathvariant",
  "stretchy",
  "accent",
  "accentunder",
  "fence",
  "separator",
  "minsize",
  "maxsize",
  "lspace",
  "rspace",
  "width",
  "height",
  "depth",
  "voffset",
  "columnalign",
  "columnspacing",
  "rowspacing",
  "columnlines",
  "rowlines",
  "frame",
  "framespacing",
  "align",
  "rowalign",
  "linethickness",
  "numalign",
  "denomalign",
  "bevelled",
  "scriptlevel",
  "notation",
  "symmetric",
  "largeop",
  "movablelimits",
  "form",
  "mathsize",
  "mathcolor",
  "mathbackground",
];

/**
 * 在默认 GitHub schema 上放行 KaTeX 产物。
 * 默认 schema 会去掉 span 的 class/style，也会丢掉 svg，公式因此只剩裸文本。
 * 另外关闭 id 前缀：GFM 脚注已经写了 user-content-，再加一层前缀后回链对不上。
 */
export const noteSanitizeSchema: Schema = {
  ...defaultSchema,
  clobber: [],
  tagNames: [...(defaultSchema.tagNames ?? []), ...mathTagNames, "svg", "path", "line"],
  attributes: {
    ...(defaultSchema.attributes ?? {}),
    code: [["className", /^language-./, "math-inline", "math-display"]],
    span: ["className", "style", "ariaHidden"],
    svg: ["className", "style", "ariaHidden", "xmlns", "viewBox", "preserveAspectRatio"],
    path: ["className", "style", "d"],
    line: ["className", "style", "x1", "y1", "x2", "y2", "stroke", "strokeWidth"],
    ...Object.fromEntries(mathTagNames.map((tag) => [tag, mathAttributes])),
  },
};

/**
 * 把公式定界符换成 remark-math 在关闭单美元后仍能识别的形式。
 * 直接把 \( \) 交给 CommonMark 时，反斜杠会被当成转义，公式只剩括号。
 * 单美元默认会把「$5 到 $10」整段吃进公式，因此只提升看起来像 TeX 的 $...$。
 * 行内公式写成 $$...$$，独立成行的 $$ 仍是显示公式。围栏代码和行内代码保持原样。
 */
export function normalizeTexDelimiters(markdown: string): string {
  let index = 0;
  let output = "";
  const length = markdown.length;

  while (index < length) {
    const fenceEnd = readFence(markdown, index);
    if (fenceEnd > index) {
      output += markdown.slice(index, fenceEnd);
      index = fenceEnd;
      continue;
    }

    const inlineEnd = readInlineCode(markdown, index);
    if (inlineEnd > index) {
      output += markdown.slice(index, inlineEnd);
      index = inlineEnd;
      continue;
    }

    if (markdown.startsWith("\\[", index)) {
      const close = markdown.indexOf("\\]", index + 2);
      const body = close === -1 ? "" : markdown.slice(index + 2, close).trim();
      if (close !== -1 && body) {
        output += `\n$$\n${body}\n$$\n`;
        index = close + 2;
        continue;
      }
    }

    if (markdown.startsWith("\\(", index)) {
      const close = markdown.indexOf("\\)", index + 2);
      const body = close === -1 ? "" : markdown.slice(index + 2, close);
      if (close !== -1 && body.trim()) {
        output += `$$${body}$$`;
        index = close + 2;
        continue;
      }
    }

    if (markdown.startsWith("$$", index)) {
      output += "$$";
      index += 2;
      continue;
    }

    if (markdown[index] === "$" && !isEscaped(markdown, index)) {
      const close = findSingleDollarClose(markdown, index);
      const body = close === -1 ? "" : markdown.slice(index + 1, close);
      if (close !== -1 && isMathLike(body)) {
        output += `$$${body}$$`;
        index = close + 1;
        continue;
      }
    }

    output += markdown[index];
    index += 1;
  }

  return output;
}

/**
 * 组装心得正文管线。每次返回新的处理器，避免插件状态串到下一篇。
 * 顺序是：GFM、中日韩强调、中日韩删除线、数学、KaTeX、净化、标题 id、去掉重复大标题、代码高亮、代码块语言标记。
 * CommonMark 把全角括号、顿号和句号算作标点。强调或删除线以这类标点收尾，
 * 后面又紧贴汉字时，闭合记号不成右翼，星号会留在正文里，或和后一处记号错配。
 * 中日韩插件只放宽这类翼标判定，放在 GFM 之后、数学之前。
 * 删除线插件必须位于 GFM 之后，否则补丁打不到 GFM 的分词器。
 * 净化必须放在 KaTeX 之后，否则公式的 class、style 和 svg 会被剥掉。
 * 标题 id 和代码高亮必须放在净化之后，否则 slug 与行内样式同样会被剥掉。
 * 与页面大标题同文的首个 h1 也在净化之后去掉，避免详情页出现两个相同标题。
 */
export function createNoteMarkdownProcessor(pageHeading?: string) {
  return unified()
    .use(remarkParse)
    .use(remarkGfm)
    .use(remarkCjkFriendly)
    .use(remarkCjkFriendlyGfmStrikethrough)
    .use(remarkMath, { singleDollarTextMath: false })
    .use(remarkRehype, { allowDangerousHtml: true })
    .use(rehypeKatex)
    .use(rehypeSanitize, noteSanitizeSchema)
    .use(rehypeSlug)
    .use(omitDuplicateLeadHeading(pageHeading))
    .use(rehypePrettyCode, {
      theme: "github-light",
      // 按钮默认常显。细指针的悬停显隐改由样式表处理，触控端保持可见。
      transformers: [transformerCopyButton({ visibility: "always", feedbackDuration: 3_000 })],
    })
    .use(stampCodeFigureLanguage);
}

type HastLike = {
  type: string;
  tagName?: string;
  value?: string;
  properties?: Record<string, unknown>;
  children?: HastLike[];
};

/**
 * 把围栏语言抄到 figure 上，供角标读取。
 * 复制按钮由高亮器在净化之后写入，此处只补语言，不改按钮。
 */
function stampCodeFigureLanguage() {
  return (tree: HastLike) => {
    const walk = (node: HastLike) => {
      if (
        node.type === "element" &&
        node.tagName === "figure" &&
        node.properties &&
        Object.prototype.hasOwnProperty.call(node.properties, "data-rehype-pretty-code-figure")
      ) {
        const pre = node.children?.find((child) => child.type === "element" && child.tagName === "pre");
        const lang = pre?.properties?.["data-language"];
        if (typeof lang === "string" && lang) {
          node.properties["data-language"] = lang;
        }
      }
      node.children?.forEach(walk);
    };
    walk(tree);
  };
}

/**
 * 把连续空白收成一个空格。页面大标题和正文 h1 用同一规则比较。
 */
export function normalizeNoteHeading(value: string): string {
  return value.replace(/\s+/gu, " ").trim();
}

function hastText(node: HastLike): string {
  if (node.type === "text") return node.value ?? "";
  if (!node.children) return "";
  return node.children.map(hastText).join("");
}

/**
 * 详情页 intro 已经渲染同文大标题时，去掉正文里的首个 h1。
 * 只比较第一个 h1；对不上就保留，后面的 h1 也不动。
 */
function omitDuplicateLeadHeading(pageHeading: string | undefined) {
  const expected = normalizeNoteHeading(pageHeading ?? "");
  return () => (tree: HastLike) => {
    if (!expected || !tree.children) return;
    const index = tree.children.findIndex((child) => child.type === "element" && child.tagName === "h1");
    if (index < 0) return;
    const lead = tree.children[index];
    if (!lead || normalizeNoteHeading(hastText(lead)) !== expected) return;
    tree.children.splice(index, 1);
  };
}

/**
 * 把心得 Markdown 渲染成 HTML，供测试和纯文本核对使用。
 */
export async function renderNoteMarkdownHtml(markdown: string, pageHeading?: string): Promise<string> {
  const file = await createNoteMarkdownProcessor(pageHeading)
    .use(rehypeStringify)
    .process(normalizeTexDelimiters(markdown));
  return String(file);
}

function readFence(markdown: string, index: number): number {
  const start = markdown.lastIndexOf("\n", index - 1) + 1;
  const prefix = markdown.slice(start, index);
  if (!/^ {0,3}$/.test(prefix)) {
    return index;
  }
  const marker = markdown.startsWith("```", index) ? "`" : markdown.startsWith("~~~", index) ? "~" : "";
  if (!marker) {
    return index;
  }
  let size = 0;
  while (markdown[index + size] === marker) {
    size += 1;
  }
  if (size < 3) {
    return index;
  }

  const closePattern = new RegExp(`^ {0,3}${marker}{${size},}[ \\t]*$`);
  let cursor = markdown.indexOf("\n", index);
  if (cursor === -1) {
    return markdown.length;
  }
  cursor += 1;
  while (cursor <= markdown.length) {
    const nextBreak = markdown.indexOf("\n", cursor);
    const lineEnd = nextBreak === -1 ? markdown.length : nextBreak;
    if (closePattern.test(markdown.slice(cursor, lineEnd))) {
      return nextBreak === -1 ? lineEnd : lineEnd + 1;
    }
    if (nextBreak === -1) {
      return markdown.length;
    }
    cursor = nextBreak + 1;
  }
  return markdown.length;
}

function findSingleDollarClose(markdown: string, open: number): number {
  const lineEnd = markdown.indexOf("\n", open);
  const limit = lineEnd === -1 ? markdown.length : lineEnd;
  let cursor = open + 1;
  while (cursor < limit) {
    if (markdown[cursor] === "\\") {
      cursor += 2;
      continue;
    }
    if (markdown[cursor] === "$") {
      return markdown[cursor + 1] === "$" ? -1 : cursor;
    }
    cursor += 1;
  }
  return -1;
}

function isEscaped(markdown: string, index: number): boolean {
  let slashes = 0;
  let cursor = index - 1;
  while (cursor >= 0 && markdown[cursor] === "\\") {
    slashes += 1;
    cursor -= 1;
  }
  return slashes % 2 === 1;
}

/**
 * 含命令、上下标或单个拉丁符号才当作行内公式。纯数字和夹着汉字的区间留给正文。
 */
function isMathLike(content: string): boolean {
  if (!content || content.length > 400 || /^\s|\s$/.test(content)) {
    return false;
  }
  if (/\p{Script=Han}/u.test(content) || /[，。！？、；：]/.test(content)) {
    return false;
  }
  if (/[\\_^{}]/.test(content)) {
    return true;
  }
  return /^[A-Za-z][A-Za-z0-9]*$/.test(content);
}

function readInlineCode(markdown: string, index: number): number {
  if (markdown[index] !== "`") {
    return index;
  }
  let size = 0;
  while (markdown[index + size] === "`") {
    size += 1;
  }
  const close = "`".repeat(size);
  const end = markdown.indexOf(close, index + size);
  if (end === -1) {
    return index;
  }
  if (markdown.slice(index + size, end).includes("\n")) {
    return index;
  }
  return end + size;
}
