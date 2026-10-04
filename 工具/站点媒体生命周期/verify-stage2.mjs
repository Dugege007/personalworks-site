/**
 * 阶段2 隔离验收：引用未清零不得删；清零后删正式位并改台账；不打生产 COS。
 * 在 工具/站点媒体生命周期 目录执行：node verify-stage2.mjs
 */

import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const CLI = path.join(TOOL_DIR, "sitemedia.mjs");

const STAGE_REL =
  "景观施工图（landscape-cds）/上海道田景观工程咨询有限公司/201709 世茂北京一渡/01-园施 PDF/04-LG通图PDF/LG-10.01 通风井详图 A2.jpg";
const OBJECT = "landscape-cds/_sitemedia-test/lg-10-01.jpg";

function run(env, args) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [CLI, ...args], {
      cwd: TOOL_DIR,
      env: { ...process.env, ...env },
      windowsHide: true,
    });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
    });
    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
    });
    child.on("exit", (code) => {
      resolve({ code: code ?? 1, stdout, stderr });
    });
  });
}

function assert(cond, message) {
  if (!cond) throw new Error(message);
}

async function exists(abs) {
  try {
    await readFile(abs);
    return true;
  } catch {
    return false;
  }
}

async function main() {
  const root = await mkdtemp(path.join(os.tmpdir(), "sitemedia-stage2-"));
  const workspace = path.join(root, "ws");
  const env = {
    WORKSPACE_ROOT: workspace,
  };
  const stageAbs = path.join(workspace, "作品中转站", ...STAGE_REL.split("/"));
  const destAbs = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    ...OBJECT.split("/"),
  );
  const otherAbs = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    "landscape-cds",
    "_sitemedia-test",
    "other.jpg",
  );
  const ledgerPath = path.join(
    workspace,
    "PersonalSite",
    "src",
    "content",
    "media-ledger.json",
  );
  const worksPath = path.join(workspace, "PersonalSite", "src", "content", "works.ts");
  const sitePath = path.join(workspace, "PersonalSite", "src", "content", "site.ts");

  try {
    await mkdir(path.dirname(stageAbs), { recursive: true });
    await mkdir(path.dirname(destAbs), { recursive: true });
    await mkdir(path.dirname(ledgerPath), { recursive: true });
    await writeFile(ledgerPath, `${JSON.stringify({ version: 1, records: [] }, null, 2)}\n`);
    await writeFile(worksPath, `export const item = { src: "${OBJECT}" };\n`);
    await writeFile(sitePath, `export const again = { coverSrc: "${OBJECT}" };\n`);
    await writeFile(stageAbs, "stage-bytes");
    await writeFile(otherAbs, "other-bytes");

    const ingested = await run(env, ["ingest", STAGE_REL, "--object", OBJECT]);
    assert(ingested.code === 0, `ingest 应成功：${ingested.stderr}`);

    const blocked = await run(env, ["withdraw", OBJECT, "--apply"]);
    assert(blocked.code !== 0, "仍有引用时 --apply 应失败");
    assert(
      blocked.stderr.includes("内容层仍引用该对象"),
      `引用阻断报错不符：${blocked.stderr}`,
    );
    assert(await exists(destAbs), "引用未清零时正式位应仍在");
    assert(await exists(otherAbs), "引用未清零时其它正式位文件应仍在");
    const stillPublished = JSON.parse(await readFile(ledgerPath, "utf8"));
    assert(stillPublished.records[0]?.status === "published", "引用未清零时台账不得改");

    await writeFile(worksPath, "export const item = { src: \"landscape-cds/stock/01.webp\" };\n");
    const stillBlocked = await run(env, ["withdraw", OBJECT, "--apply"]);
    assert(stillBlocked.code !== 0, "去掉一处引用后 --apply 仍应失败");
    assert(await exists(destAbs), "复用未清零时正式位应仍在");

    await writeFile(sitePath, "export const again = { coverSrc: \"landscape-cds/stock/02.webp\" };\n");
    const applied = await run(env, ["withdraw", OBJECT, "--apply"]);
    assert(applied.code === 0, `--apply 应成功：${applied.stderr}`);
    assert(!(await exists(destAbs)), "清零后应删除正式位");
    assert(await exists(otherAbs), "不得删除其它对象文件");
    assert(await exists(stageAbs), "下架不得删除中转站文件");
    const withdrawn = JSON.parse(await readFile(ledgerPath, "utf8"));
    assert(withdrawn.records[0]?.status === "withdrawn", "台账应为 withdrawn");
    assert(applied.stdout.includes("待刷新 CDN"), "应打印 CDN 刷新清单");
    assert(!applied.stdout.toLowerCase().includes("prune-assets"), "不得调用 prune-assets");

    console.log("阶段2 隔离验收通过。");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
