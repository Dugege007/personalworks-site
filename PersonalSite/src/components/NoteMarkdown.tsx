import { useEffect, useRef, useState } from "react";
import rehypeReact from "rehype-react";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";
import type { JSX, MouseEvent, ReactNode } from "react";
import { assetUrl } from "../lib/assets";
import { noteDirectives } from "../notes/directives";

type NoteMarkdownProps = {
  markdown: string;
  pageHeading?: string;
};

type NoteCodeButtonProps = {
  className?: string | string[];
  title?: string;
  type?: string;
  data?: string;
  children?: ReactNode;
  onClick?: unknown;
};

function classNameOf(value: unknown): string {
  if (typeof value === "string") return value;
  if (Array.isArray(value)) return value.filter((item) => typeof item === "string").join(" ");
  return "";
}

/**
 * 读出围栏原文。优先用复制按钮上的 data，读不到时再从代码节点回退。
 */
function readCopyText(props: NoteCodeButtonProps, button: HTMLButtonElement): string {
  if (typeof props.data === "string") return props.data;
  const attr = button.getAttribute("data");
  if (attr) return attr;
  const code = button.closest("code");
  if (!code) return "";
  const clone = code.cloneNode(true);
  if (!(clone instanceof HTMLElement)) return "";
  clone.querySelectorAll("button, style").forEach((node) => node.remove());
  return clone.textContent ?? "";
}

async function writeClipboard(text: string): Promise<boolean> {
  if (!text) return false;
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    try {
      const area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.top = "0";
      area.style.left = "-9999px";
      document.body.append(area);
      area.select();
      const ok = document.execCommand("copy");
      area.remove();
      return ok;
    } catch {
      return false;
    }
  }
}

/**
 * 代码块复制按钮。管线注入的 onclick 是 HTML 字符串，React 不会执行，这里改接剪贴板。
 * 其它按钮原样输出，避免以后正文里出现按钮时被吞掉。
 */
function NoteCodeButton(props: NoteCodeButtonProps) {
  const className = classNameOf(props.className);
  const isCopy = className.split(/\s+/).includes("rehype-pretty-copy");
  const [copied, setCopied] = useState(false);
  const timerRef = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    };
  }, []);

  if (!isCopy) {
    const type = props.type === "submit" || props.type === "reset" ? props.type : "button";
    return (
      <button type={type} className={className || undefined} title={props.title}>
        {props.children}
      </button>
    );
  }

  const onCopy = (event: MouseEvent<HTMLButtonElement>) => {
    const text = readCopyText(props, event.currentTarget);
    void writeClipboard(text).then((ok) => {
      if (!ok) return;
      setCopied(true);
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
      timerRef.current = window.setTimeout(() => {
        timerRef.current = null;
        setCopied(false);
      }, 3000);
    });
  };

  const label = copied ? "已复制" : "复制代码";
  return (
    <button
      type="button"
      className={copied ? `${className} rehype-pretty-copied` : className}
      title={label}
      aria-label={label}
      onClick={onCopy}
    >
      {props.children}
    </button>
  );
}

/**
 * 用 unified 管线渲染发布稿。图片对象键经 assetUrl 出站。
 * 公式使用 KaTeX。样式和解析器按需加载，避免首页带上字体和公式运行时。
 */
export function NoteMarkdown({ markdown, pageHeading }: NoteMarkdownProps) {
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
        const file = await createNoteMarkdownProcessor(pageHeading)
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
              // 宽表单独横向滚动，避免整页被表格撑出屏幕。
              table: (props: { children?: JSX.Element | JSX.Element[] }) => (
                <div className="archive-table-scroll">
                  <table>{props.children}</table>
                </div>
              ),
              button: (props: NoteCodeButtonProps) => <NoteCodeButton {...props} />,
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
  }, [markdown, pageHeading]);

  return (
    <div className="archive-body" data-directives={Object.keys(noteDirectives).length}>
      {node}
    </div>
  );
}
