type FilterRowProps = {
  label: string;
  values: string[];
  selected: string[];
  disabled?: readonly string[];
  captions?: Record<string, string>;
  onToggle: (value: string) => void;
};

/**
 * 一行筛选项，形态接近视频站的分类条。没有作品的取值置灰且不可再选。
 */
export function FilterRow({ label, values, selected, disabled, captions, onToggle }: FilterRowProps) {
  if (values.length === 0) {
    return null;
  }
  return (
    <div className="filter-row">
      <span className="filter-row-label">{label}</span>
      <div className="filter-chips">
        {values.map((value) => {
          const on = selected.includes(value);
          const empty = !on && (disabled?.includes(value) ?? false);
          return (
            <button
              key={value}
              type="button"
              className={on ? "filter-chip is-on" : "filter-chip"}
              aria-pressed={on}
              disabled={empty}
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
