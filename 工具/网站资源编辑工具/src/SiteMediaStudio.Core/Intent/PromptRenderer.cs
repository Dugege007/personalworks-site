using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 生成预演文本与对话提示词。
/// </summary>
public static class PromptRenderer
{
    /// <summary>
    /// 把预演报告写成只读说明。
    /// </summary>
    public static string RenderPreview(PreviewReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"工作区：{report.Document.ProfileName}");
        builder.AppendLine($"根目录：{report.Document.WorkspaceRoot}");
        builder.AppendLine($"模式：{report.Document.Mode}");
        builder.AppendLine($"条数：{report.Lines.Count}");
        builder.AppendLine();
        foreach (var line in report.Lines)
        {
            var mark = !line.Decision.Allowed
                ? "拒绝"
                : IntentGate.IsRedundantSkipDecision(line.Decision)
                    ? "跳过"
                    : line.Decision.IsWarn
                        ? "警告"
                        : "允许";

            var target = line.Item.Intent == MediaIntentCodes.StageRelocate
                ? $"{line.Item.Object}  {line.Item.StageRelBefore} → {line.Item.StageRelAfter}"
                : line.Item.Intent == MediaIntentCodes.CopyUpdate
                    ? $"{line.Item.Target}  {line.Item.Channel}/{line.Item.WorkId ?? "-"}  {line.Item.Object ?? ""}".Trim()
                    : line.Item.Intent == MediaIntentCodes.MediaReorder
                        ? $"{line.Item.Channel}/{line.Item.WorkId}  {line.Item.ObjectList?.Count ?? 0} 条".Trim()
                    : line.Item.Intent is MediaIntentCodes.WorkRegister or MediaIntentCodes.WorkUpdate
                        ? $"{line.Item.Channel}/{line.Item.WorkId}  {line.Item.Title}  {line.Item.StartedOn}  {line.Item.Place}".Trim()
                        : line.Item.Object ?? line.Item.StageFolder ?? line.Item.StageRel ?? line.Item.WorkId;
            builder.AppendLine($"- [{mark}] {line.Item.Intent}  {target}");
            if (!string.IsNullOrWhiteSpace(line.Decision.Message))
            {
                builder.AppendLine($"    {line.Decision.Message}");
            }
        }

        if (string.Equals(report.Document.Mode, "direct", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.AppendLine("机械本机顺序：回收 → ingest → 内容补丁。不调用 deploy，不 withdraw --apply，禁止 prune-assets。公网走发布上线。");
        }

        if (!string.IsNullOrWhiteSpace(report.PatchPreview))
        {
            builder.AppendLine();
            builder.Append(report.PatchPreview.TrimEnd());
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// 生成可交给对话执行的 Markdown。
    /// </summary>
    public static string RenderPrompt(PreviewReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("请按下列意图处理网站资源。遵守站点媒体生命周期：");
        builder.AppendLine("- 上页走 ingest，中转站 / 投放箱文件保留。");
        builder.AppendLine("- 仅取消显示只去掉内容层引用并重排显示标签，不重命名对象键。");
        builder.AppendLine("- 已隐藏恢复显示只写回原对象键，不新建键，不重新入库。");
        builder.AppendLine("- 真正撤下：本机先清零引用；发布上线后再 withdraw --apply 与 purge。禁止 deploy --prune-assets。");
        builder.AppendLine("- 整项撤下 work.withdraw：删除该作品内容记录；发布时撤下其全部对象键，视频连同伴生封面。中转站不动。正式位目录已空则删空目录。");
        builder.AppendLine("- 已上页投放箱文件不要删除；未上页废图才可移入回收站。");
        builder.AppendLine("- stock 不可撤下、不可回收。");
        builder.AppendLine("- 登记空壳只追加内容层作品，不调用 ingest，不产生对象键。");
        builder.AppendLine("- 路径重挂只改台账 stageRel，不改对象键，不改内容层 src / 题名。");
        builder.AppendLine("- 文案 copy.update 只改 lead / summary / 可选 displayName 与 description，不改 id 与自动 label。");
        builder.AppendLine("- 排序 media.reorder 只改该作品 media 数组顺序并重排标签，不改对象键。");
        builder.AppendLine("- 星级 stars.update 只改对应 media.stars，1～5 写入，0 则去掉字段，不改 src、对象键与数组序。");
        builder.AppendLine();
        builder.AppendLine("## 预演");
        builder.AppendLine();
        builder.AppendLine("```");
        builder.Append(RenderPreview(report));
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## intent.json");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(IntentDocumentBuilder.ToJson(report.Document));
        builder.AppendLine("```");
        return builder.ToString();
    }
}
