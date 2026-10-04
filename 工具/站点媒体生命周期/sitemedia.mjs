/**
 * 站点媒体生命周期：中转站 → 正式位。
 *
 * 用法（在本目录或任意目录）：
 *   node sitemedia.mjs status [object]
 *   node sitemedia.mjs ingest <stageRel> --object <object> [--source-stage-rel <sourceStageRel>]
 *   node sitemedia.mjs reconcile <stageRel> --object <object> --dry-run|--apply
 *   node sitemedia.mjs withdraw <object> --dry-run|--apply
 *   node sitemedia.mjs purge [--dry-run] [--object <object>]… [--url <url>]…
 *
 * 可选：WORKSPACE_ROOT。COS / CDN 只读 PersonalSite/.env.deploy，按对象键 deleteObject 或 PurgeUrlsCache，不 prune。
 */

import { createHash, createHmac } from "node:crypto";
import { createRequire } from "node:module";
import { copyFile, mkdir, readdir, readFile, rm, rmdir, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  extractExpandedReferences,
  hitsInContentCorpus,
  loadTypeScript,
} from "./content-refs.mjs";
import {
  LEDGER_RENAME_RETRY_PREFIX,
  LEDGER_RENAME_RETRYING_PREFIX,
  saveLedgerFile,
} from "./ledger-atomic.mjs";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const DEFAULT_WORKSPACE = path.resolve(TOOL_DIR, "..", "..");
const ENV_FILE = path.join(TOOL_DIR, ".env.media");
const CONTENT_EXT = new Set([".ts", ".tsx", ".js", ".jsx", ".json", ".md"]);
const LEDGER_NAME = "media-ledger.json";
const SKIP_ASSET_NAMES = new Set([
  ".gitkeep",
  ".ds_store",
  "thumbs.db",
  "desktop.ini",
  "readme.md",
]);
const PACK_DIR_NAMES = new Set(["webgl", "build"]);

function parseEnvFile(text) {
  /** @type {Record<string, string>} */
  const env = {};
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const eq = line.indexOf("=");
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    env[key] = value;
  }
  return env;
}

function parseArgs(argv) {
  const flags = {
    help: false,
    dryRun: false,
    apply: false,
    bump: false,
    object: "",
    key: "",
    sourceStageRel: "",
    /** @type {string[]} */
    objectList: [],
    /** @type {string[]} */
    urlList: [],
  };
  /** @type {string[]} */
  const positionals = [];
  const tokens = argv.slice(2);
  for (let i = 0; i < tokens.length; i += 1) {
    const token = tokens[i];
    if (token === "--help" || token === "-h") flags.help = true;
    else if (token === "--dry-run") flags.dryRun = true;
    else if (token === "--apply") flags.apply = true;
    else if (token === "--bump") flags.bump = true;
    else if (token === "--object" || token === "--key" || token === "--url" || token === "--source-stage-rel") {
      const value = tokens[i + 1];
      if (!value || value.startsWith("--")) {
        throw new Error(`${token} 后需要一个路径参数`);
      }
      if (token === "--object") {
        flags.object = value;
        flags.objectList.push(value);
      } else if (token === "--key") flags.key = value;
      else if (token === "--source-stage-rel") flags.sourceStageRel = value;
      else flags.urlList.push(value);
      i += 1;
    } else if (token.startsWith("--")) {
      throw new Error(`未知参数：${token}`);
    } else {
      positionals.push(token);
    }
  }
  return {
    command: positionals[0] ?? "",
    args: positionals.slice(1),
    flags,
  };
}

function printHelp() {
  console.log(`站点媒体生命周期

用法：
  node sitemedia.mjs status [object|key]
  node sitemedia.mjs status --key <key>
  node sitemedia.mjs ingest <stageRel> --object <object> [--bump] [--source-stage-rel <sourceStageRel>]
  node sitemedia.mjs reconcile <stageRel> --object <object> --dry-run|--apply
  node sitemedia.mjs withdraw <object> --dry-run|--apply
  node sitemedia.mjs purge [--dry-run] [--object <object>]… [--url <url>]…

status 对账台账、正式位与内容引用；无台账的正式位列为待补登记。
{key}/stock/ 会出现在报告中，但不可 withdraw。
ingest 从中转站复制到正式位并登记 stageRel，不移动、不删除中转站文件。本流程不读原片库。
同一 published 的 stageRel 不可再登记到其它 object。状态已是 withdrawn 的，可以登记到新 object。
reconcile 安全补登记已有正式位：校验中转站与正式位均存在，只写台账，不复制、覆盖或删除文件。
reconcile 允许 JPG 中转源对应 WebP 正式位；--dry-run 只报告，--apply 才写台账。
--apply 在内容层引用为零时删除正式位与 COS 该键，不调用 --prune-assets。
--bump 仅用于 ingest：从已 withdrawn 的 --object 生成 -vN 新键，旧键保持 withdrawn。
--source-stage-rel 仅用于 ingest：成片 stageRel 指向 .site-ready 时记下中转站原片，供对照认领。
purge 提交腾讯云 PurgeUrlsCache；--dry-run 只打印 URL。须 COS_SECRET_ID/KEY 具备 cdn:PurgeUrlsCache，且 VITE_ASSET_BASE 为 https 域名。
restage 已取消；中转站补充资源请人工放入。
配置见同目录 .env.media.example。
网站资源编辑工具可经环境变量重映射：WORKSPACE_ROOT、SITEMEDIA_STAGE_ROOT、SITEMEDIA_PLACEHOLDERS_ROOT、SITEMEDIA_CONTENT_ROOT、SITEMEDIA_LEDGER、SITEMEDIA_DEPLOY_ENV。未设置时仍用本仓库默认布局。`);
}

