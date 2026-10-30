/**
 * 按当前文稿导出一页 A4 PDF。打印页读取 `文稿/resume.md`，改文稿后重新执行即可。
 * 页内链接写成绝对地址；放不下时打印页会收行距，并压缩实习与自学。
 *
 * 用法：npm run export:resume-pdf
 * 可选：--url http://127.0.0.1:5173  复用已打开的开发服务，跳过构建
 *       --out artifacts/resume/自定义.pdf
 *       --skip-build  仅在已有 dist 时使用
 *
 * 依赖：npm i -D playwright && npx playwright install chromium
 * 中文：本机需有 Noto Sans SC，或系统中日韩字体（如 Noto Sans CJK SC）。
 */

import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { createServer } from "node:net";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const PRINT_PATH = "/profile-resume/print";
const OUTPUT_NAME = "杜宏博-Unity数字孪生-简历.pdf";
const A4_WIDTH = 595.28;
const A4_HEIGHT = 841.89;

function parseArgs(argv) {
  const args = { url: "", out: "", skipBuild: false };
  const rest = argv.slice(2);
  for (let i = 0; i < rest.length; i += 1) {
    const token = rest[i];
    if (token === "--url") {
      args.url = rest[i + 1] ?? args.url;
      i += 1;
      continue;
    }
    if (token === "--out") {
      args.out = rest[i + 1] ?? args.out;
      i += 1;
      continue;
    }
    if (token === "--skip-build") {
      args.skipBuild = true;
    }
  }
  return args;
}

async function loadPlaywright() {
  try {
    return await import("playwright");
  } catch {
    console.error("未安装 playwright。请先执行：npm i -D playwright && npx playwright install chromium");
    process.exit(1);
  }
}

function run(command, args, cwd) {
  return new Promise((resolveRun, rejectRun) => {
    const child = spawn(command, args, { cwd, stdio: "inherit" });
    child.on("error", rejectRun);
    child.on("exit", (code) => {
      if (code === 0) {
        resolveRun();
        return;
      }
      rejectRun(new Error(`${command} ${args.join(" ")} 退出码 ${code ?? "未知"}`));
    });
  });
}

function freePort() {
  return new Promise((resolvePort, rejectPort) => {
    const server = createServer();
    server.once("error", rejectPort);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      const port = address && typeof address === "object" ? address.port : 0;
      server.close(() => resolvePort(port));
    });
  });
}

async function waitForUrl(url, timeoutMs) {
  const started = Date.now();
  let lastError = "无响应";
  while (Date.now() - started < timeoutMs) {
    try {
      const response = await fetch(url, { redirect: "manual" });
      if (response.status < 500) {
        return;
      }
      lastError = `HTTP ${response.status}`;
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }
    await new Promise((resolveWait) => {
      setTimeout(resolveWait, 300);
    });
  }
  throw new Error(`预览服务未就绪：${url}（${lastError}）`);
}

function startPreview(root, port) {
  const viteBin = resolve(root, "node_modules", "vite", "bin", "vite.js");
  const child = spawn(
    process.execPath,
    [viteBin, "preview", "--host", "127.0.0.1", "--port", String(port), "--strictPort"],
    { cwd: root, detached: true, stdio: ["ignore", "pipe", "pipe"] },
  );
  let log = "";
  const append = (chunk) => {
    log += chunk.toString();
    if (log.length > 8000) {
      log = log.slice(-8000);
    }
  };
  child.stdout?.on("data", append);
  child.stderr?.on("data", append);
  return {
    child,
    log: () => log,
    stop() {
      if (child.pid) {
        try {
          process.kill(-child.pid, "SIGTERM");
        } catch {
          child.kill("SIGTERM");
        }
      }
      child.stdout?.destroy();
      child.stderr?.destroy();
      child.unref();
    },
  };
}

function countPdfPages(buffer) {
  const raw = buffer.toString("latin1");
  const pages = raw.match(/\/Type\s*\/Page(?!s)\b/g);
  if (pages && pages.length > 0) {
    return pages.length;
  }
  const counts = [...raw.matchAll(/\/Count\s+(\d+)/g)].map((match) => Number(match[1]));
  if (counts.length > 0) {
    return Math.max(...counts);
  }
  throw new Error("无法从 PDF 读取页数");
}

function readMediaBox(buffer) {
  const raw = buffer.toString("latin1");
  const match = raw.match(/\/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]/);
  if (!match) {
    return null;
  }
  return {
    width: Number(match[3]) - Number(match[1]),
    height: Number(match[4]) - Number(match[2]),
  };
}

