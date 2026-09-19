/**
 * 本机一键发布：构建 SPA → 同步 placeholders 到 COS → 将 dist 覆盖到轻量 Nginx 根目录 → 重载 Nginx。
 *
 * 用法（在 PersonalSite 目录）：
 *   双击 deploy.bat             一条龙（免敲命令）
 *   npm run deploy              一条龙
 *   npm run deploy -- --spa     只发静态包
 *   npm run deploy -- --assets  只同步 COS
 *   npm run deploy -- --skip-build
 *   npm run deploy -- --dry-run
 *
 * 配置：复制 .env.deploy.example 为 .env.deploy 后填写。密钥不进 Git。
 * 媒体走 COS，不上传到轻量盘；Nginx 使用 reload，避免 restart 中断连接。
 */

import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import {
  cp,
  mkdtemp,
  readdir,
  readFile,
  rm,
  stat,
} from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { withTransientRetry } from "./cos-retry.mjs";

const require = createRequire(import.meta.url);
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const ENV_FILE = path.join(ROOT, ".env.deploy");
const DIST_DIR = path.join(ROOT, "dist");
const PLACEHOLDERS_DIR = path.join(ROOT, "public", "placeholders");
const TAR_NAME = "personal-site-dist.tgz";
const REMOTE_TAR = `/tmp/${TAR_NAME}`;
const SKIP_ASSET_NAMES = new Set([
  ".gitkeep",
  ".ds_store",
  "thumbs.db",
  "desktop.ini",
  "readme.md",
]);
const BACKUP_KEEP = 5;

function parseArgs(argv) {
  const flags = {
    spa: false,
    assets: false,
    skipBuild: false,
    dryRun: false,
    pruneAssets: false,
  };
  for (const token of argv.slice(2)) {
    if (token === "--spa") flags.spa = true;
    else if (token === "--assets") flags.assets = true;
    else if (token === "--skip-build") flags.skipBuild = true;
    else if (token === "--dry-run") flags.dryRun = true;
    else if (token === "--prune-assets") flags.pruneAssets = true;
    else throw new Error(`未知参数：${token}`);
  }
  if (flags.spa && flags.assets) {
    throw new Error("不要同时使用 --spa 与 --assets");
  }
  return flags;
}

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

function required(env, key) {
  const value = env[key]?.trim() ?? "";
  if (!value) throw new Error(`请在 .env.deploy 中填写 ${key}`);
  return value;
}

function assertSafeRemotePath(remotePath) {
  if (!/^\/[A-Za-z0-9._/-]+$/.test(remotePath) || remotePath.includes("..")) {
    throw new Error(`远程路径不安全：${remotePath}`);
  }
}

/**
 * @param {string} command
 * @param {string[]} args
 * @param {{ cwd?: string, env?: NodeJS.ProcessEnv, input?: string, shell?: boolean }} [options]
 */
