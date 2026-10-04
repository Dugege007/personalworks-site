# 站点媒体生命周期

把中转站网图登记进台账并复制到正式位（中转站文件保留）；下架时按对象键删除正式位与 COS。再用时中转站仍有该路径则直接入库，缺失则人工放回。不读仓库外原片库，不转码、不改内容层 TypeScript、不调用 `--prune-assets`。方案见 `Docs/工具开发/站点媒体生命周期.md`。

## 配置

可选：复制 `.env.media.example` 为 `.env.media`，填写 `WORKSPACE_ROOT`（默认仓库根）。环境变量同名键优先。不需要 `ORIGIN_LIBRARY_ROOT`。

COS 凭证只读 `PersonalSite/.env.deploy`，与发布脚本相同。未配置时 `--apply` 只删本地正式位，并打印跳过远端。

中转站补充资源仅人工放入。`ingest` 只检查中转站是否已有该文件。

## 调用

Agent 操作入库 / 取消显示 / 撤下时走项目技能 `sitemedia`（`.cursor/skills/sitemedia`），闸门见 `.cursor/rules/sitemedia-lifecycle.mdc`。也可直接调用 `sitemedia.bat`。

```powershell
node sitemedia.mjs status
node sitemedia.mjs status --key profile
node sitemedia.mjs status profile/portrait.webp
node sitemedia.mjs ingest <stageRel> --object <object>
node sitemedia.mjs ingest <stageRel> --object <object> --bump
node sitemedia.mjs ingest <stageRel> --object <object> --source-stage-rel <sourceStageRel>
node sitemedia.mjs reconcile <stageRel> --object <object> --dry-run
node sitemedia.mjs reconcile <stageRel> --object <object> --apply
node sitemedia.mjs withdraw <object> --dry-run
node sitemedia.mjs withdraw <object> --apply
node sitemedia.mjs purge --object <object>
node sitemedia.mjs purge --dry-run --object <object>
python prepare-initial-batch.py --batch .\batches\initial-projects.json --stage-root ..\..\作品中转站 --dry-run
python prepare-initial-batch.py --batch .\batches\initial-projects.json --stage-root ..\..\作品中转站 --apply
python prepare-initial-batch.py --source <源图> --output <成片.webp> --apply
node ingest-initial-batch.mjs --batch .\batches\initial-projects.json --dry-run
node ingest-initial-batch.mjs --batch .\batches\initial-projects.json --apply
```

| 参数 | 说明 |
|------|------|
| `<stageRel>` | 相对 `作品中转站/`，可含公司/项目子目录 |
| `--object` | 相对 `placeholders/` 的 COS 键，须带栏目 `key/` |
| `--key` | `status` 只看该栏目 |
| `--dry-run` | `withdraw` 或 `reconcile` 只报告，不写盘 |
| `--apply` | `withdraw` 删除引用为零的对象；`reconcile` 只补 published 台账 |
| `--bump` | `ingest`：对已 withdrawn 的 `--object` 生成 `-vN` 新键，旧键保持 withdrawn |
| `--source-stage-rel` | `ingest`：成片 `stageRel` 指向 `.site-ready` 时记下中转站原片，供对照认领 |
| `--url` | `purge`：完整 HTTPS URL，可重复 |

`ingest` 不转码，也不移动、不删除中转站文件。中转站文件扩展名须与 `object` 一致。同一 `stageRel` 已 `published` 时不可再登记到其它 `object`。`{key}/stock/` 拒绝。`restage` 与 `--origin-rel` 已取消。

不得把原始 JPG / PNG / GIF 直接交给 `ingest`。`prepare-initial-batch.py` 在源图同目录的 `.site-ready/<项目 id>/` 生成 WebP，长边最多 2560，必要时降到 1920，去元数据，原图不覆盖；批次模式全部成功后才原子更新清单，单张模式只写指定成片。网站资源编辑工具执行上页时走单张模式。`ingest-initial-batch.mjs` 先整批预检，再逐项调用既有 `sitemedia ingest`，可断点续跑，不绕过台账闸门。

`reconcile` 用于正式位已有、台账缺失的历史对象。执行前须正向确认中转站源；它允许明确的 JPG → WebP 转码映射，不复制、覆盖或删除媒体。内容引用扫描会静态展开 `album`、`cover`、`covered`、`listed` 与显式 `src`，在用动态对象不能撤下。

## 顺序

1. 人工将源图放入中转站 → 压成网页规格（窗口执行上页，或 `prepare-initial-batch.py`）→ `ingest` 成片 → 内容层写 `src` → `npm run deploy`
2. 历史正式位先确认中转站源 → `reconcile --dry-run` → `reconcile --apply`
3. 去掉内容层全部引用 → `npm run deploy -- --spa` → `withdraw --dry-run` → `withdraw --apply` → `purge --object <object>`
4. 中转站仍有该路径则直接 `ingest`；缺失则人工放回。默认同键；`--bump` 换 `-vN` 并改内容层 `src`
