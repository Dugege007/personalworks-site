type NavMarkName = "prev" | "next" | "close";

const PATHS: Record<NavMarkName, string[]> = {
  prev: ["m15 18-6-6 6-6"],
  next: ["m9 18 6-6-6-6"],
  close: ["M18 6 6 18", "m6 6 12 12"],
};

/**
 * 灯箱圆钮与图条翻页共用的 Lucide 线框标，几何居中，不用字符。
 */
export function NavMark({ name }: { name: NavMarkName }) {
  return (
    <svg
      className="lrb-nav-mark"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {PATHS[name].map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}