function normalizeRel(raw, label) {
  const trimmed = String(raw ?? "").trim().replace(/\\/g, "/");
  if (!trimmed) throw new Error(`${label} 不能为空`);
  const stripped = trimmed.replace(/^\/+/, "").replace(/^\.\//, "");
  const parts = stripped.split("/").filter((part) => part && part !== ".");
  if (parts.length === 0) throw new Error(`${label} 不能为空`);
  if (parts.some((part) => part === "..")) {
    throw new Error(`${label} 不得包含上级目录段`);
  }
  return parts.join("/");
}

function normalizeObject(raw) {
  const object = normalizeRel(raw, "object");
  if (object.startsWith("placeholders/")) {
    throw new Error("object 不要带 placeholders/ 前缀");
  }
  if (!object.includes("/")) {
    throw new Error("object 须为 key/文件名，例如 profile/portrait.webp");
  }
  if (/(^|\/)stock\//.test(object)) {
    throw new Error("{key}/stock/ 常驻占位不走本工具");
  }
  return object;
}

function normalizeOptionalSourceStageRel(raw, stageRel) {
  const trimmed = String(raw ?? "").trim();
  if (!trimmed) return "";
  const sourceStageRel = normalizeRel(trimmed, "sourceStageRel");
  return sourceStageRel === stageRel ? "" : sourceStageRel;
}

function extOf(rel) {
  const base = rel.split("/").pop() ?? "";
  const dot = base.lastIndexOf(".");
  return dot >= 0 ? base.slice(dot).toLowerCase() : "";
}

function bumpedCandidate(object, version) {
  const slash = object.lastIndexOf("/");
  const dir = slash >= 0 ? object.slice(0, slash + 1) : "";
  const base = slash >= 0 ? object.slice(slash + 1) : object;
  const dot = base.lastIndexOf(".");
  const stem = dot >= 0 ? base.slice(0, dot) : base;
  const ext = dot >= 0 ? base.slice(dot) : "";
  const matched = stem.match(/^(.*)-v(\d+)$/);
  const root = matched ? matched[1] : stem;
  return `${dir}${root}-v${version}${ext}`;
}

function firstBumpVersion(object) {
  const base = object.split("/").pop() ?? "";
  const dot = base.lastIndexOf(".");
  const stem = dot >= 0 ? base.slice(0, dot) : base;
  const matched = stem.match(/-v(\d+)$/);
  return matched ? Number(matched[1]) + 1 : 2;
}

async function allocateBumpedObject(config, ledger, object) {
  const start = firstBumpVersion(object);
  for (let version = start; version < start + 1000; version += 1) {
    const candidate = bumpedCandidate(object, version);
    if (findRecord(ledger, candidate)) continue;
    if (await pathExists(destAbs(config, candidate))) continue;
    return candidate;
  }
  throw new Error(`无法为 ${object} 分配新的 -vN 对象键`);
}

async function pathExists(abs) {
  try {
    await stat(abs);
    return true;
  } catch (error) {
    if (error && error.code === "ENOENT") return false;
    throw error;
  }
}

async function isFile(abs) {
  try {
    const info = await stat(abs);
    return info.isFile();
  } catch (error) {
    if (error && error.code === "ENOENT") return false;
    throw error;
  }
}

async function loadFileEnv() {
  try {
    return parseEnvFile(await readFile(ENV_FILE, "utf8"));
  } catch (error) {
    if (error && error.code === "ENOENT") return {};
    throw error;
  }
}

function pickEnv(fileEnv, key) {
  const fromProcess = process.env[key]?.trim() ?? "";
  if (fromProcess) return fromProcess;
  return fileEnv[key]?.trim() ?? "";
}

function resolveOptional(fileEnv, key, fallback) {
  const override = pickEnv(fileEnv, key);
  return override ? path.resolve(override) : fallback;
}

async function loadConfig() {
  const fileEnv = await loadFileEnv();
  const workspaceRoot = path.resolve(
    pickEnv(fileEnv, "WORKSPACE_ROOT") || DEFAULT_WORKSPACE,
  );
  return {
    workspaceRoot,
    stageRoot: resolveOptional(
      fileEnv,
      "SITEMEDIA_STAGE_ROOT",
      path.join(workspaceRoot, "作品中转站"),
    ),
    placeholdersRoot: resolveOptional(
      fileEnv,
      "SITEMEDIA_PLACEHOLDERS_ROOT",
      path.join(workspaceRoot, "PersonalSite", "public", "placeholders"),
    ),
    contentRoot: resolveOptional(
      fileEnv,
      "SITEMEDIA_CONTENT_ROOT",
      path.join(workspaceRoot, "PersonalSite", "src"),
    ),
    deployEnvPath: resolveOptional(
      fileEnv,
      "SITEMEDIA_DEPLOY_ENV",
      path.join(workspaceRoot, "PersonalSite", ".env.deploy"),
    ),
    ledgerPath: resolveOptional(
      fileEnv,
      "SITEMEDIA_LEDGER",
      path.join(workspaceRoot, "PersonalSite", "src", "content", LEDGER_NAME),
    ),
    sitePackagePath: path.join(DEFAULT_WORKSPACE, "PersonalSite", "package.json"),
  };
}

async function loadDeployEnv(config) {
  try {
    return parseEnvFile(await readFile(config.deployEnvPath, "utf8"));
  } catch (error) {
    if (error && error.code === "ENOENT") return {};
    throw error;
  }
}

function hasCosConfig(env) {
  return Boolean(
    env.COS_SECRET_ID?.trim() &&
      env.COS_SECRET_KEY?.trim() &&
      env.COS_BUCKET?.trim() &&
      env.COS_REGION?.trim(),
  );
}

function isMissingCosObjectError(error) {
  const status = Number(error?.statusCode ?? error?.status);
  const code = String(error?.code ?? error?.error ?? "");
  const message = String(error?.message ?? error ?? "");
  return (
    status === 404 ||
    /NoSuchKey|NoSuchResource/i.test(code) ||
    /NoSuchKey|not exist|不存在|404/i.test(message)
  );
}

function refreshUrls(deployEnv, object) {
  const base = (deployEnv.VITE_ASSET_BASE ?? "").trim().replace(/\/+$/, "");
  const local = `/placeholders/${object}`;
  if (base) return [`${base}/${object}`, local];
  return [local];
}

async function deleteCosKey(config, deployEnv, object) {
  const sitePkg = path.join(config.workspaceRoot, "PersonalSite", "package.json");
  let COS;
  try {
    const require = createRequire(sitePkg);
    COS = require("cos-nodejs-sdk-v5");
  } catch {
    throw new Error("缺少 cos-nodejs-sdk-v5，请在 PersonalSite 执行 npm install");
  }
  const cos = new COS({
    SecretId: deployEnv.COS_SECRET_ID.trim(),
    SecretKey: deployEnv.COS_SECRET_KEY.trim(),
  });
  try {
    await cos.deleteObject({
      Bucket: deployEnv.COS_BUCKET.trim(),
      Region: deployEnv.COS_REGION.trim(),
      Key: object,
    });
  } catch (error) {
    if (isMissingCosObjectError(error)) return "missing";
    throw error;
  }
  return "deleted";
}

async function loadLedger(config) {
  let text;
  try {
    text = await readFile(config.ledgerPath, "utf8");
  } catch (error) {
    if (error && error.code === "ENOENT") {
      throw new Error(`找不到台账：${config.ledgerPath}`);
    }
    throw error;
  }
  const data = JSON.parse(text);
  if (data.version !== 1 || !Array.isArray(data.records)) {
    throw new Error("台账格式无效：需要 version=1 且 records 为数组");
  }
  return data;
}

async function saveLedger(config, ledger) {
  const retries = await saveLedgerFile(config.ledgerPath, ledger, {
    onRetry(count) {
      console.log(`${LEDGER_RENAME_RETRYING_PREFIX}${count}`);
    },
  });
  if (retries > 0) {
    console.log(`${LEDGER_RENAME_RETRY_PREFIX}${retries}`);
  }
}

function findRecord(ledger, object) {
  return ledger.records.find((item) => item.object === object);
}

function findPublishedByStageRel(ledger, stageRel, exceptObject) {
  return ledger.records.find(
    (item) =>
      item.status === "published" &&
      item.object !== exceptObject &&
      (item.stageRel === stageRel || item.sourceStageRel === stageRel),
  );
}

async function copyToDest(fromAbs, toAbs) {
  await mkdir(path.dirname(toAbs), { recursive: true });
  await copyFile(fromAbs, toAbs);
}

function stageAbs(config, stageRel) {
  return path.join(config.stageRoot, ...stageRel.split("/"));
}

function destAbs(config, object) {
  return path.join(config.placeholdersRoot, ...object.split("/"));
}

function isStockObject(object) {
  return /(^|\/)stock\//.test(object);
}

function objectKey(object) {
  return object.split("/")[0] ?? "";
}

function matchesFilter(object, filter) {
  if (filter.type === "all") return true;
  if (filter.type === "key") {
    return object === filter.value || object.startsWith(`${filter.value}/`);
  }
  return object === filter.value;
}

async function loadContentCorpus(config) {
  const ts = loadTypeScript(config.sitePackagePath);
  /** @type {{ rel: string, lines: string[] }[]} */
  const files = [];
  /** @type {{ object: string, file: string, line: number, dynamic: boolean, helper: string }[]} */
  const expandedRefs = [];
  async function walk(current) {
    let entries = [];
    try {
      entries = await readdir(current, { withFileTypes: true });
    } catch (error) {
      if (error && error.code === "ENOENT") return;
      throw error;
    }
    for (const entry of entries) {
      const abs = path.join(current, entry.name);
      if (entry.isDirectory()) {
        if (entry.name === "node_modules" || entry.name === "dist") continue;
        await walk(abs);
        continue;
      }
      if (entry.name === LEDGER_NAME) continue;
      const ext = path.extname(entry.name).toLowerCase();
      if (!CONTENT_EXT.has(ext)) continue;
      const text = await readFile(abs, "utf8");
      const rel = path.relative(config.contentRoot, abs).split(path.sep).join("/");
      files.push({
        rel,
        lines: text.split(/\r?\n/),
      });
      expandedRefs.push(...extractExpandedReferences(ts, text, rel));
    }
  }
  await walk(config.contentRoot);
  return { files, expandedRefs };
}

function hitsInCorpus(corpus, object) {
  return hitsInContentCorpus(corpus, object);
}

async function listPlaceholderDisk(config) {
  /** @type {string[]} */
  const objects = [];
  /** @type {string[]} */
  const packDirs = [];
  async function walk(current) {
    let entries = [];
    try {
      entries = await readdir(current, { withFileTypes: true });
    } catch (error) {
      if (error && error.code === "ENOENT") return;
      throw error;
    }
    for (const entry of entries) {
      const abs = path.join(current, entry.name);
      if (entry.isDirectory()) {
        if (PACK_DIR_NAMES.has(entry.name.toLowerCase())) {
          const rel = path.relative(config.placeholdersRoot, abs).split(path.sep).join("/");
          packDirs.push(`${rel}/`);
          continue;
        }
        await walk(abs);
        continue;
      }
      if (SKIP_ASSET_NAMES.has(entry.name.toLowerCase())) continue;
      const rel = path.relative(config.placeholdersRoot, abs).split(path.sep).join("/");
      objects.push(rel);
    }
  }
  await walk(config.placeholdersRoot);
  objects.sort();
  packDirs.sort();
  return { objects, packDirs };
}

async function describeRecord(config, record, corpus) {
  const staged = await isFile(stageAbs(config, record.stageRel));
  const published = await isFile(destAbs(config, record.object));
  const resolvedCorpus = corpus ?? (await loadContentCorpus(config));
  const refs = hitsInCorpus(resolvedCorpus, record.object);
  return { staged, published, refs };
}

function printHits(hits) {
  if (hits.length === 0) {
    console.log("内容层引用：0");
    return;
  }
  console.log(`内容层引用：${hits.length}`);
  for (const hit of hits.slice(0, 20)) {
    const detail = hit.dynamic ? `（${hit.helper} 静态展开）` : "";
    console.log(`  ${hit.file}:${hit.line}${detail}`);
  }
  if (hits.length > 20) console.log(`  … 其余 ${hits.length - 20} 处`);
}

function parseStatusFilter(args, flags) {
  if (flags.key && args[0]) {
    throw new Error("status 不要同时传位置参数与 --key");
  }
  if (flags.key) {
    const key = normalizeRel(flags.key, "key");
    if (key.includes("/")) throw new Error("--key 只接受栏目 key，不要带文件路径");
    return { type: "key", value: key };
  }
  if (!args[0]) return { type: "all", value: "" };
  const raw = args[0].replace(/\\/g, "/").trim();
  if (!raw.includes("/")) return { type: "key", value: raw };
  return { type: "object", value: normalizeRel(raw, "object") };
}

async function cmdStatus(config, filter) {
  const ledger = await loadLedger(config);
  const corpus = await loadContentCorpus(config);
  const disk = await listPlaceholderDisk(config);
  const ledgerByObject = new Map(ledger.records.map((item) => [item.object, item]));

  /** @type {{ object: string, info: Awaited<ReturnType<typeof describeRecord>>, notes: string[] }[]} */
  const ledgerRows = [];
  /** @type {{ object: string, refs: { file: string, line: number }[] }[]} */
  const unledgered = [];
  /** @type {{ object: string, refs: { file: string, line: number }[] }[]} */
  const stock = [];

  for (const record of ledger.records) {
    if (!matchesFilter(record.object, filter)) continue;
    const info = await describeRecord(config, record, corpus);
    const notes = [];
    if (record.status === "published" && !info.published) notes.push("幽灵：published 无正式位");
    if (record.status === "withdrawn" && info.published) notes.push("幽灵：withdrawn 仍有正式位");
    if (record.status === "published" && !info.staged) notes.push("中转站缺失");
    ledgerRows.push({ object: record.object, record, info, notes });
  }

  for (const object of disk.objects) {
    if (!matchesFilter(object, filter)) continue;
    if (ledgerByObject.has(object)) continue;
    const refs = hitsInCorpus(corpus, object);
    if (isStockObject(object)) stock.push({ object, refs });
    else unledgered.push({ object, refs });
  }

  const packDirs = disk.packDirs.filter((dir) =>
    matchesFilter(dir.replace(/\/$/, ""), filter) || matchesFilter(`${dir}index`, filter),
  );

  if (filter.type === "object" && ledgerRows.length === 0 && unledgered.length === 0 && stock.length === 0) {
    throw new Error(`未找到对象：${filter.value}（台账、正式位均无）`);
  }

  const missingStage = ledgerRows.filter((row) => row.notes.includes("中转站缺失")).length;
  const ghosts = ledgerRows.filter((row) =>
    row.notes.some((note) => note.startsWith("幽灵")),
  ).length;

  console.log("对账摘要");
  console.log(`  台账：${ledgerRows.length}`);
  console.log(`  待补登记：${unledgered.length}`);
  console.log(`  常驻占位：${stock.length}（不可 withdraw）`);
  console.log(`  中转站缺失：${missingStage}`);
  console.log(`  幽灵：${ghosts}`);
  if (packDirs.length) console.log(`  发布包目录：${packDirs.length}（未逐文件列出）`);

  if (unledgered.length) {
    console.log("");
    console.log("待补登记（正式位有文件、台账无记录；补 stageRel 后再登记，勿再 ingest 覆盖正式位）");
    for (const row of unledgered) {
      console.log(`  ${row.object}  引用 ${row.refs.length}`);
    }
  }

  if (stock.length) {
    console.log("");
    console.log("常驻占位 {key}/stock/（不可 withdraw）");
    for (const row of stock) {
      console.log(`  ${row.object}  引用 ${row.refs.length}`);
    }
  }

  if (packDirs.length) {
    console.log("");
    console.log("发布包目录");
    for (const dir of packDirs) console.log(`  ${dir}`);
  }

  if (ledgerRows.length) {
    console.log("");
    console.log("台账");
    for (const row of ledgerRows) {
      console.log("");
      console.log(`对象：${row.object}`);
      console.log(`状态：${row.record.status}`);
      console.log(`中转站：${row.record.stageRel}（${row.info.staged ? "存在" : "缺失"}）`);
      console.log(`正式位：${row.info.published ? "存在" : "缺失"}`);
      printHits(row.info.refs);
      if (row.notes.length) console.log(`备注：${row.notes.join("；")}`);
    }
  } else if (!unledgered.length && !stock.length) {
    console.log("");
    console.log("台账为空，正式位也无匹配文件。");
  }
}

async function cmdIngest(config, stageRelRaw, flags) {
  const stageRel = normalizeRel(stageRelRaw, "stageRel");
  const requested = normalizeObject(flags.object);
  const ledger = await loadLedger(config);
  const existing = findRecord(ledger, requested);

  let object = requested;

  if (flags.bump) {
    if (!existing) {
      throw new Error(`--bump 需要台账中已有对象：${requested}。首次入库不要加 --bump。`);
    }
    if (existing.status !== "withdrawn") {
      throw new Error(`--bump 仅用于 withdrawn 后再入库。当前为 ${existing.status}，先 withdraw。`);
    }
    object = await allocateBumpedObject(config, ledger, requested);
  } else if (existing?.status === "published") {
    throw new Error(`对象已是 published：${object}。下架后再入库，或 withdraw 后加 --bump 换键。`);
  }

  if (extOf(stageRel) !== extOf(object)) {
    throw new Error(
      `中转站扩展名 ${extOf(stageRel) || "（无）"} 与 object ${extOf(object) || "（无）"} 不一致；本命令不转码`,
    );
  }

  const sourceStageRel = normalizeOptionalSourceStageRel(flags.sourceStageRel, stageRel);
  if (sourceStageRel) {
    if (!(await isFile(stageAbs(config, sourceStageRel)))) {
      throw new Error(`中转站没有来源文件：${sourceStageRel}`);
    }
    const sourceHolder = findPublishedByStageRel(ledger, sourceStageRel, object);
    if (sourceHolder) {
      throw new Error(
        `中转站来源路径已登记给 published 对象：${sourceHolder.object}。同一原片不可再入库到其它对象。`,
      );
    }
  }

  const fromAbs = stageAbs(config, stageRel);
  const toAbs = destAbs(config, object);

  if (!(await isFile(fromAbs))) {
    throw new Error(`中转站没有该文件：${fromAbs}`);
  }
  if (await pathExists(toAbs)) {
    throw new Error(`正式位已有文件，拒绝覆盖：${toAbs}`);
  }
  const stageHolder = findPublishedByStageRel(ledger, stageRel, object);
  if (stageHolder) {
    throw new Error(
      `中转站路径已登记给 published 对象：${stageHolder.object}。同一中转站文件不可再入库到其它对象。`,
    );
  }

  await copyToDest(fromAbs, toAbs);

  const next = {
    object,
    stageRel,
    status: "published",
  };
  if (sourceStageRel) {
    next.sourceStageRel = sourceStageRel;
  }
  if (flags.bump) {
    ledger.records.push(next);
  } else if (existing) {
    Object.assign(existing, next);
    delete existing.originRel;
  } else {
    ledger.records.push(next);
  }
  try {
    await saveLedger(config, ledger);
  } catch (error) {
    await rm(toAbs);
    throw error;
  }

  const deployEnv = await loadDeployEnv(config);
  console.log(`已入库 ${object}`);
  console.log(`中转站未移动，已复制到正式位：${toAbs}`);
  if (flags.bump) {
    console.log(`旧键保持 withdrawn：${requested}`);
    console.log("请将内容层 src 改为新键（本命令不改 TypeScript）：");
    console.log(`  ${requested}`);
    console.log("  →");
    console.log(`  ${object}`);
    const refs = hitsInCorpus(await loadContentCorpus(config), requested);
    printHits(refs);
    if (refs.length) {
      console.log("旧键仍有引用。写入新键并去掉旧键后，再发静态包。");
    }
    console.log("待刷新 CDN（旧键与新键）：");
    for (const url of [...refreshUrls(deployEnv, requested), ...refreshUrls(deployEnv, object)]) {
      console.log(`  ${url}`);
    }
  } else {
    console.log(`请在内容层写入 src = ${object}`);
    console.log("待刷新 CDN：");
    for (const url of refreshUrls(deployEnv, object)) {
      console.log(`  ${url}`);
    }
  }
}

async function cmdReconcile(config, stageRelRaw, flags) {
  if (flags.bump) throw new Error("--bump 只用于 ingest，不要加在 reconcile 上");
  if (flags.dryRun === flags.apply) {
    throw new Error("reconcile 必须且只能指定 --dry-run 或 --apply 之一");
  }
  const stageRel = normalizeRel(stageRelRaw, "stageRel");
  const object = normalizeObject(flags.object);
  const ledger = await loadLedger(config);

  if (findRecord(ledger, object)) {
    throw new Error(`object 已有台账记录，拒绝补登记：${object}`);
  }
  const stageHolder = findPublishedByStageRel(ledger, stageRel, object);
  if (stageHolder) {
    throw new Error(
      `中转站路径已被 published 对象占用：${stageHolder.object}。拒绝补登记到 ${object}。`,
    );
  }

  const fromAbs = stageAbs(config, stageRel);
  const toAbs = destAbs(config, object);
  if (!(await isFile(fromAbs))) {
    throw new Error(`中转站没有该文件：${fromAbs}`);
  }
  if (!(await isFile(toAbs))) {
    throw new Error(`正式位没有该文件：${toAbs}`);
  }

  console.log("已有正式位补登记");
  console.log(`中转站：${stageRel}（存在）`);
  console.log(`正式位：${object}（存在）`);
  if (extOf(stageRel) !== extOf(object)) {
    console.log(
      `扩展名映射：${extOf(stageRel) || "（无）"} → ${extOf(object) || "（无）"}（按已转码正式位登记）`,
    );
  }
  console.log("文件操作：不复制、不覆盖、不删除");

  if (flags.dryRun) {
    console.log("预演结束，台账未修改。请确认映射后显式加 --apply。");
    return;
  }

  ledger.records.push({ object, stageRel, status: "published" });
  await saveLedger(config, ledger);
  console.log("补登记完成：仅新增 published 台账记录，现有文件均未改动。");
}

async function cmdWithdraw(config, objectRaw, flags) {
  if (flags.bump) throw new Error("--bump 只用于 ingest，不要加在 withdraw 上");
  if (flags.dryRun === flags.apply) {
    throw new Error("withdraw 必须且只能指定 --dry-run 或 --apply 之一");
  }
  const object = normalizeObject(objectRaw);
  const ledger = await loadLedger(config);
  const record = findRecord(ledger, object);
  if (!record) throw new Error(`台账无此对象：${object}`);

  const info = await describeRecord(config, record);
  console.log(`对象：${object}`);
  console.log(`台账状态：${record.status}`);
  console.log(`中转站：${record.stageRel}（${info.staged ? "存在" : "缺失"}）`);
  console.log(`正式位：${destAbs(config, object)}（${info.published ? "存在" : "缺失"}）`);
  printHits(info.refs);
  console.log(`COS 键：${object}`);

  if (flags.dryRun) {
    console.log("待刷新 CDN：发布后按该键拼接 VITE_ASSET_BASE");
    console.log("预演结束，未删除任何文件。");
    return;
  }

  if (info.refs.length > 0) {
    const dynamicCount = info.refs.filter((hit) => hit.dynamic).length;
    const dynamicDetail = dynamicCount > 0 ? `，其中静态展开引用 ${dynamicCount} 处` : "";
    throw new Error(
      `内容层仍引用该对象（${info.refs.length} 处${dynamicDetail}），拒绝删除。先去掉引用再 --apply。`,
    );
  }

  const deployEnv = await loadDeployEnv(config);
  if (hasCosConfig(deployEnv)) {
    const result = await deleteCosKey(config, deployEnv, object);
    if (result === "missing") {
      console.log(`COS 无此键，视为已删除：${object}`);
    } else {
      console.log(`已删除 COS 对象 ${object}`);
    }
  } else {
    console.log("未配置 PersonalSite/.env.deploy 的 COS 凭证，跳过远端删除。");
  }

  const localAbs = destAbs(config, object);
  if (await isFile(localAbs)) {
    await rm(localAbs);
    console.log(`已删除正式位：${localAbs}`);
  } else {
    console.log("正式位文件已不存在。");
  }
  await removeEmptyOfficialDir(config, object);

  record.status = "withdrawn";
  await saveLedger(config, ledger);
  console.log("台账已改为 withdrawn。中转站未收回网图。");
  console.log("待刷新 CDN：");
  for (const url of refreshUrls(deployEnv, object)) {
    console.log(`  ${url}`);
  }
}

/**
 * 对象所在的作品目录已空则删除。不向上删除栏目夹，也不动中转站。
 * @param {{ placeholdersRoot: string }} config
 * @param {string} object
 */
async function removeEmptyOfficialDir(config, object) {
  const dirAbs = path.resolve(path.dirname(destAbs(config, object)));
  const rootAbs = path.resolve(config.placeholdersRoot);
  const relative = path.relative(rootAbs, dirAbs);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) return;
  const depth = relative.split(path.sep).filter(Boolean).length;
  if (depth < 2) return;
  let names;
  try {
    names = await readdir(dirAbs);
  } catch {
    return;
  }
  if (names.length > 0) return;
  await rmdir(dirAbs);
  console.log(`已删除空目录：${dirAbs}`);
}

