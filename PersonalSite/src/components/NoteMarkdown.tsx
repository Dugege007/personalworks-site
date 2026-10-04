import { useEffect, useState } from "react";
import { unified } from "unified";
import remarkParse from "remark-parse";
import remarkGfm from "remark-gfm";
import remarkRehype from "remark-rehype";
import rehypeSlug from "rehype-slug";
import rehypeSanitize from "rehype-sanitize";
import rehypePrettyCode from "rehype-pretty-code";
import rehypeReact from "rehype-react";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";
import type { JSX } from "react";
import { assetUrl } from "../lib/assets";
import { noteDirectives } from "../notes/directives";

type NoteMarkdownProps = {
  markdown: string;
};

/**
 * 用 unified 管线渲染发布稿。图片对象键经 assetUrl 出站。
 */
export function NoteMarkdown({ markdown }: NoteMarkdownProps) {
  const [node, setNode] = useState<JSX.Element | null>(null);

  useEffect(() => {
    let cancelled = false;
    void unified()
      .use(remarkParse)
      .use(remarkGfm)
      .use(remarkRehype, { allowDangerousHtml: true })
      .use(rehypeSanitize)
      .use(rehypeSlug)
      .use(rehypePrettyCode, { theme: "github-light" })
      .use(rehypeReact, {
        Fragment,
        jsx,
        jsxs,
        components: {
          img: (props: { src?: string; alt?: string }) => {
            if (!props.src) {
              return null;
            }
            return <img src={assetUrl(props.src)} alt={props.alt ?? ""} />;
          },
        },
      })
      .process(markdown)
      .then((file: { result: unknown }) => {
        if (!cancelled) {
          setNode(file.result as JSX.Element);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [markdown]);

  return (
    <div className="archive-body" data-directives={Object.keys(noteDirectives).length}>
      {node}
    </div>
  );
}
