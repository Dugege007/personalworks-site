/**
 * 阶段1 隔离验收：不触碰真实中转站、placeholders 与台账。
 * 在 工具/站点媒体生命周期 目录执行：node verify-stage1.mjs
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
  const root = await mkdtemp(path.join(os.tmpdir(), "sitemedia-stage1-"));
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
  const ledgerPath = path.join(
    workspace,
    "PersonalSite",
    "src",
    "content",
    "media-ledger.json",
  );
  const contentFile = path.join(
    workspace,
    "PersonalSite",
    "src",
    "content",
    "works.ts",
  );

  try {
    await mkdir(path.dirname(stageAbs), { recursive: true });
    await mkdir(path.dirname(destAbs), { recursive: true });
    await mkdir(path.dirname(ledgerPath), { recursive: true });
    await writeFile(ledgerPath, `${JSON.stringify({ version: 1, records: [] }, null, 2)}\n`);
    await writeFile(contentFile, 'export const sample = { src: "landscape-cds/stock/01.webp" };\n');
    await writeFile(stageAbs, "stage-bytes");

    const missingStage = await run(env, [
      "ingest",
      "景观施工图（landscape-cds）/missing.jpg",
      "--object",
      OBJECT,
    ]);
    assert(missingStage.code !== 0, "中转站缺文件时 ingest 应失败");
    assert(
      missingStage.stderr.includes("中转站没有该文件"),
      `缺中转站报错不符：${missingStage.stderr}`,
    );
    assert(!(await exists(destAbs)), "中转站缺文件时不得写入正式位");

    const ingested = await run(env, ["ingest", STAGE_REL, "--object", OBJECT]);
    assert(ingested.code === 0, `ingest 应成功：${ingested.stderr}`);
    assert(await exists(stageAbs), "入库后中转站文件应仍在");
    assert(await exists(destAbs), "入库后正式位应有文件");
    const ledger = JSON.parse(await readFile(ledgerPath, "utf8"));
    assert(ledger.records[0]?.status === "published", "台账应为 published");
    assert(ledger.records[0]?.stageRel === STAGE_REL, "stageRel 应保留原相对路径");
    assert(!("originRel" in ledger.records[0]), "台账不应登记 originRel");

    const reused = await run(env, [
      "ingest",
      STAGE_REL,
      "--object",
      "landscape-cds/_sitemedia-test/other.jpg",
    ]);
    assert(reused.code !== 0, "同一中转站路径再入库到其它对象应失败");
    assert(
      reused.stderr.includes("中转站路径已登记给 published 对象"),
      `占用检查报错不符：${reused.stderr}`,
    );
    assert(await exists(stageAbs), "占用检查失败时中转站文件应仍在");

    const restagePublished = await run(env, ["restage", OBJECT]);
    assert(restagePublished.code !== 0, "restage 应失败");
    assert(
      restagePublished.stderr.includes("restage 已取消"),
      `restage 拒绝报错不符：${restagePublished.stderr}`,
    );
    assert(await exists(stageAbs), "restage 不得改动中转站文件");

    const dryRun = await run(env, ["withdraw", OBJECT, "--dry-run"]);
    assert(dryRun.code === 0, `withdraw --dry-run 应成功：${dryRun.stderr}`);
    assert(dryRun.stdout.includes("预演结束"), "dry-run 应声明未删除");
    assert(await exists(destAbs), "dry-run 不得删除正式位");

    const apply = await run(env, ["withdraw", OBJECT, "--apply"]);
    assert(apply.code === 0, `--apply 应成功：${apply.stderr}`);
    assert(!(await exists(destAbs)), "--apply 应删除正式位");
    assert(await exists(stageAbs), "--apply 不得删除中转站文件");
    const afterApply = JSON.parse(await readFile(ledgerPath, "utf8"));
    assert(afterApply.records[0]?.status === "withdrawn", "台账应为 withdrawn");
    assert(apply.stdout.includes("跳过远端删除"), "隔离环境无 COS 时应跳过远端");
    assert(!apply.stdout.includes("prune"), "不得出现 prune");

    const restageWithdrawn = await run(env, ["restage", OBJECT]);
    assert(restageWithdrawn.code !== 0, "下架后 restage 仍应拒绝");
    assert(restageWithdrawn.stderr.includes("restage 已取消"), "应提示人工放回中转站");

    await rm(stageAbs);
    const ingestMissing = await run(env, ["ingest", STAGE_REL, "--object", OBJECT]);
    assert(ingestMissing.code !== 0, "中转站被删后 ingest 应失败");
    assert(ingestMissing.stderr.includes("中转站没有该文件"), ingestMissing.stderr);

    const status = await run(env, ["status", OBJECT]);
    assert(status.code === 0, `status 应成功：${status.stderr}`);
    assert(status.stdout.includes("withdrawn"), "status 应显示 withdrawn");

    console.log("阶段1 隔离验收通过。");
    console.log(`临时目录已删：${root}`);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
