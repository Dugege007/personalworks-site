import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, rm, stat, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const CLI = path.join(TOOL_DIR, "sitemedia.mjs");

function runCli(workspaceRoot, args) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [CLI, ...args], {
      cwd: TOOL_DIR,
      env: {
        ...process.env,
        WORKSPACE_ROOT: workspaceRoot,
        SITEMEDIA_STAGE_ROOT: path.join(workspaceRoot, "作品中转站"),
        SITEMEDIA_PLACEHOLDERS_ROOT: path.join(
          workspaceRoot,
          "PersonalSite",
          "public",
          "placeholders",
        ),
        SITEMEDIA_CONTENT_ROOT: path.join(workspaceRoot, "PersonalSite", "src"),
        SITEMEDIA_LEDGER: path.join(
          workspaceRoot,
          "PersonalSite",
          "src",
          "content",
          "media-ledger.json",
        ),
        SITEMEDIA_DEPLOY_ENV: path.join(workspaceRoot, "PersonalSite", ".env.deploy"),
      },
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

async function writeWorkspaceFile(workspaceRoot, rel, contents) {
  const abs = path.join(workspaceRoot, ...rel.split("/"));
  await mkdir(path.dirname(abs), { recursive: true });
  await writeFile(abs, contents);
  return abs;
}

async function createWorkspace(t, records = []) {
  const root = await mkdtemp(path.join(os.tmpdir(), "sitemedia-test-"));
  t.after(async () => {
    await rm(root, { recursive: true, force: true });
  });
  const workspaceRoot = path.join(root, "workspace");
  const ledgerPath = await writeWorkspaceFile(
    workspaceRoot,
    "PersonalSite/src/content/media-ledger.json",
    `${JSON.stringify({ version: 1, records }, null, 2)}\n`,
  );
  return { workspaceRoot, ledgerPath };
}

test("status 合并 album、cover、covered、listed、src 与字面引用", async (t) => {
  const objects = [
    "demo/album/01.jpg",
    "demo/album/02.jpg",
    "demo/album-webp/01.webp",
    "demo/cover/01.webp",
    "demo/covered/01.webp",
    "demo/listed/01.jpg",
    "demo/explicit/01.png",
    "demo/literal/01.jpg",
  ];
  const records = objects.map((object) => ({
    object,
    stageRel: `来源/${object.replaceAll("/", "-")}`,
    status: "published",
  }));
  const { workspaceRoot } = await createWorkspace(t, records);
  const works = [
    'const a = album("demo", "album", 2);',
    'const b = album("demo", "album-webp", 1, "webp");',
    'const c = cover("demo", "cover", "封面");',
    'const d = covered("demo", "covered", ["首图", "说明"]);',
    "const e = listed([",
    '  ["demo/listed/01.jpg", "列表图"],',
    "]);",
    'const f = { src: "demo/explicit/01.png" };',
    'const g = "/placeholders/demo/literal/01.jpg";',
    "",
  ].join("\n");
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", works);
  for (const record of records) {
    await writeWorkspaceFile(workspaceRoot, `作品中转站/${record.stageRel}`, "stage");
    await writeWorkspaceFile(
      workspaceRoot,
      `PersonalSite/public/placeholders/${record.object}`,
      "published",
    );
  }

  const result = await runCli(workspaceRoot, ["status", "--key", "demo"]);
  assert.equal(result.code, 0, result.stderr);
  assert.equal(
    [...result.stdout.matchAll(/内容层引用：1/g)].length,
    objects.length,
    "字面引用与静态展开引用应按文件、行去重",
  );
  assert.match(result.stdout, /对象：demo\/album\/01\.jpg[\s\S]*works\.ts:1（album 静态展开）/);
  assert.match(result.stdout, /对象：demo\/album\/02\.jpg[\s\S]*works\.ts:1（album 静态展开）/);
  assert.match(
    result.stdout,
    /对象：demo\/album-webp\/01\.webp[\s\S]*works\.ts:2（album 静态展开）/,
  );
  assert.match(result.stdout, /对象：demo\/cover\/01\.webp[\s\S]*works\.ts:3（cover 静态展开）/);
  assert.match(
    result.stdout,
    /对象：demo\/covered\/01\.webp[\s\S]*works\.ts:4（covered 静态展开）/,
  );
  assert.match(result.stdout, /对象：demo\/listed\/01\.jpg[\s\S]*works\.ts:6/);
  assert.match(result.stdout, /对象：demo\/explicit\/01\.png[\s\S]*works\.ts:8/);
  assert.match(result.stdout, /对象：demo\/literal\/01\.jpg[\s\S]*works\.ts:9/);
});

test("动态展开对象可预演但不可撤下", async (t) => {
  const object = "demo/album/01.jpg";
  const stageRel = "来源/album-01.jpg";
  const { workspaceRoot, ledgerPath } = await createWorkspace(t, [
    { object, stageRel, status: "published" },
  ]);
  await writeWorkspaceFile(
    workspaceRoot,
    "PersonalSite/src/content/works.ts",
    'const media = album("demo", "album", 1);\n',
  );
  const stageAbs = await writeWorkspaceFile(workspaceRoot, `作品中转站/${stageRel}`, "stage");
  const destAbs = await writeWorkspaceFile(
    workspaceRoot,
    `PersonalSite/public/placeholders/${object}`,
    "published",
  );

  const dryRun = await runCli(workspaceRoot, ["withdraw", object, "--dry-run"]);
  assert.equal(dryRun.code, 0, dryRun.stderr);
  assert.match(dryRun.stdout, /works\.ts:1（album 静态展开）/);
  assert.match(dryRun.stdout, /预演结束，未删除任何文件/);

  const apply = await runCli(workspaceRoot, ["withdraw", object, "--apply"]);
  assert.notEqual(apply.code, 0);
  assert.match(apply.stderr, /静态展开引用 1 处/);
  assert.equal(await readFile(stageAbs, "utf8"), "stage");
  assert.equal(await readFile(destAbs, "utf8"), "published");
  const ledger = JSON.parse(await readFile(ledgerPath, "utf8"));
  assert.equal(ledger.records[0].status, "published");
});

test("reconcile 支持已转码映射且只写台账", async (t) => {
  const stageRel = "来源/portrait.jpg";
  const object = "profile/portrait.webp";
  const { workspaceRoot, ledgerPath } = await createWorkspace(t);
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", "export {};\n");
  const stageAbs = await writeWorkspaceFile(workspaceRoot, `作品中转站/${stageRel}`, "jpg-source");
  const destAbs = await writeWorkspaceFile(
    workspaceRoot,
    `PersonalSite/public/placeholders/${object}`,
    "webp-published",
  );

  const dryRun = await runCli(workspaceRoot, [
    "reconcile",
    stageRel,
    "--object",
    object,
    "--dry-run",
  ]);
  assert.equal(dryRun.code, 0, dryRun.stderr);
  assert.match(dryRun.stdout, /扩展名映射：\.jpg → \.webp/);
  assert.match(dryRun.stdout, /不复制、不覆盖、不删除/);
  assert.deepEqual(JSON.parse(await readFile(ledgerPath, "utf8")).records, []);

  const apply = await runCli(workspaceRoot, [
    "reconcile",
    stageRel,
    "--object",
    object,
    "--apply",
  ]);
  assert.equal(apply.code, 0, apply.stderr);
  assert.equal(await readFile(stageAbs, "utf8"), "jpg-source");
  assert.equal(await readFile(destAbs, "utf8"), "webp-published");
  assert.deepEqual(JSON.parse(await readFile(ledgerPath, "utf8")).records, [
    { object, stageRel, status: "published" },
  ]);
});

test("reconcile 拒绝 object 与 stageRel 冲突", async (t) => {
  const stageRel = "来源/existing.jpg";
  const existingObject = "demo/existing.webp";
  const alternateObject = "demo/alternate.webp";
  const { workspaceRoot } = await createWorkspace(t, [
    { object: existingObject, stageRel, status: "published" },
  ]);
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", "export {};\n");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${stageRel}`, "source");
  await writeWorkspaceFile(
    workspaceRoot,
    `PersonalSite/public/placeholders/${existingObject}`,
    "existing",
  );
  await writeWorkspaceFile(
    workspaceRoot,
    `PersonalSite/public/placeholders/${alternateObject}`,
    "alternate",
  );

  const objectConflict = await runCli(workspaceRoot, [
    "reconcile",
    stageRel,
    "--object",
    existingObject,
    "--apply",
  ]);
  assert.notEqual(objectConflict.code, 0);
  assert.match(objectConflict.stderr, /object 已有台账记录/);

  const stageConflict = await runCli(workspaceRoot, [
    "reconcile",
    stageRel,
    "--object",
    alternateObject,
    "--apply",
  ]);
  assert.notEqual(stageConflict.code, 0);
  assert.match(stageConflict.stderr, /中转站路径已被 published 对象占用/);
});

test("ingest 写入 sourceStageRel 且拒绝占用原片", async (t) => {
  const sourceRel = "来源/01 clip.mp4";
  const preparedRel = "来源/.site-ready/demo/08.mp4";
  const object = "demo/clip/08.mp4";
  const { workspaceRoot, ledgerPath } = await createWorkspace(t);
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", "export {};\n");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${sourceRel}`, "source-mp4");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${preparedRel}`, "prepared-mp4");

  const missing = await runCli(workspaceRoot, [
    "ingest",
    preparedRel,
    "--object",
    object,
    "--source-stage-rel",
    "来源/missing.mp4",
  ]);
  assert.notEqual(missing.code, 0);
  assert.match(missing.stderr, /中转站没有来源文件/);

  const ok = await runCli(workspaceRoot, [
    "ingest",
    preparedRel,
    "--object",
    object,
    "--source-stage-rel",
    sourceRel,
  ]);
  assert.equal(ok.code, 0, ok.stderr);
  assert.deepEqual(JSON.parse(await readFile(ledgerPath, "utf8")).records, [
    { object, stageRel: preparedRel, status: "published", sourceStageRel: sourceRel },
  ]);

  await writeWorkspaceFile(workspaceRoot, "作品中转站/来源/.site-ready/demo/09.mp4", "other");
  const occupied = await runCli(workspaceRoot, [
    "ingest",
    "来源/.site-ready/demo/09.mp4",
    "--object",
    "demo/clip/09.mp4",
    "--source-stage-rel",
    sourceRel,
  ]);
  assert.notEqual(occupied.code, 0);
  assert.match(occupied.stderr, /中转站来源路径已登记给 published 对象/);
});

test("撤下后的中转路径可入库到新对象键，published 仍拒绝", async (t) => {
  const sourceRel = "来源/01.jpg";
  const oldPrepared = "来源/.site-ready/old-id/01.webp";
  const newPrepared = "来源/.site-ready/new-folder/01.webp";
  const oldObject = "landscape-photo/old-id/01.webp";
  const samePathObject = "real-world-photo/new-folder/02.webp";
  const sourceObject = "real-world-photo/new-folder/01.webp";
  const blockedObject = "real-world-photo/new-folder/03.webp";
  const { workspaceRoot, ledgerPath } = await createWorkspace(t, [
    {
      object: oldObject,
      stageRel: oldPrepared,
      status: "withdrawn",
      sourceStageRel: sourceRel,
    },
  ]);
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", "export {};\n");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${sourceRel}`, "jpg");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${oldPrepared}`, "old-webp");
  await writeWorkspaceFile(workspaceRoot, `作品中转站/${newPrepared}`, "new-webp");

  const samePath = await runCli(workspaceRoot, [
    "ingest",
    oldPrepared,
    "--object",
    samePathObject,
  ]);
  assert.equal(samePath.code, 0, samePath.stderr);

  const viaSource = await runCli(workspaceRoot, [
    "ingest",
    newPrepared,
    "--object",
    sourceObject,
    "--source-stage-rel",
    sourceRel,
  ]);
  assert.equal(viaSource.code, 0, viaSource.stderr);

  const ledger = JSON.parse(await readFile(ledgerPath, "utf8"));
  const oldRow = ledger.records.find((item) => item.object === oldObject);
  const samePathRow = ledger.records.find((item) => item.object === samePathObject);
  const sourceRow = ledger.records.find((item) => item.object === sourceObject);
  assert.equal(oldRow.status, "withdrawn");
  assert.equal(oldRow.stageRel, oldPrepared);
  assert.equal(samePathRow.status, "published");
  assert.equal(samePathRow.stageRel, oldPrepared);
  assert.equal(sourceRow.status, "published");
  assert.equal(sourceRow.sourceStageRel, sourceRel);

  const blocked = await runCli(workspaceRoot, [
    "ingest",
    oldPrepared,
    "--object",
    blockedObject,
  ]);
  assert.notEqual(blocked.code, 0);
  assert.match(blocked.stderr, /中转站路径已登记给 published 对象/);
});

async function pathExists(abs) {
  try {
    await stat(abs);
    return true;
  } catch {
    return false;
  }
}

test("撤下最后一张后删除作品目录，空栏目夹与中转站保留", async (t) => {
  const keep = "landscape-photo/keep/01.webp";
  const dropA = "landscape-photo/drop/01.webp";
  const dropB = "landscape-photo/drop/02.webp";
  const lonely = "lonely-photo/only/01.webp";
  const { workspaceRoot } = await createWorkspace(t, [
    { object: keep, stageRel: "来源/keep.webp", status: "published" },
    { object: dropA, stageRel: "来源/a.webp", status: "published" },
    { object: dropB, stageRel: "来源/b.webp", status: "published" },
    { object: lonely, stageRel: "来源/lonely.webp", status: "published" },
  ]);
  await writeWorkspaceFile(workspaceRoot, "PersonalSite/src/content/works.ts", "export {};\n");
  const stageAbs = await writeWorkspaceFile(workspaceRoot, "作品中转站/来源/a.webp", "stage-a");
  await writeWorkspaceFile(workspaceRoot, "作品中转站/来源/b.webp", "stage-b");
  await writeWorkspaceFile(workspaceRoot, "作品中转站/来源/lonely.webp", "stage-lonely");
  await writeWorkspaceFile(workspaceRoot, `PersonalSite/public/placeholders/${keep}`, "keep");
  await writeWorkspaceFile(workspaceRoot, `PersonalSite/public/placeholders/${dropA}`, "a");
  await writeWorkspaceFile(workspaceRoot, `PersonalSite/public/placeholders/${dropB}`, "b");
  await writeWorkspaceFile(workspaceRoot, `PersonalSite/public/placeholders/${lonely}`, "lonely");
  const placeholders = path.join(workspaceRoot, "PersonalSite", "public", "placeholders");

  const first = await runCli(workspaceRoot, ["withdraw", dropA, "--apply"]);
  assert.equal(first.code, 0, first.stderr);
  assert.equal(await pathExists(path.join(placeholders, "landscape-photo", "drop")), true);
  assert.doesNotMatch(first.stdout, /已删除空目录/);
  assert.equal(await readFile(stageAbs, "utf8"), "stage-a");

  const second = await runCli(workspaceRoot, ["withdraw", dropB, "--apply"]);
  assert.equal(second.code, 0, second.stderr);
  assert.match(second.stdout, /已删除空目录/);
  assert.equal(await pathExists(path.join(placeholders, "landscape-photo", "drop")), false);
  assert.equal(await pathExists(path.join(placeholders, "landscape-photo", "keep")), true);
  assert.equal(await readFile(path.join(placeholders, "landscape-photo", "keep", "01.webp"), "utf8"), "keep");
  assert.equal(await readFile(stageAbs, "utf8"), "stage-a");

  const last = await runCli(workspaceRoot, ["withdraw", lonely, "--apply"]);
  assert.equal(last.code, 0, last.stderr);
  assert.equal(await pathExists(path.join(placeholders, "lonely-photo", "only")), false);
  assert.equal(await pathExists(path.join(placeholders, "lonely-photo")), true);
  assert.equal(await readFile(path.join(workspaceRoot, "作品中转站", "来源", "lonely.webp"), "utf8"), "stage-lonely");
});
