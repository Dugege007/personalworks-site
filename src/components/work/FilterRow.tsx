type FilterRowProps = {
  label: string;
  values: string[];
  selected: string[];
  captions?: Record<string, string>;
  onToggle: (value: string) => void;
};

/**
 * 一行筛选项，形态接近视频站的分类条。
 */
export function FilterRow({ label, values, selected, captions, onToggle }: FilterRowProps) {
  if (values.length === 0) {
    return null;
  }
  return (
    <div className="filter-row">
      <span className="filter-row-label">{label}</span>
      <div className="filter-chips">
        {values.map((value) => {
          const on = selected.includes(value);
          return (
            <button
              key={value}
              type="button"
              className={on ? "filter-chip is-on" : "filter-chip"}
              aria-pressed={on}
              onClick={() => onToggle(value)}
            >
              {captions?.[value] ?? value}
            </button>
          );
        })}
      </div>
    </div>
  );
}
