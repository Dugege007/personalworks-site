import { useEffect, useRef, useState } from "react";
import type { MouseEvent } from "react";
import { scrollPageToElement } from "../hooks/useLenis";
import {
  buildNoteToc,
  defaultCollapsedTocIds,
  noteTocSignature,
  type NoteTocDepth,
  type NoteTocHeading,
  type NoteTocNode,
} from "../notes/toc";

type NoteTocProps = {
  resetKey: string;
};

const narrowQuery = "(max-width: 860px)";

/**
 * 目录用可见文字。KaTeX 同时输出 MathML 和视觉层，只保留视觉层，避免公式出现两遍。
 */
function headingText(element: HTMLElement): string {
  const clone = element.cloneNode(true);
  if (!(clone instanceof HTMLElement)) return "";
  clone.querySelectorAll(".katex-mathml").forEach((node) => node.remove());
  return clone.textContent?.replace(/\s+/g, " ").trim() ?? "";
}

/**
 * 从已渲染正文收集 h2–h4。没有 id 或没有文字的标题不进目录。
 */
function readHeadings(body: Element): NoteTocHeading[] {
  const headings: NoteTocHeading[] = [];
  body.querySelectorAll("h2, h3, h4").forEach((node) => {
    if (!(node instanceof HTMLElement)) return;
    const depth = Number(node.tagName.slice(1));
    if (depth !== 2 && depth !== 3 && depth !== 4) return;
    const text = headingText(node);
    if (!node.id || !text) return;
    headings.push({ id: node.id, text, depth: depth as NoteTocDepth });
  });
  return headings;
}

/**
 * 取地址栏哈希对应的标题 id。非法百分号编码时保留原文。
 */
