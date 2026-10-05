/**
 * 跟在 `[文字](地址)` 后面，表示这段文字会打开网页。
 */
export function JumpLinkIcon() {
  return (
    <svg className="jump-link-icon" viewBox="0 0 12 12" width="1em" height="1em" aria-hidden="true">
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.15"
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M5.1 2.4H2.6v7h7V6.9"
      />
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.15"
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M6.4 5.6 9.6 2.4M7.15 2.4H9.6V4.85"
      />
    </svg>
  );
}
