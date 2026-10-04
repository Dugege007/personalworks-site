/**
 * 首轮清单入库编排。每一项仍调用 sitemedia ingest，脚本只负责整批预检与断点续跑。
 *
 * 用法：
 *   node ingest-initial-batch.mjs --batch ./batches/initial-projects.json --dry-run
 *   node ingest-initial-batch.mjs --batch ./batches/initial-projects.json --apply
 */

import { spawn } from "node:child_process";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const WORKSPACE_ROOT = path.resolve(TOOL_DIR, "..", "..");
const STAGE_ROOT = path.join(WORKSPACE_ROOT, "作品中转站");
const PLACEHOLDERS_ROOT = path.join(WORKSPACE_ROOT, "PersonalSite", "public", "placeholders");
const LEDGER_PATH = path.join(WORKSPACE_ROOT, "PersonalSite", "src", "content", "media-ledger.json");
const CLI_PATH = path.join(TOOL_DIR, "sitemedia.mjs");

function parseArgs(argv) {
  const flags = { batch: "", dryRun: false, apply: false };
  const tokens = argv.slice(2);
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index];
    if (token === "--dry-run") flags.dryRun = true;
    else if (token === "--apply") flags.apply = true;
    else if (token === "--batch") {
      flags.batch = tokens[index + 1] ?? "";
      index += 1;
    } else {
      throw new Error(`未知参数：${token}`);
    }
  }
  if (!flags.batch) throw new Error("缺少 --batch");
  if (flags.dryRun === flags.apply) {
    throw new Error("必须且只能指定 --dry-run 或 --apply");
  }
  return flags;
}

function resolveRel(root, rel) {
  const normalized = String(rel ?? "").replace(/\\/g, "/");
  const parts = normalized.split("/").filter(Boolean);
  if (parts.length === 0 || parts.some((part) => part === "..")) {
    throw new Error(`相对路径无效：${rel}`);
  }
  return path.join(root, ...parts);
}

async function isFile(filePath) {
  try {
    return (await stat(filePath)).isFile();
  } catch (error) {
    if (error?.code === "ENOENT") return false;
    throw error;
  }
}

function runIngest(stageRel, object) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [CLI_PATH, "ingest", stageRel, "--object", object], {
      cwd: WORKSPACE_ROOT,
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
    child.on("exit", (code) => resolve({ code: code ?? 1, stdout, stderr }));
  });
}

async function loadJson(filePath) {
  return JSON.parse(await readFile(filePath, "utf8"));
}

async function buildPlan(batchPath) {
  const batch = await loadJson(batchPath);
  const ledger = await loadJson(LEDGER_PATH);
  const recordByObject = new Map(ledger.records.map((record) => [record.object, record]));
  const plan = [];
  const errors = [];

  for (const project of batch.projects ?? []) {
    for (const media of project.media ?? []) {
      if (media.action === "skip") continue;
      const record = recordByObject.get(media.object);
      const stageExists = await isFile(resolveRel(STAGE_ROOT, media.stageRel));
      const destExists = await isFile(resolveRel(PLACEHOLDERS_ROOT, media.object));
      const exactPublished =
        record?.status === "published" && record.stageRel === media.stageRel && destExists;

      if (!stageExists) {
        errors.push(`${project.id} 中转站缺失：${media.stageRel}`);
        continue;
      }
      if (media.action === "existing") {
        if (!exactPublished) {
          errors.push(`${project.id} 既有对象未完成台账对账：${media.object}`);
        }
        continue;
      }
      if (media.action !== "ingest") {
        errors.push(`${project.id} 未知动作：${media.action}`);
        continue;
      }
      if (exactPublished) {
        plan.push({ projectId: project.id, ...media, state: "done" });
        continue;
      }
      if (record || destExists) {
        errors.push(`${project.id} 对象状态冲突：${media.object}`);
        continue;
      }
      plan.push({ projectId: project.id, ...media, state: "pending" });
    }
  }
  return { plan, errors };
}

async function main() {
  const flags = parseArgs(process.argv);
  const batchPath = path.resolve(flags.batch);
  const { plan, errors } = await buildPlan(batchPath);
  if (errors.length > 0) {
    errors.forEach((error) => console.error(`预检失败：${error}`));
    throw new Error(`整批预检失败，共 ${errors.length} 项；未执行入库`);
  }

  const doneCount = plan.filter((item) => item.state === "done").length;
  const pending = plan.filter((item) => item.state === "pending");
  console.log(`预检通过：待入库 ${pending.length}，已完成 ${doneCount}。`);
  if (flags.dryRun) {
    console.log("预演结束，未复制媒体、未修改台账。");
    return;
  }

  for (let index = 0; index < pending.length; index += 1) {
    const item = pending[index];
    const result = await runIngest(item.stageRel, item.object);
    if (result.code !== 0) {
      const reason = result.stderr.trim() || result.stdout.trim() || `退出码 ${result.code}`;
      throw new Error(`${item.projectId}/${item.object} 入库失败：${reason}`);
    }
    if ((index + 1) % 20 === 0 || index + 1 === pending.length) {
      console.log(`已入库 ${index + 1}/${pending.length}`);
    }
  }
  console.log(`整批入库完成：新增 ${pending.length}，原有 ${doneCount}。`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
