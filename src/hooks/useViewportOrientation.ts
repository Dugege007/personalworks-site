import { useEffect, useState } from "react";
import { readViewportOrientation, type PhotoOrientation } from "../content/photoOrientation";

/**
 * 跟踪窗口横竖。方屏时返回 square。
 */
export function useViewportOrientation(): PhotoOrientation {
  const [orientation, setOrientation] = useState<PhotoOrientation>(() =>
    typeof window === "undefined" ? "landscape" : readViewportOrientation(window.innerWidth, window.innerHeight),
  );

  useEffect(() => {
    const sync = () => setOrientation(readViewportOrientation(window.innerWidth, window.innerHeight));
    sync();
    window.addEventListener("resize", sync);
    const media = window.matchMedia("(orientation: landscape)");
    media.addEventListener("change", sync);
    return () => {
      window.removeEventListener("resize", sync);
      media.removeEventListener("change", sync);
    };
  }, []);

  return orientation;
}
