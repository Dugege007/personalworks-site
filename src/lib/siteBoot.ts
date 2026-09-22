const FIND_MS = 2500;
const MEDIA_MS = 20000;
const PAGE_MEDIA_MS = 30000;
const BOOT_PREVIEW_MS = 4500;
const MEDIA_PROGRESS_FLOOR = 0.75;
const MEDIA_PROGRESS_SPAN = 0.23;

/**
 * 进站闸放到 100% 前，先等主栏里已挂上的图和视频都就绪。
 */
export function releaseSiteBootWhenFirstScreenReady() {
  const holdPreview =
    import.meta.env.DEV && new URLSearchParams(window.location.search).has("boot");
  const startedAt = performance.now();

  void (async () => {
    await waitFrames(2);
    await waitUntilPageMediaSettled();
    if (holdPreview) {
      const remain = BOOT_PREVIEW_MS - (performance.now() - startedAt);
      if (remain > 0) {
        await sleep(remain);
      }
    }
    window.__siteBoot?.ready();
  })();
}

/**
 * 进站闸仍在时不开始页内轮换，避免未进页就切图。
 */
export function whenSiteBootReleased(): Promise<void> {
  if (!document.documentElement.hasAttribute("data-boot")) {
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    const observer = new MutationObserver(() => {
      if (!document.documentElement.hasAttribute("data-boot")) {
        observer.disconnect();
        resolve();
      }
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-boot"],
    });
  });
}

function queryAllMainMedia(): Array<HTMLImageElement | HTMLVideoElement> {
  const main = document.querySelector("main");
  if (!main) {
    return [];
  }
  return [...main.querySelectorAll("img, video")].filter(isUsableMedia);
}

function isUsableMedia(node: Element | null): node is HTMLImageElement | HTMLVideoElement {
  if (node instanceof HTMLImageElement) {
    return Boolean(node.currentSrc || node.getAttribute("src"));
  }
  if (node instanceof HTMLVideoElement) {
    return Boolean(node.currentSrc || node.getAttribute("src") || node.poster);
  }
  return false;
}

function isMediaSettled(el: HTMLImageElement | HTMLVideoElement) {
  if (el instanceof HTMLImageElement) {
    return el.complete;
  }
  if (el.poster) {
    return true;
  }
  return el.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA;
}

function waitForPageMain(): Promise<void> {
  const ready = () => {
    const main = document.querySelector("main");
    return Boolean(main && main.childElementCount > 0);
  };
  if (ready()) {
    return Promise.resolve();
  }
  const root = document.getElementById("root") ?? document.body;
  return new Promise((resolve) => {
    const finish = () => {
      observer.disconnect();
      window.clearTimeout(timer);
      resolve();
    };
    const observer = new MutationObserver(() => {
      if (ready()) {
        finish();
      }
    });
    observer.observe(root, { childList: true, subtree: true });
    const timer = window.setTimeout(finish, FIND_MS);
  });
}

function markMediaProgress(done: number, total: number) {
  const safeTotal = Math.max(total, 1);
  window.__siteBoot?.mark(MEDIA_PROGRESS_FLOOR + MEDIA_PROGRESS_SPAN * (done / safeTotal));
}

/**
 * 等主栏里已挂上的图和视频都就绪，读数按完成比例走，不在中途封顶。
 */
async function waitUntilPageMediaSettled() {
  await waitForPageMain();
  await waitFrames(3);
  const deadline = performance.now() + PAGE_MEDIA_MS;
  const waited = new Set<HTMLImageElement | HTMLVideoElement>();
  while (performance.now() < deadline) {
    const list = queryAllMainMedia();
    if (list.length === 0) {
      return;
    }
    markMediaProgress(list.filter(isMediaSettled).length, list.length);
    await Promise.all(
      list.map(async (el) => {
        if (waited.has(el)) {
          return;
        }
        waited.add(el);
        await waitForMedia(el);
        const now = queryAllMainMedia();
        markMediaProgress(now.filter(isMediaSettled).length, now.length);
      }),
    );
    await waitFrames(2);
    const again = queryAllMainMedia();
    if (again.length > 0 && again.every(isMediaSettled)) {
      window.__siteBoot?.mark(0.98);
      return;
    }
  }
}

/**
 * 等该媒体下载并解码；失败或超时也放行，避免闸卡死。
 */
function waitForMedia(el: HTMLImageElement | HTMLVideoElement): Promise<void> {
  return Promise.race([settleMedia(el), sleep(MEDIA_MS)]);
}

async function settleMedia(el: HTMLImageElement | HTMLVideoElement) {
  if (el instanceof HTMLImageElement) {
    if (el.naturalWidth > 0) {
      if (typeof el.decode === "function") {
        await el.decode().catch(() => undefined);
      }
      return;
    }
    const src = el.currentSrc || el.src;
    if (el.loading === "lazy") {
      await waitForImageSrc(src);
    }
    if (el.naturalWidth === 0 && !el.complete) {
      await once(el, "load", "error");
    }
    if (typeof el.decode === "function" && el.naturalWidth > 0) {
      await el.decode().catch(() => undefined);
    }
    return;
  }
  if (el.poster) {
    await waitForImageSrc(el.poster);
    return;
  }
  if (el.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
    return;
  }
  await once(el, "loadeddata", "error");
}

function waitForImageSrc(src: string): Promise<void> {
  if (!src) {
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    const img = new Image();
    const done = () => resolve();
    img.onload = done;
    img.onerror = done;
    img.src = src;
    if (img.complete && img.naturalWidth > 0) {
      done();
    }
  });
}

function once(el: EventTarget, ok: string, fail: string): Promise<void> {
  return new Promise((resolve) => {
    const done = () => {
      el.removeEventListener(ok, done);
      el.removeEventListener(fail, done);
      resolve();
    };
    el.addEventListener(ok, done);
    el.addEventListener(fail, done);
  });
}

function waitFrames(count: number): Promise<void> {
  return new Promise((resolve) => {
    const step = (left: number) => {
      if (left <= 0) {
        resolve();
        return;
      }
      requestAnimationFrame(() => step(left - 1));
    };
    step(count);
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}
