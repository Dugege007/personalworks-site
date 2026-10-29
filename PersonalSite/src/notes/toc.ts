export type NoteTocDepth = 2 | 3 | 4;

export type NoteTocHeading = {
  id: string;
  text: string;
  depth: NoteTocDepth;
};

export type NoteTocNode = NoteTocHeading & {
  children: NoteTocNode[];
};

/**
 * 把正文标题收成树。h2 为根，h3 挂在最近的 h2 下，h4 挂在最近的 h3 下。
 * 缺少上级时，该标题自己作为根，避免目录丢段。
 */
export function buildNoteToc(headings: NoteTocHeading[]): NoteTocNode[] {
  const roots: NoteTocNode[] = [];
  let currentH2: NoteTocNode | null = null;
  let currentH3: NoteTocNode | null = null;

  for (const heading of headings) {
    const text = heading.text.trim();
    if (!heading.id || !text) continue;
    if (heading.depth !== 2 && heading.depth !== 3 && heading.depth !== 4) continue;
    const node: NoteTocNode = { id: heading.id, text, depth: heading.depth, children: [] };
    if (node.depth === 2) {
      roots.push(node);
      currentH2 = node;
      currentH3 = null;
      continue;
    }
    if (node.depth === 3) {
      if (currentH2) currentH2.children.push(node);
      else roots.push(node);
      currentH3 = node;
      continue;
    }
    if (currentH3) currentH3.children.push(node);
    else if (currentH2) currentH2.children.push(node);
    else roots.push(node);
  }

  return roots;
}

/**
 * 默认只展开到二级标题。有下级的节点先折叠，三级及以下要点开才出现。
 */
export function defaultCollapsedTocIds(nodes: NoteTocNode[]): string[] {
  const ids: string[] = [];
  const walk = (node: NoteTocNode) => {
    if (node.children.length > 0) ids.push(node.id);
    node.children.forEach(walk);
  };
  nodes.forEach(walk);
  return ids;
}

/**
 * 目录结构签名。标题集合没变时用来跳过折叠状态重置。
 */
export function noteTocSignature(nodes: NoteTocNode[]): string {
  const parts: string[] = [];
  const walk = (node: NoteTocNode) => {
    parts.push(`${node.depth}\t${node.id}\t${node.text}`);
    node.children.forEach(walk);
  };
  nodes.forEach(walk);
  return parts.join("\n");
}