function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd ?? ROOT,
      env: options.env ?? process.env,
      shell: options.shell ?? false,
      stdio: options.input ? ["pipe", "inherit", "inherit"] : "inherit",
    });
    if (options.input) {
      child.stdin.write(options.input);
      child.stdin.end();
    }
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${command} ${args.join(" ")} 退出码 ${code}`));
    });
  });
}

function sshArgs(env, extra = []) {
  const args = [
    "-p",
    env.DEPLOY_SSH_PORT || "22",
    "-o",
    "BatchMode=yes",
    "-o",
    "StrictHostKeyChecking=accept-new",
  ];
  if (env.DEPLOY_SSH_KEY) {
    args.push("-i", env.DEPLOY_SSH_KEY, "-o", "IdentitiesOnly=yes");
  }
  args.push(...extra);
  return args;
}

function scpArgs(env) {
  const args = [
    "-P",
    env.DEPLOY_SSH_PORT || "22",
    "-o",
    "BatchMode=yes",
    "-o",
    "StrictHostKeyChecking=accept-new",
  ];
  if (env.DEPLOY_SSH_KEY) {
    args.push("-i", env.DEPLOY_SSH_KEY, "-o", "IdentitiesOnly=yes");
  }
  return args;
}

function sshTarget(env) {
  return `${required(env, "DEPLOY_SSH_USER")}@${required(env, "DEPLOY_SSH_HOST")}`;
}

async function collectAssetFiles(dir) {
  /** @type {{ abs: string, key: string, size: number }[]} */
  const files = [];
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
        await walk(abs);
        continue;
      }
      if (SKIP_ASSET_NAMES.has(entry.name.toLowerCase())) continue;
      const info = await stat(abs);
      if (!info.isFile()) continue;
      const rel = path.relative(PLACEHOLDERS_DIR, abs).split(path.sep).join("/");
      files.push({ abs, key: rel, size: info.size });
    }
  }
  await walk(dir);
  return files;
}

function hasCosConfig(env) {
  return Boolean(
    env.COS_SECRET_ID?.trim() &&
      env.COS_SECRET_KEY?.trim() &&
      env.COS_BUCKET?.trim() &&
      env.COS_REGION?.trim(),
  );
}

async function syncAssets(env, flags) {
  const files = await collectAssetFiles(PLACEHOLDERS_DIR);
  if (files.length === 0) {
    console.log("COS：public/placeholders 下没有待传媒体，跳过。");
    return;
  }
  if (!hasCosConfig(env)) {
    throw new Error(
      `检测到 ${files.length} 个本地媒体文件，但 COS 密钥未配置。请填写 .env.deploy，或改用 npm run deploy -- --spa`,
    );
  }
  if (flags.dryRun) {
    console.log(`COS 预演：将同步 ${files.length} 个对象（键与 placeholders 相对路径一致）`);
    for (const file of files.slice(0, 20)) {
      console.log(`  ${file.key}  (${file.size} B)`);
    }
    if (files.length > 20) console.log(`  … 其余 ${files.length - 20} 个`);
    return;
  }

  let COS;
  try {
    COS = require("cos-nodejs-sdk-v5");
  } catch {
    throw new Error("缺少 cos-nodejs-sdk-v5，请在 PersonalSite 执行 npm install");
  }

  const bucket = required(env, "COS_BUCKET");
  const region = required(env, "COS_REGION");
  const cos = new COS({
    SecretId: required(env, "COS_SECRET_ID"),
    SecretKey: required(env, "COS_SECRET_KEY"),
    FileParallelLimit: 3,
    ChunkParallelLimit: 3,
  });

  /** @type {Map<string, number>} */
  const remoteSizeDict = new Map();
  let marker = "";
  do {
    const listed = await cos.getBucket({
      Bucket: bucket,
      Region: region,
      Prefix: "",
      Marker: marker,
      MaxKeys: 1000,
    });
    for (const item of listed.Contents ?? []) {
      remoteSizeDict.set(item.Key, Number(item.Size));
    }
    marker = listed.IsTruncated === "true" ? (listed.NextMarker ?? "") : "";
  } while (marker);

  let uploaded = 0;
  let skipped = 0;
  for (const file of files) {
    const remoteSize = remoteSizeDict.get(file.key);
    if (remoteSize === file.size) {
      skipped += 1;
      continue;
    }
    const isHashedBuild = /\/Build\/.*[.-][a-f0-9]{8,}\./i.test(file.key);
    const cacheControl = isHashedBuild
      ? "public, max-age=2592000"
      : "public, max-age=3600";
    console.log(`COS 上传 ${file.key}`);
    await withTransientRetry(
      () =>
        cos.uploadFile({
          Bucket: bucket,
          Region: region,
          Key: file.key,
          FilePath: file.abs,
          SliceSize: 8 * 1024 * 1024,
          Headers: { "Cache-Control": cacheControl },
        }),
      {
        onRetry(error, attempt) {
          const code =
            error && typeof error === "object" && "code" in error
              ? String(error.code)
              : "未知";
          console.log(`COS 重试 ${file.key}（第 ${attempt} 次，${code}）`);
        },
      },
    );
    uploaded += 1;
  }

  if (flags.pruneAssets) {
    const localKeys = new Set(files.map((file) => file.key));
    for (const key of remoteSizeDict.keys()) {
      if (localKeys.has(key)) continue;
      console.log(`COS 删除多余对象 ${key}`);
      await cos.deleteObject({ Bucket: bucket, Region: region, Key: key });
    }
  }

  console.log(`COS 完成：上传 ${uploaded}，跳过未改 ${skipped}`);
}

async function buildSpa(env, flags) {
  if (flags.skipBuild) {
    await stat(path.join(DIST_DIR, "index.html"));
    console.log("跳过构建，使用已有 dist/");
    return;
  }
  const assetBase = env.VITE_ASSET_BASE?.trim() ?? "";
  console.log(`构建中（VITE_ASSET_BASE=${assetBase || "(空，走 /placeholders)"}）`);
  if (flags.dryRun) return;
  await run("npm", ["run", "build"], {
    env: { ...process.env, VITE_ASSET_BASE: assetBase },
    shell: process.platform === "win32",
  });
  await stat(path.join(DIST_DIR, "index.html"));
}

function remoteDeployScript(remoteRoot, nginxCmd) {
  assertSafeRemotePath(remoteRoot);
  const nginxLine = nginxCmd
    ? nginxCmd
    : `if command -v nginx >/dev/null 2>&1; then
  nginx -t && nginx -s reload
