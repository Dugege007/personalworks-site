export const COPY_PLACEHOLDER = "待填写描述";

/**
 * 去空白后有字，且不是历史占位句。
 */
export function isCopyFilled(text?: string | null): boolean {
  const trimmed = (text ?? "").trim();
  return trimmed.length > 0 && trimmed !== COPY_PLACEHOLDER;
}

/**
 * 栏目 / 项目导语：已填才返回，未填不渲染。
 */
export function resolveLead(text?: string | null): string | undefined {
  return isCopyFilled(text) ? text!.trim() : undefined;
}

/**
 * 资源名称：已填用自定义名，否则用原来的 label。
 */
export function resolveMediaDisplayName(media: {
  displayName?: string | null;
  label: string;
}): string {
  return isCopyFilled(media.displayName) ? media.displayName!.trim() : media.label;
}

/**
 * 资源导语：已填用资源描述，否则回退项目描述，都未填则不显示。
 */
export function resolveMediaDescription(
  media: { description?: string | null },
  projectDescription?: string | null,
): string | undefined {
  if (isCopyFilled(media.description)) {
    return media.description!.trim();
  }
  return resolveLead(projectDescription);
}
