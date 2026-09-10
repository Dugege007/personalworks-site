import { useEffect } from "react";
import Lenis from "lenis";
import gsap from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";

gsap.registerPlugin(ScrollTrigger);

let pageLenis: Lenis | null = null;

/**
 * 指针停在需接管滚轮的区域时暂停整页 Lenis；离开后恢复。
 */
export function pausePageLenis() {
  pageLenis?.stop();
}

export function resumePageLenis() {
  pageLenis?.start();
}

export function useLenis() {
  useEffect(() => {
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced) {
      return;
    }

    const lenis = new Lenis({
      autoRaf: false,
      lerp: 0.08,
    });
    pageLenis = lenis;

    const onScroll = () => {
      ScrollTrigger.update();
    };
    lenis.on("scroll", onScroll);

    const ticker = (time: number) => {
      lenis.raf(time * 1000);
    };
    gsap.ticker.add(ticker);
    gsap.ticker.lagSmoothing(0);

    return () => {
      gsap.ticker.remove(ticker);
      lenis.off("scroll", onScroll);
      lenis.destroy();
      if (pageLenis === lenis) {
        pageLenis = null;
      }
    };
  }, []);
}