function collectPurgeUrls(deployEnv, objectList, urlList) {
  /** @type {string[]} */
  const resultList = [];
  const seenSet = new Set();
  const add = (raw) => {
    const url = String(raw ?? "").trim();
    if (!url || seenSet.has(url)) return;
    seenSet.add(url);
    resultList.push(url);
  };
  for (const objectRaw of objectList) {
    const object = normalizeObject(objectRaw);
    for (const url of refreshUrls(deployEnv, object)) {
      add(url);
    }
  }
  for (const url of urlList) {
    add(url);
  }
  return resultList;
}

function httpsPurgeUrls(urlList) {
  return urlList.filter((url) => /^https?:\/\//i.test(url));
}

function sha256Hex(text) {
  return createHash("sha256").update(text, "utf8").digest("hex");
}

function hmacSha256(key, text) {
  return createHmac("sha256", key).update(text, "utf8").digest();
}

/**
 * 腾讯云 API 3.0 TC3 签名后提交 PurgeUrlsCache。
 * @param {Record<string, string>} deployEnv
 * @param {string[]} urlList
 */
async function purgeUrlsCache(deployEnv, urlList) {
  const secretId = deployEnv.COS_SECRET_ID?.trim() ?? "";
  const secretKey = deployEnv.COS_SECRET_KEY?.trim() ?? "";
  if (!secretId || !secretKey) {
    throw new Error("未配置 COS_SECRET_ID / COS_SECRET_KEY，无法提交 CDN 刷新。CAM 须含 cdn:PurgeUrlsCache。");
  }
  if (urlList.length === 0) {
    throw new Error("没有可提交的 http(s) URL。请填写 VITE_ASSET_BASE。");
  }
  if (urlList.length > 1000) {
    throw new Error(`单次最多 1000 条 URL，当前 ${urlList.length}。`);
  }

  const host = "cdn.tencentcloudapi.com";
  const service = "cdn";
  const action = "PurgeUrlsCache";
  const version = "2018-06-06";
  const timestamp = Math.floor(Date.now() / 1000);
  const date = new Date(timestamp * 1000).toISOString().slice(0, 10);
  const payload = JSON.stringify({ Urls: urlList });
  const hashedPayload = sha256Hex(payload);
  const contentType = "application/json; charset=utf-8";
  const canonicalHeaders = `content-type:${contentType}\nhost:${host}\n`;
  const signedHeaders = "content-type;host";
  const canonicalRequest = ["POST", "/", "", canonicalHeaders, signedHeaders, hashedPayload].join("\n");
  const credentialScope = `${date}/${service}/tc3_request`;
  const stringToSign = ["TC3-HMAC-SHA256", String(timestamp), credentialScope, sha256Hex(canonicalRequest)].join(
    "\n",
  );
  const secretDate = hmacSha256(`TC3${secretKey}`, date);
  const secretService = hmacSha256(secretDate, service);
  const secretSigning = hmacSha256(secretService, "tc3_request");
  const signature = createHmac("sha256", secretSigning).update(stringToSign, "utf8").digest("hex");
  const authorization = `TC3-HMAC-SHA256 Credential=${secretId}/${credentialScope}, SignedHeaders=${signedHeaders}, Signature=${signature}`;

  const response = await fetch(`https://${host}/`, {
    method: "POST",
    headers: {
      Authorization: authorization,
      "Content-Type": contentType,
      Host: host,
      "X-TC-Action": action,
      "X-TC-Timestamp": String(timestamp),
      "X-TC-Version": version,
    },
    body: payload,
  });
  const body = await response.json();
  const error = body?.Response?.Error;
  if (error) {
    throw new Error(`${error.Code}：${error.Message}`);
  }
  const taskId = body?.Response?.TaskId;
  if (!taskId) {
    throw new Error("CDN 刷新未返回 TaskId。");
  }
  return String(taskId);
}

async function cmdPurge(config, flags, extraObjectList) {
  const objectList = [...flags.objectList, ...extraObjectList];
  if (objectList.length === 0 && flags.urlList.length === 0) {
    throw new Error("purge 需要至少一个 --object 或 --url");
  }
  if (flags.apply) throw new Error("purge 不要加 --apply");
  if (flags.bump) throw new Error("--bump 只用于 ingest");

  const deployEnv = await loadDeployEnv(config);
  const allUrlList = collectPurgeUrls(deployEnv, objectList, flags.urlList);
  const submitList = httpsPurgeUrls(allUrlList);
  console.log("待刷新 CDN：");
  for (const url of allUrlList) {
    console.log(`  ${url}`);
  }
  if (flags.dryRun) {
    console.log("预演结束，未提交刷新。");
    return;
  }
  const taskId = await purgeUrlsCache(deployEnv, submitList);
  console.log(`已提交 CDN 刷新 TaskId：${taskId}`);
}

async function cmdRestage() {
  throw new Error("restage 已取消。中转站补充资源请人工放入后再 ingest。本流程不读原片库。");
}

async function main() {
  const parsed = parseArgs(process.argv);
  if (parsed.flags.help || parsed.command === "help") {
    printHelp();
    return;
  }
  if (!parsed.command) {
    printHelp();
    throw new Error("请指定命令");
  }

  const config = await loadConfig();

  if (parsed.command === "status") {
    const filter = parseStatusFilter(parsed.args, parsed.flags);
    await cmdStatus(config, filter);
    return;
  }
  if (parsed.command === "ingest") {
    const stageRel = parsed.args[0];
    if (!stageRel) throw new Error("ingest 需要 <stageRel>");
    if (!parsed.flags.object) throw new Error("ingest 需要 --object");
    await cmdIngest(config, stageRel, parsed.flags);
    return;
  }
  if (parsed.command === "reconcile" || parsed.command === "register-existing") {
    const stageRel = parsed.args[0];
    if (!stageRel) throw new Error("reconcile 需要 <stageRel>");
    if (!parsed.flags.object) throw new Error("reconcile 需要 --object");
    await cmdReconcile(config, stageRel, parsed.flags);
    return;
  }
  if (parsed.command === "withdraw") {
    const object = parsed.args[0];
    if (!object) throw new Error("withdraw 需要 <object>");
    await cmdWithdraw(config, object, parsed.flags);
    return;
  }
  if (parsed.command === "purge") {
    await cmdPurge(config, parsed.flags, parsed.args);
    return;
  }
  if (parsed.command === "restage") {
    const object = parsed.args[0];
    if (!object) throw new Error("restage 需要 <object>");
    await cmdRestage();
    return;
  }
  throw new Error(`未知命令：${parsed.command}`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
