import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { stripNoteFrontmatter } from "../src/content/notes/select.ts";
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

test("fences keep a copy button, optional line numbers, and unlabeled blocks", async () => {
  const html = await renderNoteMarkdownHtml(`
\`\`\`python
print("hi")
\`\`\`

\`\`\`js showLineNumbers
const n = 1;
\`\`\`

\`\`\`
plain
\`\`\`
`);

  const pythonEnd = html.indexOf("</figure>");
  const python = html.slice(0, pythonEnd);
  assert.match(python, /<figure[^>]*data-language="python"/);
  assert.match(python, /class="rehype-pretty-copy"/);
  assert.match(python, /data="print\(&#x22;hi&#x22;\)"/);
  assert.equal(python.includes("data-line-numbers"), false);

  const js = html.slice(pythonEnd, html.lastIndexOf("</figure>") + "</figure>".length);
  assert.match(js, /data-line-numbers=""/);
  assert.match(js, /data-language="js"/);
  assert.match(js, /class="rehype-pretty-copy"/);

  const tail = html.slice(html.lastIndexOf("</figure>"));
  assert.match(tail, /<pre><code>plain/);
  assert.equal(tail.includes("rehype-pretty-copy"), false);
  assert.match(html, /data-theme="github-light"/);
});

test("the dtw sample keeps a single page heading and drops the matching body h1", async () => {
  const markdown = stripNoteFrontmatter(
    readFileSync(fileURLToPath(new URL("../src/content/notes/n-20261028-205600.md", import.meta.url)), "utf8"),
  );
  const heading = "动态时间规整：把长短不一的两条曲线掰到同一套节拍上再比距离";
  const html = await renderNoteMarkdownHtml(markdown, heading);
  assert.equal(/<h1\b/.test(html), false);
  assert.match(html, /<h2[^>]*>是什么<\/h2>/);
  assert.match(html, /<h3[^>]*>直觉画面<\/h3>/);
});

test("the first h1 is omitted only when it matches the page heading", async () => {
  const duplicate = await renderNoteMarkdownHtml("# 同文  标题\n\n正文\n\n# 另一个标题\n", "同文 标题");
  assert.equal(duplicate.includes("同文"), false);
  assert.match(duplicate, /<p>正文<\/p>/);
  assert.match(duplicate, /<h1[^>]*>另一个标题<\/h1>/);

  const spaced = await renderNoteMarkdownHtml("# 标题\u3000甲\n\n段落\n", "标题 甲");
  assert.equal(/<h1\b/.test(spaced), false);
  assert.match(spaced, /<p>段落<\/p>/);

  const different = await renderNoteMarkdownHtml("# 正文标题\n\n段落\n", "页面标题");
  assert.match(different, /<h1[^>]*>正文标题<\/h1>/);

  const untouched = await renderNoteMarkdownHtml("# 标题\n\n段落\n");
  assert.match(untouched, /<h1[^>]*>标题<\/h1>/);
});

test("dollar and display delimiters render, and an empty note does not throw", async () => {
  const html = await renderNoteMarkdownHtml("行内 $Y_t$，以及\n\n$$\nZ_t = \\lambda\n$$\n");
  assert.match(html, /<p>行内 <span class="katex">/);
  assert.match(html, /class="katex-display"/);
  assert.match(html, /encoding="application\/x-tex"[^>]*>Y_t</);
  assert.equal(await renderNoteMarkdownHtml(""), "");
});

test("emphasis next to fullwidth punctuation stays strong when Chinese follows", async () => {
  const opening = await renderNoteMarkdownHtml(
    "**有向距离场（Signed Distance Field，SDF）加球面追踪（Sphere Tracing）**是前端视觉",
  );
  assert.match(
    opening,
    /<p><strong>有向距离场（Signed Distance Field，SDF）加球面追踪（Sphere Tracing）<\/strong>是前端视觉<\/p>/,
  );

  const wrapped = await renderNoteMarkdownHtml("前面**（括号）**后面");
  assert.match(wrapped, /<p>前面<strong>（括号）<\/strong>后面<\/p>/);

  const sentence = await renderNoteMarkdownHtml("结束句号**粗体。**后面");
  assert.match(sentence, /<p>结束句号<strong>粗体。<\/strong>后面<\/p>/);

  const paired = await renderNoteMarkdownHtml(
    "**滑动窗口最大值（LeetCode 239，Sliding Window Maximum）**的标准解法是**单调双端队列（monotonic deque）**，代码只有十来行。",
  );
  assert.match(
    paired,
    /<p><strong>滑动窗口最大值（LeetCode 239，Sliding Window Maximum）<\/strong>的标准解法是<strong>单调双端队列（monotonic deque）<\/strong>，代码只有十来行。<\/p>/,
  );

  const strike = await renderNoteMarkdownHtml("文字~~删除。~~后面");
  assert.match(strike, /<p>文字<del>删除。<\/del>后面<\/p>/);

  const ascii = await renderNoteMarkdownHtml("**bold.**next");
  assert.match(ascii, /<p>\*\*bold\.\*\*next<\/p>/);
  const spaced = await renderNoteMarkdownHtml("**粗体**后面");
  assert.match(spaced, /<p><strong>粗体<\/strong>后面<\/p>/);
});

test("published notes keep emphasis, links, and code, and hide frontmatter", async () => {
  const notesDir = fileURLToPath(new URL("../src/content/notes/", import.meta.url));
  const sdf = readFileSync(new URL("../src/content/notes/n-20261017-205600.md", import.meta.url), "utf8");
  const body = stripNoteFrontmatter(sdf);
  assert.equal(body.startsWith("---"), false);
  assert.equal(body.includes("title:"), false);

  const html = await renderNoteMarkdownHtml(body);
  assert.match(
    html,
    /<strong>有向距离场（Signed Distance Field，SDF）加球面追踪（Sphere Tracing）<\/strong>/,
  );
  assert.match(html, /<a href="https:\/\/iquilezles\.org\/articles\/distfunctions\/">距离函数列表<\/a>/);
  assert.match(html, /<code>code\/2026-10-17-sdf_sphere_tracing\.py<\/code>/);
  assert.match(html, /data-language="glsl"/);
  assert.match(html, /sdRoundBox/);
  assert.equal(html.includes("title:"), false);
  assert.equal(html.includes("tags:"), false);

  const leftovers: string[] = [];
  for (const file of readdirSync(notesDir).filter((name) => name.endsWith(".md"))) {
    const markdown = stripNoteFrontmatter(readFileSync(join(notesDir, file), "utf8"));
    const rendered = await renderNoteMarkdownHtml(markdown);
    const prose = rendered
      .replace(/<pre[\s\S]*?<\/pre>/g, " ")
      .replace(/<code[\s\S]*?<\/code>/g, " ")
      .replace(/<annotation[\s\S]*?<\/annotation>/g, " ")
      .replace(/<[^>]+>/g, "");
    if (prose.includes("**") || prose.includes("~~")) {
      leftovers.push(file);
    }
    assert.equal(rendered.includes("\ntitle:"), false);
  }
  assert.deepEqual(leftovers, []);
});
