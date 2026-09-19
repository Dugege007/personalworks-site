import { useEffect, useState } from "react";
import { profile } from "../content/site";

let legalShown = false;
const listenerList: Array<() => void> = [];

/**
 * 页面展示名：默认曾用名，点击切到身份证现用名，再点切回。各处共用同一状态。
 */
export function useProfileName() {
  const [shown, setShown] = useState(legalShown);

  useEffect(() => {
    const sync = () => setShown(legalShown);
    listenerList.push(sync);
    return () => {
      const index = listenerList.indexOf(sync);
      if (index >= 0) {
        listenerList.splice(index, 1);
      }
    };
  }, []);

  /**
   * 在曾用名与身份证现用名之间切换。
   */
  function toggle() {
    legalShown = !legalShown;
    for (const listener of listenerList) {
      listener();
    }
  }

  return {
    displayName: shown ? profile.legalName : profile.name,
    toggle,
  };
}
