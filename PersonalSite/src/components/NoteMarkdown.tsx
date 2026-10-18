import { useEffect, useState } from "react";
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
 * 公式使用 KaTeX。样式和解析器按需加载，避免首页带上字体和公式运行时。
 */
export function NoteMarkdown({ markdown }: NoteMarkdownProps) {
  const [node, setNode] = useState<JSX.Element | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const [{ createNoteMarkdownProcessor, normalizeTexDelimiters }] = await Promise.all([
          import("../notes/renderMarkdown"),
          import("katex/dist/katex.min.css"),
        ]);
        if (cancelled) {
          return;
        }
        const file = await createNoteMarkdownProcessor()
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
          .process(normalizeTexDelimiters(markdown));
        if (!cancelled) {
          setNode(file.result as JSX.Element);
        }
      } catch (error) {
        console.error("心得正文渲染失败。", error);
        if (!cancelled) {
          setNode(null);
        }
      }
    })();
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
