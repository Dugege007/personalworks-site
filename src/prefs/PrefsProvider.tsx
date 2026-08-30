import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { findDefaultSkin, findEnabledSkin, listEnabledSkins } from "../content/prefs";
import {
  applySkinToDom,
  loadResolvedPrefs,
  readPrefs,
  resolveSkinId,
  writePrefs,
} from "./storage";
import { tx } from "./tx";
import { PREFS_KEY, type Locale, type PrefsV1, type SkinId, type SkinRecord } from "./types";

type PrefsContextValue = {
  skin: SkinId;
  locale: Locale;
  enabledSkins: SkinRecord[];
  currentSkin: SkinRecord;
  setSkin: (id: SkinId) => void;
  skinName: (record: SkinRecord) => string;
};

const PrefsContext = createContext<PrefsContextValue | null>(null);

type PrefsProviderProps = {
  children: ReactNode;
};

/**
 * 应用层偏好：挂载后校验本地皮肤 id，并在多标签间同步。
 */
export function PrefsProvider({ children }: PrefsProviderProps) {
  const [prefs, setPrefs] = useState<PrefsV1>(() => {
    const resolved = loadResolvedPrefs();
    applySkinToDom(resolved.skin);
    return resolved;
  });

  useEffect(() => {
    applySkinToDom(prefs.skin);
  }, [prefs.skin]);

  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key !== PREFS_KEY) {
        return;
      }
      const next = readPrefs();
      if (!next) {
        return;
      }
      const skin = resolveSkinId(next.skin);
      setPrefs((current) => {
        const merged: PrefsV1 = { ...current, ...next, skin };
        applySkinToDom(merged.skin);
        return merged;
      });
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const setSkin = useCallback((id: SkinId) => {
    const skin = resolveSkinId(id);
    setPrefs((current) => {
      const next: PrefsV1 = { ...current, skin };
      writePrefs(next);
      applySkinToDom(skin);
      return next;
    });
  }, []);

  const enabledSkins = useMemo(() => listEnabledSkins(), []);
  const currentSkin = useMemo(
    () => findEnabledSkin(prefs.skin) ?? findDefaultSkin(),
    [prefs.skin],
  );

  const value = useMemo<PrefsContextValue>(
    () => ({
      skin: prefs.skin,
      locale: prefs.locale,
      enabledSkins,
      currentSkin,
      setSkin,
      skinName: (record) => tx(record.name, prefs.locale),
    }),
    [prefs.skin, prefs.locale, enabledSkins, currentSkin, setSkin],
  );

  return <PrefsContext.Provider value={value}>{children}</PrefsContext.Provider>;
}

/**
 * 读取站点偏好。必须在 PrefsProvider 内使用。
 */
export function usePrefs(): PrefsContextValue {
  const value = useContext(PrefsContext);
  if (!value) {
    throw new Error("usePrefs 必须在 PrefsProvider 内调用");
  }
  return value;
}