function assertA4(box) {
  if (!box) {
    throw new Error("PDF 缺少 MediaBox，无法确认纸型");
  }
  const widthOk = Math.abs(box.width - A4_WIDTH) < 3;
  const heightOk = Math.abs(box.height - A4_HEIGHT) < 3;
  if (!widthOk || !heightOk) {
    throw new Error(`PDF 纸型不是 A4：${box.width.toFixed(2)} × ${box.height.toFixed(2)} pt`);
  }
}

async function main() {
  const args = parseArgs(process.argv);
  const playwright = await loadPlaywright();
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const outFile = args.out
    ? resolve(root, args.out)
    : resolve(root, "artifacts", "resume", OUTPUT_NAME);
  let preview;

  try {
    let base = args.url;
    if (!base) {
      if (!args.skipBuild) {
        await run("npm", ["run", "build"], root);
      }
      const port = await freePort();
      preview = startPreview(root, port);
      base = `http://127.0.0.1:${port}`;
      await waitForUrl(base, 30000);
    }

    const target = new URL(PRINT_PATH, base.endsWith("/") ? base : `${base}/`).href;
    await mkdir(dirname(outFile), { recursive: true });

    const browser = await playwright.chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 794, height: 1123 } });
    try {
      await page.emulateMedia({ media: "print" });
      await page.goto(target, { waitUntil: "load", timeout: 60000 });
      await page.waitForFunction(() => !document.documentElement.hasAttribute("data-boot"), null, {
        timeout: 15000,
      });
      await page.waitForFunction(() => {
        const fit = document.querySelector(".resume-print")?.getAttribute("data-fit");
        return fit === "done" || fit === "overflow";
      }, null, { timeout: 20000 });
      const fitState = await page.getAttribute(".resume-print", "data-fit");
      if (fitState === "overflow") {
        throw new Error("内容超过一页。已压缩行距、实习与自学经历，并依次去掉 Github 与个人主页后仍放不下。");
      }
      const fontState = await page.evaluate(async () => {
        const fonts = document.fonts;
        if (fonts?.ready) {
          await fonts.ready;
        }
        const load = async (spec) => {
          if (!fonts?.load) {
            return false;
          }
          try {
            await fonts.load(spec);
          } catch {
            return false;
          }
          return fonts.check(spec);
        };
        const sans = await load('400 16px "Noto Sans SC"');
        const cjk = await load('400 16px "Noto Sans CJK SC"');
        return { sans, cjk };
      });
      if (!fontState.sans && !fontState.cjk) {
        throw new Error("中文字体未加载。请安装 Noto Sans SC 或 Noto Sans CJK SC 后重试。");
      }

      const hrefs = await page.$$eval("a[href]", (nodes) => [
        ...new Set(
          nodes
            .map((node) => node.getAttribute("href") ?? "")
            .filter((href) => /^https?:/i.test(href)),
        ),
      ]);
      const fitMeta = await page.evaluate(() => {
        const el = document.querySelector(".resume-print");
        return {
          density: el?.getAttribute("data-density") ?? "0",
          compress: el?.getAttribute("data-compress") === "1",
        };
      });

      const buffer = await page.pdf({
        format: "A4",
        printBackground: false,
        preferCSSPageSize: true,
        displayHeaderFooter: false,
        margin: { top: "0", right: "0", bottom: "0", left: "0" },
      });
      const pageCount = countPdfPages(buffer);
      if (pageCount !== 1) {
        throw new Error(`PDF 页数应为 1，实际为 ${pageCount}`);
      }
      assertA4(readMediaBox(buffer));
      const raw = buffer.toString("latin1");
      const missingInFile = hrefs.filter((href) => !raw.includes(href));
      if (missingInFile.length > 0) {
        throw new Error(`PDF 内缺少可点击链接：${missingInFile.join("，")}`);
      }
      await writeFile(outFile, buffer);
      const kilo = (buffer.length / 1024).toFixed(1);
      const compressNote = fitMeta.compress ? "，已压缩实习与自学" : "";
      console.log(`已写入 ${outFile}（${kilo} KB，1 页 A4，行距档 ${fitMeta.density}${compressNote}）`);
      if (buffer.length > 2 * 1024 * 1024) {
        console.warn("文件超过 2MB，上传附件前请再压缩。");
      }
    } finally {
      await browser.close();
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`导出失败：${message}`);
    if (preview) {
      const log = preview.log().trim();
      if (log) {
        console.error(log);
      }
    }
    process.exitCode = 1;
  } finally {
    preview?.stop();
  }
  process.exit(process.exitCode ?? 0);
}

main();
