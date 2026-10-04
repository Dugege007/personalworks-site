/**
 * 阶段3 隔离验收：待补登记、stock 报告、key 过滤；stock 不可 withdraw。
 * 在 工具/站点媒体生命周期 目录执行：node verify-stage3.mjs
 */

import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const CLI = path.join(TOOL_DIR, "sitemedia.mjs");

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

async function main() {
  const root = await mkdtemp(path.join(os.tmpdir(), "sitemedia-stage3-"));
  const workspace = path.join(root, "ws");
  const env = {
    WORKSPACE_ROOT: workspace,
  };
  const portrait = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    "profile",
    "portrait.webp",
  );
  const stock = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    "landscape-cds",
    "stock",
    "01.webp",
  );
  const leftover = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    "digital-twin",
    "ghost",
    "cover.webp",
  );
  const okDest = path.join(
    workspace,
    "PersonalSite",
    "public",
    "placeholders",
    "digital-twin",
    "ok",
    "cover.webp",
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
    await mkdir(path.dirname(portrait), { recursive: true });
    await mkdir(path.dirname(stock), { recursive: true });
    await mkdir(path.dirname(leftover), { recursive: true });
    await mkdir(path.dirname(okDest), { recursive: true });
    await mkdir(path.dirname(ledgerPath), { recursive: true });
    await writeFile(portrait, "portrait-bytes");
    await writeFile(stock, "stock-bytes");
    await writeFile(leftover, "ghost-bytes");
    await writeFile(okDest, "ok-bytes");
    await writeFile(
      worksPath,
      'export const a = { src: "profile/portrait.webp" };\nexport const b = { src: "landscape-cds/stock/01.webp" };\n',
    );
    await writeFile(
      ledgerPath,
      `${JSON.stringify(
        {
          version: 1,
          records: [
            {
              object: "digital-twin/ok/cover.webp",
              originRel: "数字孪生（digital-twin）/ok/cover.webp",
              stageRel: "数字孪生（digital-twin）/ok/cover.webp",
              status: "published",
            },
            {
              object: "digital-twin/ghost/cover.webp",
              originRel: "数字孪生（digital-twin）/ghost/cover.webp",
              stageRel: "数字孪生（digital-twin）/ghost/cover.webp",
              status: "withdrawn",
            },
          ],
        },
        null,
        2,
      )}\n`,
    );

    const all = await run(env, ["status"]);
    assert(all.code === 0, `status 应成功：${all.stderr}`);
    assert(all.stdout.includes("待补登记：1"), `待补登记计数不符：\n${all.stdout}`);
    assert(all.stdout.includes("profile/portrait.webp"), "形象照应列为待补登记");
    assert(all.stdout.includes("常驻占位：1"), `stock 计数不符：\n${all.stdout}`);
    assert(all.stdout.includes("landscape-cds/stock/01.webp"), "stock 应出现在报告中");
    assert(all.stdout.includes("不可 withdraw"), "stock 应标明不可 withdraw");
    assert(all.stdout.includes("中转站缺失：1"), `中转站缺失计数不符：\n${all.stdout}`);
    assert(all.stdout.includes("幽灵：1"), `幽灵计数不符：\n${all.stdout}`);
    assert(all.stdout.includes("幽灵：withdrawn 仍有正式位"), "幽灵备注应出现");

    const byKey = await run(env, ["status", "--key", "profile"]);
    assert(byKey.code === 0, `status --key 应成功：${byKey.stderr}`);
    assert(byKey.stdout.includes("profile/portrait.webp"), "key=profile 应含形象照");
    assert(!byKey.stdout.includes("landscape-cds/stock/01.webp"), "key=profile 不应列出施工图 stock");
    assert(!byKey.stdout.includes("digital-twin/ghost/cover.webp"), "key=profile 不应列出其它台账");

    const byPosKey = await run(env, ["status", "profile"]);
    assert(byPosKey.code === 0, "status profile 应视为 key");
    assert(byPosKey.stdout.includes("profile/portrait.webp"), "位置参数 key 应含形象照");

    const one = await run(env, ["status", "profile/portrait.webp"]);
    assert(one.code === 0, `status 单对象应成功：${one.stderr}`);
    assert(one.stdout.includes("待补登记：1"), "单对象待补登记");
    assert(!one.stdout.includes("landscape-cds/stock/01.webp"), "单对象不应带出 stock");

    const blocked = await run(env, ["withdraw", "landscape-cds/stock/01.webp", "--dry-run"]);
    assert(blocked.code !== 0, "stock 的 withdraw 应失败");
    assert(
      blocked.stderr.includes("stock"),
      `stock 拒绝报错不符：${blocked.stderr}`,
    );

    console.log("阶段3 隔离验收通过。");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
