import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const workspace = path.join(here, "..", "..");
const manifestPath = path.join(
  workspace,
  "工具",
  "站点媒体生命周期",
  "batches",
  "initial-projects.json",
);
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));

test("首轮安全项目每项最多选择8张且对象键完整", () => {
  const allowed = new Set([
    "digital-twin",
    "game-dev",
    "landscape-rendering",
    "landscape-photo",
    "humanist-photo",
  ]);
  for (const project of manifest.projects) {
    assert.ok(allowed.has(project.channel), project.channel);
    const selected = project.media.filter((item) => item.action !== "skip");
    assert.ok(selected.length > 0 && selected.length <= 8, project.id);
    for (const media of selected) {
      assert.ok(media.stageRel);
      assert.ok(media.object.startsWith(`${project.channel}/`));
      assert.equal(media.skipReason, "");
      assert.ok(
        fs.existsSync(path.join(workspace, "作品中转站", ...media.stageRel.split("/"))),
        media.stageRel,
      );
      if (media.action === "ingest") {
        assert.equal(path.extname(media.stageRel).toLowerCase(), ".webp");
        assert.equal(path.extname(media.object).toLowerCase(), ".webp");
        assert.ok(media.stageRel.includes(`/.site-ready/${project.id}/`), media.stageRel);
        assert.ok(media.sourceStageRel);
        assert.ok(media.sourceObject);
        assert.ok(
          fs.existsSync(path.join(workspace, "作品中转站", ...media.sourceStageRel.split("/"))),
          media.sourceStageRel,
        );
        assert.equal(media.prepared?.format, "webp");
        assert.equal(media.prepared?.metadataStripped, true);
        assert.ok(Math.max(media.prepared?.width ?? 0, media.prepared?.height ?? 0) <= 2560);
        assert.equal(
          fs.statSync(path.join(workspace, "作品中转站", ...media.stageRel.split("/"))).size,
          media.prepared?.outputBytes,
        );
      }
    }
  }
  const objects = manifest.projects.flatMap((project) =>
    project.media.filter((item) => item.action !== "skip").map((item) => item.object),
  );
  assert.equal(new Set(objects).size, objects.length);
});

test("首轮内容源只引用清单选中的正式对象键", () => {
  const contentText = [
    fs.readFileSync(path.join(workspace, "PersonalSite", "src", "content", "initialWorkProjects.ts"), "utf8"),
    fs.readFileSync(path.join(workspace, "PersonalSite", "src", "content", "initialGameProjects.ts"), "utf8"),
    fs.readFileSync(path.join(workspace, "PersonalSite", "src", "content", "site.ts"), "utf8"),
    fs.readFileSync(path.join(workspace, "PersonalSite", "src", "content", "stockMedia.ts"), "utf8"),
  ].join("\n");
  const selectedObjects = manifest.projects.flatMap((project) =>
    project.media.filter((item) => item.action !== "skip").map((item) => item.object),
  );
  for (const object of selectedObjects) {
    assert.ok(contentText.includes(`"${object}"`), object);
  }
  for (const project of manifest.projects) {
    for (const media of project.media.filter((item) => item.action === "ingest")) {
      assert.ok(!contentText.includes(`"${media.sourceObject}"`), media.sourceObject);
    }
  }
});

test("未授权人像与排除媒体只有 skip 动作", () => {
  for (const project of manifest.excludedProjects) {
    assert.equal(project.channel, "portrait-photo");
    assert.ok(project.media.every((item) => item.action === "skip"));
    assert.ok(project.media.every((item) => item.skipReason.includes("授权")));
  }
  assert.ok(manifest.excludedMedia.every((item) => item.action === "skip"));
});

test("既有景观对象键保持不变", () => {
  const xiaowayao = manifest.projects.find((project) => project.id === "xiaowayao");
  assert.deepEqual(
    xiaowayao.media
      .filter((item) => item.action === "existing")
      .map((item) => item.object),
    Array.from({ length: 8 }, (_, index) =>
      `landscape-rendering/xiaowayao/${String(index + 1).padStart(2, "0")}.jpg`,
    ),
  );
});
