import { useLocation, useNavigate } from "react-router-dom";
import { findDefaultSkin, findEnabledSkin } from "../content/prefs";
import { iaOfSkin } from "../ia";
import { resolvePathForSkin } from "../ia/resolve";
import { usePrefs } from "../prefs/PrefsProvider";

type SkinPickerProps = {
  open: boolean;
  onClose: () => void;
};

/**
 * 皮肤选择窗：色板卡片立即应用，封面缺失时仅展示色板。
 */
export function SkinPicker({ open, onClose }: SkinPickerProps) {
  const { skin, enabledSkins, setSkin, skinName } = usePrefs();
  const location = useLocation();
  const navigate = useNavigate();

  if (!open) {
    return null;
  }

  /**
   * 先写皮肤，再按目标 IA 映射路径；换 URL 时 replace，不堆历史。
   */
  function handlePick(id: string) {
    setSkin(id);
    const target = findEnabledSkin(id) ?? findDefaultSkin();
    const next = resolvePathForSkin(location.pathname, location.search, iaOfSkin(target).id);
    if (next.pathname !== location.pathname || next.search !== location.search) {
      navigate({ pathname: next.pathname, search: next.search }, { replace: true });
    }
  }

  return (
    <div className="skin-overlay" role="presentation">
      <button className="skin-scrim" type="button" aria-label="关闭皮肤选择" onClick={onClose} />
      <div
        className="skin-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="skin-picker-title"
      >
        <div className="skin-panel-head">
          <h2 id="skin-picker-title">选择皮肤</h2>
          <button className="header-btn" type="button" aria-label="关闭" onClick={onClose}>
            <span className="skin-close" aria-hidden="true" />
          </button>
        </div>
        <ul className="skin-card-list">
          {enabledSkins.map((record) => {
            const current = record.id === skin;
            return (
              <li key={record.id}>
                <button
                  className={`skin-card${current ? " is-current" : ""}`}
                  type="button"
                  aria-pressed={current}
                  onClick={() => handlePick(record.id)}
                >
                  {record.preview.cover ? (
                    <img className="skin-cover" src={record.preview.cover} alt="" />
                  ) : (
                    <span className="skin-swatches" aria-hidden="true">
                      {record.preview.swatches.map((color) => (
                        <i key={color} style={{ background: color }} />
                      ))}
                    </span>
                  )}
                  <span className="skin-card-meta">
                    <span className="skin-card-name">{skinName(record)}</span>
                    {current ? <span className="skin-card-flag">当前</span> : null}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}
