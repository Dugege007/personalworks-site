/**
 * 开发用皮肤封面截图：将视口调为 1:1 后截取首页，再恢复原比例。
 * 新皮肤确认后再跑，不进入访客运行时。
 *
 * 用法：npm run shot:cover -- strata
 * 可选：--url http://localhost:5173 --size 900 --path /
 *
 * 依赖：npm i -D playwright && npx playwright install chromium
 */

import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const DEFAULT_URL = "http://localhost:5173";
const DEFAULT_SIZE = 900;
const DEFAULT_PATH = "/";
const ORIGINAL_VIEWPORT = { width: 1280, height: 720 };

function parseArgs(argv) {
  const args = { id: "", url: DEFAULT_URL, size: DEFAULT_SIZE, path: DEFAULT_PATH };
  const rest = argv.slice(2);
  for (let i = 0; i < rest.length; i += 1) {
    const token = rest[i];
    if (token === "--url") {
      args.url = rest[i + 1] ?? args.url;
      i += 1;
      continue;
    }
    if (token === "--size") {
      args.size = Number(rest[i + 1] ?? args.size);
      i += 1;
      continue;
    }
    if (token === "--path") {
      args.path = rest[i + 1] ?? args.path;
      i += 1;
      continue;
    }
    if (!token.startsWith("--") && !args.id) {
      args.id = token;
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

async function main() {
  const args = parseArgs(process.argv);
  if (!args.id) {
    console.error("缺少皮肤 id。用法：npm run shot:cover -- strata");
    process.exit(1);
  }
  if (!Number.isFinite(args.size) || args.size < 320) {
    console.error("截图边长无效，须为不小于 320 的数字。");
    process.exit(1);
  }

  const playwright = await loadPlaywright();
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const outFile = resolve(root, "public", "skins", `${args.id}-cover.png`);
  const target = new URL(args.path, args.url).href;

  await mkdir(dirname(outFile), { recursive: true });

  const browser = await playwright.chromium.launch({ headless: false });
  const page = await browser.newPage({ viewport: ORIGINAL_VIEWPORT });

  try {
    await page.goto(target, { waitUntil: "networkidle" });
    await page.evaluate(() => document.fonts.ready);
    await page.setViewportSize({ width: args.size, height: args.size });
    await page.waitForTimeout(600);
    const buffer = await page.screenshot({ type: "png" });
    await page.setViewportSize(ORIGINAL_VIEWPORT);
    await writeFile(outFile, buffer);
    console.log(`已写入 ${outFile}`);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`截图失败：${message}`);
    console.error("请确认已执行 npm run dev，且目标页可打开。");
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
}

main();
