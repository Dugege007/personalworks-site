import { Link, useLocation } from "react-router-dom";
import { findCategoryByPath, findCollectionByPath } from "../content/site";
import { findPublishedWork, type WorkRecord } from "../content/works";
import { assetUrl } from "../lib/assets";
import "../styles/placeholder.css";

type MetaItem = {
  label: string;
  value: string;
};

/**
 * 从作品可选字段生成元信息行；缺字段不占位。
 */
function buildMetaItems(work: WorkRecord): MetaItem[] {
  const items: MetaItem[] = [{ label: "年份", value: work.year }];
  if (work.role) {
    items.push({ label: "职责", value: work.role });
  }
  if (work.lineType) {
    items.push({ label: "产线类型", value: work.lineType });
  }
  if (work.metrics) {
    items.push({ label: "指标", value: work.metrics });
  }
  if (work.clientAlias) {
    items.push({ label: "项目别名", value: work.clientAlias });
  }
  if (work.sheetType) {
    items.push({ label: "图种", value: work.sheetType });
  }
  if (work.placeAlias) {
    items.push({ label: "地点", value: work.placeAlias });
  }
  if (work.exifLite) {
    items.push({ label: "EXIF", value: work.exifLite });
  }
  if (work.displayName) {
    items.push({ label: "署名", value: work.displayName });
  }
  return items;
}

/**
 * 共用作品详情骨架：返回、标题、元信息、正文、图组；栏目差异仅字段显隐。
 */
export function WorkDetailPage() {
  const location = useLocation();
  const segments = location.pathname.split("/").filter(Boolean);
  const id = segments[segments.length - 1] ?? "";
  const category = findCategoryByPath(location.pathname);
  const collection = findCollectionByPath(location.pathname);
  const backPath = category?.path ?? "/";
  const backLabel = category?.title ?? "分类";
  const work = collection ? findPublishedWork(collection.id, id) : undefined;
  const theme = collection?.theme ?? category?.theme ?? "home";

  if (!work) {
    return (
      <div className="page" data-theme={theme}>
        <Link className="back" to={backPath}>
          ← 返回{backLabel}
        </Link>
        <h1>档案不存在</h1>
        <p className="page-lead">这条档案未开放，或不在当前分类中。</p>
      </div>
    );
  }

  const metaItems = buildMetaItems(work);

  return (
    <div className="page" data-theme={theme}>
      <Link className="back" to={backPath}>
        ← 返回{backLabel}
      </Link>
      <div className="page-kicker">
        {work.year} / {collection?.title ?? "ARCHIVE"}
      </div>
      <h1>{work.title}</h1>
      <p className="page-lead">{work.summary}</p>
      {work.tags && work.tags.length > 0 ? (
        <div className="archive-tags">
          {work.tags.map((tag) => (
            <span className="archive-tag" key={tag}>
              {tag}
            </span>
          ))}
        </div>
      ) : null}
      <dl className="archive-meta">
        {metaItems.map((item) => (
          <div key={item.label}>
            <dt>{item.label}</dt>
            <dd>{item.value}</dd>
          </div>
        ))}
      </dl>
      <p className="archive-body">{work.body}</p>
      <div className="media-grid">
        {work.media.map((item) => {
          const href = item.src ? assetUrl(item.src) : "";
          if (item.kind === "video") {
            return (
              <figure className="media-frame is-video" key={item.label}>
                {href ? (
                  <video src={href} muted controls playsInline />
                ) : (
                  <span>视频框 · {item.label}</span>
                )}
              </figure>
            );
          }
          return (
            <figure className="media-frame" key={item.label}>
              {href ? <img src={href} alt="" /> : null}
              <span>{item.label}</span>
            </figure>
          );
        })}
      </div>
      {work.channel === "construction" ? (
        <p className="archive-disclaimer">仅供作品展示，不作为施工依据。</p>
      ) : null}
    </div>
  );
}
