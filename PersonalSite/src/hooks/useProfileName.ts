import { useEffect, useState } from "react";
import {
  applyProfileDocumentTitle,
  readProfileNameMode,
  toggleProfileNameMode,
  writeProfileNameMode,
  type ProfileNameMode,
} from "../content/profileNameMode";
import { profile } from "../content/site";

let mode: ProfileNameMode = readProfileNameMode();
const listenerList: Array<() => void> = [];

function displayNameOf(current: ProfileNameMode): string {
  return current === "legal" ? profile.legalName : profile.name;
}

/**
 * 按本地记录把标签页标题设为当前姓名。入口在首屏脚本之后再校一次。
 */
export function syncProfileDocumentTitle(): void {
  applyProfileDocumentTitle(displayNameOf(mode));
}

function publish(next: ProfileNameMode): void {
  mode = next;
  writeProfileNameMode(next);
  applyProfileDocumentTitle(displayNameOf(next));
  for (const listener of listenerList) {
    listener();
  }
}

/**
 * 页面展示名：默认曾用名，点击切到现用名，再点切回。各处共用同一状态，并写入本地。
 */
export function useProfileName() {
  const [shown, setShown] = useState(mode);

  useEffect(() => {
    const sync = () => setShown(mode);
    listenerList.push(sync);
    setShown(mode);
    applyProfileDocumentTitle(displayNameOf(mode));
    return () => {
      const index = listenerList.indexOf(sync);
      if (index >= 0) {
        listenerList.splice(index, 1);
      }
    };
  }, []);

  /**
   * 在曾用名与现用名之间切换，并记住这次选择。
   */
  function toggle() {
    publish(toggleProfileNameMode(mode));
  }

  return {
    displayName: displayNameOf(shown),
    toggle,
  };
}
