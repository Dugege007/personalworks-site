import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { normalizeTexDelimiters, renderNoteMarkdownHtml } from "../src/notes/renderMarkdown.ts";

const ewmaNote = readFileSync(
  fileURLToPath(new URL("../src/content/notes/n-20261007-015206.md", import.meta.url)),
  "utf8",
);

test("tex delimiters become dollar math and prose currency stays literal", () => {
  assert.equal(normalizeTexDelimiters(String.raw`记为 \(Y_t\)。`), "记为 $$Y_t$$。");
  assert.equal(normalizeTexDelimiters("\\[\nZ_t\n\\]\n"), "\n$$\nZ_t\n$$\n\n");
  assert.equal(normalizeTexDelimiters("行内 \\[Z_t\\] 结束"), "行内 \n$$\nZ_t\n$$\n 结束");
  assert.equal(normalizeTexDelimiters("```text\n\\(Y_t\\)\n```\n"), "```text\n\\(Y_t\\)\n```\n");
  assert.equal(normalizeTexDelimiters("`\\(Y_t\\)`"), "`\\(Y_t\\)`");
  assert.equal(normalizeTexDelimiters("$Y_t$ 与 $$Z_t$$"), "$$Y_t$$ 与 $$Z_t$$");
  assert.equal(normalizeTexDelimiters("符号 $x$ 与 $\\mu$"), "符号 $$x$$ 与 $$\\mu$$");
  assert.equal(normalizeTexDelimiters("价格从 $5 到 $10，转义 \\$20。"), "价格从 $5 到 $10，转义 \\$20。");
});

test("published ewma note renders math, headings, emphasis, links, and highlighted code", async () => {
  const html = await renderNoteMarkdownHtml(ewmaNote);

  assert.match(html, /class="katex"/);
  assert.match(html, /class="katex-display"/);
  assert.match(html, /<math[^>]*xmlns="http:\/\/www\.w3\.org\/1998\/Math\/MathML"/);
  assert.match(html, /<svg[^>]*viewBox=/);
  assert.match(html, /<path d="/);
  assert.match(html, /style="[^"]*vertical-align:/);
  assert.match(html, /encoding="application\/x-tex"[^>]*>[^<]*\\, Z_\{t-1\}/);
  assert.equal(html.includes("\\(Y_t\\)"), false);

  assert.match(html, /<h2 id="怎么工作">怎么工作<\/h2>/);
  assert.match(html, /<h3 id="1-递推定义">1\. 递推定义<\/h3>/);
  assert.match(html, /<strong>稳态近似<\/strong>/);
  assert.match(html, /<em>Journal of Quality Technology<\/em>/);
  assert.match(
    html,
    /<a href="https:\/\/www\.itl\.nist\.gov\/div898\/handbook\/pmc\/section3\/pmc314\.htm">/,
  );
  assert.match(html, /data-rehype-pretty-code-figure=""/);
  assert.match(html, /data-language="python"/);
  assert.match(html, /ewma_control/);
});

test("gfm constructs survive sanitize, including footnotes and relative images", async () => {
  const html = await renderNoteMarkdownHtml(`
# 标题

这是 **粗体**、*斜体* 和 ~~删除线~~。

- 普通列表
- [ ] 未完成
- [x] 已完成

| 列 | 值 |
| --- | --- |
| λ | $Y_t$ |

自动链接 https://example.com/a

脚注 [^1]。

[^1]: 注记

\`inline\`

\`\`\`text
\\(Y_t\\)
\`\`\`

![图](notes/demo.png)

价格从 $5 到 $10，转义 \\$20 仍是正文。
`);

  assert.match(html, /<h1 id="标题">标题<\/h1>/);
  assert.match(html, /<strong>粗体<\/strong>/);
  assert.match(html, /<em>斜体<\/em>/);
  assert.match(html, /<del>删除线<\/del>/);
  assert.match(html, /<ul class="contains-task-list">/);
  assert.match(html, /<li class="task-list-item"><input type="checkbox" disabled>/);
  assert.match(html, /<input type="checkbox" checked disabled>/);
  assert.match(html, /<table>/);
  assert.match(html, /<td><span class="katex">/);
  assert.match(html, /<a href="https:\/\/example\.com\/a">https:\/\/example\.com\/a<\/a>/);
  assert.match(html, /id="user-content-fn-1"/);
  assert.match(html, /href="#user-content-fn-1"/);
  assert.equal(html.includes("user-content-user-content"), false);
  assert.match(html, /<img src="notes\/demo.png" alt="图">/);
  assert.match(html, /价格从 \$5 到 \$10，转义 \$20 仍是正文。/);
  assert.equal(html.includes("katex-error"), false);

  const codeText = html.slice(html.indexOf("<figure"), html.indexOf("</figure>")).replace(/<[^>]+>/g, "");
  assert.match(codeText, /\\\(Y_t\\\)/);
  assert.equal(codeText.includes("katex"), false);
});

test("dollar and display delimiters render, and an empty note does not throw", async () => {
  const html = await renderNoteMarkdownHtml("行内 $Y_t$，以及\n\n$$\nZ_t = \\lambda\n$$\n");
  assert.match(html, /<p>行内 <span class="katex">/);
  assert.match(html, /class="katex-display"/);
  assert.match(html, /encoding="application\/x-tex"[^>]*>Y_t</);
  assert.equal(await renderNoteMarkdownHtml(""), "");
});