elif [ -x /www/server/nginx/sbin/nginx ]; then
  /www/server/nginx/sbin/nginx -t && /www/server/nginx/sbin/nginx -s reload
else
  echo "未找到 nginx 可执行文件" >&2
  exit 1
fi`;
  return `set -euo pipefail
ROOT="${remoteRoot}"
BACKUP_ROOT="/www/backup/duhongbo.com"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP="$BACKUP_ROOT/$STAMP"
INCOMING="\${ROOT}.incoming"
TAR="${REMOTE_TAR}"

if [ ! -f "$TAR" ]; then
  echo "缺少 $TAR" >&2
  exit 1
fi

mkdir -p "$BACKUP_ROOT" "$INCOMING"
if [ -f "$ROOT/index.html" ]; then
  mkdir -p "$BACKUP"
  cp -a "$ROOT/." "$BACKUP/"
fi

tar -xzf "$TAR" -C "$INCOMING"
mkdir -p "$ROOT"
rm -rf "$ROOT/assets" "$ROOT/placeholders"
cp -a "$INCOMING/." "$ROOT/"
rm -rf "$INCOMING" "$TAR"

KEEP=${BACKUP_KEEP}
if [ -d "$BACKUP_ROOT" ]; then
  ls -1dt "$BACKUP_ROOT"/*/ 2>/dev/null | tail -n +$((KEEP + 1)) | while IFS= read -r dir; do
  rm -rf "$dir"
done
fi

${nginxLine}
echo "发布完成：$ROOT （备份 $BACKUP）"
`;
}

async function deploySpa(env, flags) {
  const remoteRoot = required(env, "DEPLOY_REMOTE_ROOT");
  assertSafeRemotePath(remoteRoot);
  const target = sshTarget(env);
  const workDir = await mkdtemp(path.join(tmpdir(), "personal-site-deploy-"));
  const stagingDir = path.join(workDir, "dist");
  const tarPath = path.join(workDir, TAR_NAME);

  try {
    console.log("打包 dist（排除 placeholders，媒体不进轻量盘）");
    if (!flags.dryRun) {
      await cp(DIST_DIR, stagingDir, {
        recursive: true,
        filter: (src) => {
          const rel = path.relative(DIST_DIR, src);
          if (!rel || rel === ".") return true;
          const top = rel.split(path.sep)[0];
          return top !== "placeholders";
        },
      });
      await run("tar", ["-czf", tarPath, "-C", stagingDir, "."]);
    }

    console.log(`上传到 ${target}:${REMOTE_TAR}`);
    if (!flags.dryRun) {
      await run("scp", [...scpArgs(env), tarPath, `${target}:${REMOTE_TAR}`]);
    }

    console.log("远端覆盖网站根目录并重载 Nginx");
    if (!flags.dryRun) {
      await run("ssh", [...sshArgs(env), target, "bash", "-s"], {
        input: remoteDeployScript(remoteRoot, env.DEPLOY_NGINX_CMD?.trim() ?? ""),
      });
    } else {
      console.log(`  远程根目录 ${remoteRoot}`);
      console.log("  将保留 .well-known，替换 assets，删除误放到站点根的 placeholders");
    }
  } finally {
    await rm(workDir, { recursive: true, force: true });
  }
}

async function main() {
  const flags = parseArgs(process.argv);
  let envText;
  try {
    envText = await readFile(ENV_FILE, "utf8");
  } catch {
    throw new Error("未找到 .env.deploy。请复制 .env.deploy.example 为 .env.deploy 并填写。");
  }
  const env = parseEnvFile(envText);
  const doSpa = !flags.assets;
  const doAssets = !flags.spa;
  const localAssets = await collectAssetFiles(PLACEHOLDERS_DIR);

  if (flags.dryRun) console.log("—— 预演模式，不会改服务器或 COS ——");
  if (doSpa && !env.VITE_ASSET_BASE?.trim() && localAssets.length > 0) {
    console.log(
      "注意：VITE_ASSET_BASE 为空且本地已有媒体。静态包不会把 placeholders 传到轻量盘，线上图应走 COS/CDN。",
    );
  }
  if (doSpa) await buildSpa(env, flags);
  if (doAssets) await syncAssets(env, flags);
  if (doSpa) await deploySpa(env, flags);
  console.log(flags.dryRun ? "预演结束。" : "全部完成。");
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
