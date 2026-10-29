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

/**
 * 换页时回到文档顶部。不记忆上一页滚动位置。
 */
export function scrollPageToTop() {
  if (pageLenis) {
    pageLenis.scrollTo(0, { immediate: true, force: true });
  }
  window.scrollTo(0, 0);
  document.documentElement.scrollTop = 0;
  document.body.scrollTop = 0;
}

/**
 * 滚到笔记标题。顶栏偏移写在标题的 scroll-margin-top 上。
 * Lenis 滚到元素时会读这个值；Lenis 未启动时按同一个已用值即时定位。
 * 调用方要求即时，或系统偏好减动效时，也不做平滑滚动。
 */
export function scrollPageToElement(element: HTMLElement, immediate = false) {
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const instant = immediate || reduced;
  if (pageLenis) {
    // 正文异步插入后，Lenis 的可滚高度可能还停在插入前，直接滚会被夹短。
    pageLenis.resize();
    pageLenis.scrollTo(element, { immediate: instant, force: true });
    return;
  }
  const margin = Number.parseFloat(getComputedStyle(element).scrollMarginTop);
  const top = window.scrollY + element.getBoundingClientRect().top - (Number.isFinite(margin) ? margin : 0);
  window.scrollTo({ top: Math.max(0, top), behavior: "auto" });
}

export function useLenis() {
  useEffect(() => {
    history.scrollRestoration = "manual";
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
