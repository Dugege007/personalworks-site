/**
 * 阶段4 隔离验收：默认同键再入库；--bump 生成 -vN 且旧键保持 withdrawn。
 * 在 工具/站点媒体生命周期 目录执行：node verify-stage4.mjs
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
const BUMPED = "landscape-cds/_sitemedia-test/lg-10-01-v2.jpg";

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
  const root = await mkdtemp(path.join(os.tmpdir(), "sitemedia-stage4-"));
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
  const bumpedAbs = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    ...BUMPED.split("/"),
  );
  const ledgerPath = path.join(
    workspace,
    "PersonalSite",
    "src",
    "content",
    "media-ledger.json",
  );
  const worksPath = path.join(workspace, "PersonalSite", "src", "content", "works.ts");

  try {
    await mkdir(path.dirname(stageAbs), { recursive: true });
    await mkdir(path.dirname(ledgerPath), { recursive: true });
    await writeFile(ledgerPath, `${JSON.stringify({ version: 1, records: [] }, null, 2)}\n`);
    await writeFile(worksPath, "export const sample = { src: \"landscape-cds/stock/01.webp\" };\n");
    await writeFile(stageAbs, "stage-bytes");

    const firstBump = await run(env, ["ingest", STAGE_REL, "--object", OBJECT, "--bump"]);
    assert(firstBump.code !== 0, "首次入库加 --bump 应失败");
    assert(firstBump.stderr.includes("首次入库不要加 --bump"), firstBump.stderr);

    const ingested = await run(env, ["ingest", STAGE_REL, "--object", OBJECT]);
    assert(ingested.code === 0, `首次 ingest 应成功：${ingested.stderr}`);

    const bumpPublished = await run(env, ["ingest", STAGE_REL, "--object", OBJECT, "--bump"]);
    assert(bumpPublished.code !== 0, "published 时 --bump 应失败");
    assert(bumpPublished.stderr.includes("withdrawn"), bumpPublished.stderr);

    const applied = await run(env, ["withdraw", OBJECT, "--apply"]);
    assert(applied.code === 0, applied.stderr);
    assert(await exists(stageAbs), "下架后中转站文件应仍在，再用不必 restage");

    const sameKey = await run(env, ["ingest", STAGE_REL, "--object", OBJECT]);
    assert(sameKey.code === 0, `同键再入库应成功：${sameKey.stderr}`);
    assert(await exists(destAbs), "默认同键应写回原 object");
    assert(!(await exists(bumpedAbs)), "未加 --bump 不得生成 -v2");
    let ledger = JSON.parse(await readFile(ledgerPath, "utf8"));
    assert(ledger.records.length === 1, "同键再入库应仍是一条台账");
    assert(ledger.records[0].object === OBJECT, "同键 object 不变");
    assert(ledger.records[0].status === "published", "同键再入库应为 published");

    await run(env, ["withdraw", OBJECT, "--apply"]);

    const bumped = await run(env, ["ingest", STAGE_REL, "--object", OBJECT, "--bump"]);
    assert(bumped.code === 0, `--bump 应成功：${bumped.stderr}`);
    assert(bumped.stdout.includes(BUMPED), `应打印新键 ${BUMPED}：\n${bumped.stdout}`);
    assert(bumped.stdout.includes("请将内容层 src 改为新键"), "应提示改内容层");
    assert(bumped.stdout.includes("待刷新 CDN"), "应打印 CDN 刷新清单");
    assert(await exists(bumpedAbs), "新键正式位应存在");
    assert(!(await exists(destAbs)), "旧键正式位应仍不存在");
    ledger = JSON.parse(await readFile(ledgerPath, "utf8"));
    const oldRow = ledger.records.find((item) => item.object === OBJECT);
    const newRow = ledger.records.find((item) => item.object === BUMPED);
    assert(oldRow?.status === "withdrawn", "旧键应保持 withdrawn");
    assert(newRow?.status === "published", "新键应为 published");
    assert(newRow.originRel === undefined, "新键不应登记 originRel");
    assert(newRow.stageRel === oldRow.stageRel, "新键应沿用同一 stageRel");

    console.log("阶段4 隔离验收通过。");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