function locationHashId(): string {
  const raw = window.location.hash.replace(/^#/, "");
  if (!raw) return "";
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

/**
 * 当前节取激活线以上的最后一个标题。激活线在固定顶栏下方。
 */
function chooseCurrentHeading(elements: HTMLElement[]): string | null {
  if (elements.length === 0) return null;
  const header = document.querySelector("header.header");
  const line = (header instanceof HTMLElement ? header.getBoundingClientRect().height : 64) + 12;
  let current = elements[0]?.id ?? null;
  for (const element of elements) {
    if (element.getBoundingClientRect().top <= line + 1) current = element.id;
    else break;
  }
  return current;
}

/**
 * 把当前标题的祖先从折叠集合里去掉，便于目录里看见高亮项。
 */
function revealAncestors(collapsed: Set<string>, nodes: NoteTocNode[], activeId: string): Set<string> {
  const walk = (list: NoteTocNode[], ancestors: string[]): boolean => {
    for (const node of list) {
      if (node.id === activeId) {
        ancestors.forEach((id) => collapsed.delete(id));
        return true;
      }
      if (walk(node.children, [...ancestors, node.id])) return true;
    }
    return false;
  };
  walk(nodes, []);
  return collapsed;
}

function focusHeading(element: HTMLElement) {
  if (!element.hasAttribute("tabindex")) element.setAttribute("tabindex", "-1");
  element.focus({ preventScroll: true });
}

type TocListProps = {
  nodes: NoteTocNode[];
  collapsed: Set<string>;
  activeId: string | null;
  onToggle: (id: string) => void;
  onSelect: (event: MouseEvent<HTMLAnchorElement>, id: string) => void;
};

function NoteTocList({ nodes, collapsed, activeId, onToggle, onSelect }: TocListProps) {
  return (
    <ol className="note-toc-list">
      {nodes.map((node) => {
        const hasChildren = node.children.length > 0;
        const isCollapsed = collapsed.has(node.id);
        return (
          <li key={node.id} className={`note-toc-item is-depth-${node.depth}`}>
            <div className="note-toc-row">
              <a
                href={`#${node.id}`}
                data-toc-id={node.id}
                aria-current={activeId === node.id ? "location" : undefined}
                onClick={(event) => onSelect(event, node.id)}
              >
                {node.text}
              </a>
              {hasChildren ? (
                <button
                  type="button"
                  className="note-toc-toggle"
                  aria-expanded={!isCollapsed}
                  aria-label={isCollapsed ? `展开「${node.text}」的下级标题` : `折叠「${node.text}」的下级标题`}
                  onClick={() => onToggle(node.id)}
                />
              ) : null}
            </div>
            {hasChildren && !isCollapsed ? (
              <NoteTocList
                nodes={node.children}
                collapsed={collapsed}
                activeId={activeId}
                onToggle={onToggle}
                onSelect={onSelect}
              />
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}

/**
 * 笔记目录。宽屏贴在正文右侧；窄屏改为正文前的折叠面板。
 * 标题从已渲染的 .archive-body 读取。没有 h2–h4 时不占位。
 */
export function NoteToc({ resetKey }: NoteTocProps) {
  const slotRef = useRef<HTMLDivElement>(null);
  const navRef = useRef<HTMLElement>(null);
  const [nodes, setNodes] = useState<NoteTocNode[]>([]);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [activeId, setActiveId] = useState<string | null>(null);
  const [narrow, setNarrow] = useState(() => window.matchMedia(narrowQuery).matches);
  const [open, setOpen] = useState(false);
  const listId = `note-toc-${resetKey}`;

  useEffect(() => {
    setOpen(false);
  }, [resetKey]);

  useEffect(() => {
    const media = window.matchMedia(narrowQuery);
    const sync = () => setNarrow(media.matches);
    sync();
    media.addEventListener("change", sync);
    return () => media.removeEventListener("change", sync);
  }, []);

  useEffect(() => {
    const sheet = slotRef.current?.parentElement;
    const body = sheet?.querySelector(".archive-body");
    if (!body) return;

    const sync = () => {
      const next = buildNoteToc(readHeadings(body));
      setNodes((current) => (noteTocSignature(current) === noteTocSignature(next) ? current : next));
    };

    sync();
    const observer = new MutationObserver(sync);
    observer.observe(body, { childList: true, subtree: true });
    return () => {
      observer.disconnect();
    };
  }, [resetKey]);

  useEffect(() => {
    if (nodes.length === 0) return;
    const id = locationHashId();
    const body = slotRef.current?.parentElement?.querySelector(".archive-body");
    const target = id ? document.getElementById(id) : null;
    if (!target || !body?.contains(target)) return;
    let stopped = false;
    const stop = () => {
      stopped = true;
    };
    const align = () => {
      if (stopped || !target.isConnected) return;
      scrollPageToElement(target, true);
    };
    align();
    focusHeading(target);
    window.addEventListener("wheel", stop, { passive: true, once: true });
    window.addEventListener("touchstart", stop, { passive: true, once: true });
    const images = [...body.querySelectorAll("img")].filter((img) => !img.complete);
    images.forEach((img) => {
      img.addEventListener("load", align, { once: true });
      img.addEventListener("error", align, { once: true });
    });
    return () => {
      stopped = true;
      window.removeEventListener("wheel", stop);
      window.removeEventListener("touchstart", stop);
      images.forEach((img) => {
        img.removeEventListener("load", align);
        img.removeEventListener("error", align);
      });
    };
  }, [nodes]);

  useEffect(() => {
    setCollapsed(new Set(defaultCollapsedTocIds(nodes)));
  }, [nodes]);

  useEffect(() => {
    if (!activeId) return;
    setCollapsed((current) => {
      const next = revealAncestors(new Set(current), nodes, activeId);
      if (next.size === current.size) {
        for (const id of next) {
          if (!current.has(id)) return next;
        }
        return current;
      }
      return next;
    });
  }, [activeId, nodes]);

  useEffect(() => {
    const sheet = slotRef.current?.parentElement;
    const body = sheet?.querySelector(".archive-body");
    if (!body || nodes.length === 0) {
      setActiveId(null);
      return;
    }
    const elements = [...body.querySelectorAll<HTMLElement>("h2[id], h3[id], h4[id]")];
    if (elements.length === 0) return;
    let frame = 0;
    const publish = () => {
      if (frame) return;
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        const current = chooseCurrentHeading(elements);
        setActiveId((previous) => (previous === current ? previous : current));
      });
    };
    const header = document.querySelector("header.header");
    const top = Math.round((header instanceof HTMLElement ? header.getBoundingClientRect().height : 64) + 12);
    // 标题越过顶栏下方的激活线时观察器会回调。平滑滚动停在带内时可能不再跨越边界，滚动事件补最后一帧。
    const observer = new IntersectionObserver(publish, {
      root: null,
      rootMargin: `-${top}px 0px -55% 0px`,
      threshold: [0, 1],
    });
    elements.forEach((element) => observer.observe(element));
    publish();
    window.addEventListener("scroll", publish, { passive: true });
    return () => {
      observer.disconnect();
      window.removeEventListener("scroll", publish);
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, [nodes]);

  useEffect(() => {
    const nav = navRef.current;
    if (!nav || !activeId || narrow) return;
    const link = nav.querySelector<HTMLElement>(`[data-toc-id="${CSS.escape(activeId)}"]`);
    if (!link) return;
    const navBox = nav.getBoundingClientRect();
    const linkBox = link.getBoundingClientRect();
    if (linkBox.top < navBox.top + 4) nav.scrollTop -= navBox.top - linkBox.top + 8;
    else if (linkBox.bottom > navBox.bottom - 4) nav.scrollTop += linkBox.bottom - navBox.bottom + 8;
  }, [activeId, narrow, collapsed, nodes]);

  useEffect(() => {
    const nav = navRef.current;
    if (!nav) return;
    const sync = () => {
      const canScroll = !narrow && nav.scrollHeight > nav.clientHeight + 1;
      if (canScroll) nav.setAttribute("data-lenis-prevent", "");
      else nav.removeAttribute("data-lenis-prevent");
    };
    sync();
    const observer = new ResizeObserver(sync);
    observer.observe(nav);
    return () => {
      observer.disconnect();
      nav.removeAttribute("data-lenis-prevent");
    };
  }, [narrow, nodes, collapsed, open]);

  const onToggle = (id: string) => {
    setCollapsed((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const onSelect = (event: MouseEvent<HTMLAnchorElement>, id: string) => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const body = slotRef.current?.parentElement?.querySelector(".archive-body");
    const target = document.getElementById(id);
    if (!body || !(target instanceof HTMLElement) || !body.contains(target)) return;
    event.preventDefault();
    const url = new URL(window.location.href);
    url.hash = id;
    window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
    // 窄屏先收起目录，下一帧再滚，避免面板高度变化把标题顶离激活线。
    if (narrow) {
      setOpen(false);
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => {
          if (!target.isConnected) return;
          scrollPageToElement(target);
          focusHeading(target);
        });
      });
      return;
    }
    scrollPageToElement(target);
    focusHeading(target);
  };

  const showList = nodes.length > 0 && (!narrow || open);

  return (
    <div
      ref={slotRef}
      className={nodes.length === 0 ? "note-toc-anchor" : "note-toc-slot"}
      hidden={nodes.length === 0}
    >
      {nodes.length === 0 ? null : (
        <nav ref={navRef} className={narrow ? "note-toc is-panel" : "note-toc"} aria-label="本篇目录">
          {narrow ? (
            <button
              type="button"
              className="note-toc-summary"
              aria-expanded={open}
              aria-controls={listId}
              onClick={() => setOpen((current) => !current)}
            >
              本篇目录
            </button>
          ) : (
            <p className="note-toc-label">本篇目录</p>
          )}
          {showList ? (
            <div id={listId}>
              <NoteTocList
                nodes={nodes}
                collapsed={collapsed}
                activeId={activeId}
                onToggle={onToggle}
                onSelect={onSelect}
              />
            </div>
          ) : null}
        </nav>
      )}
    </div>
  );
}
