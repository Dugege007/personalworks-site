type NavMarkName = "prev" | "next" | "close" | "info";

const PATHS: Record<NavMarkName, string[]> = {
  prev: ["m15 18-6-6 6-6"],
  next: ["m9 18 6-6-6-6"],
  close: ["M18 6 6 18", "m6 6 12 12"],
  info: ["M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z", "M3 15h18"],
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
