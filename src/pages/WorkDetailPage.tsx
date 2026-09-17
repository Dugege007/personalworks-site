import { Link, useLocation } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { LandscapeProjectBoard } from "../components/work/LandscapeProjectBoard";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { lexicon } from "../content/lexicon";
import { findCategoryByPath, findCollectionByChannel, findCollectionByPath } from "../content/site";
import {
  findPublishedWork,
  findPublishedWorksById,
  formatStartedOn,
  listWorkImages,
  type WorkRecord,
} from "../content/works";
import { channelTitleZh } from "../ia/href";
import { iaOfSkin } from "../ia";
import { kindOfChannel } from "../ia/query";
import { hrefForDevelopChannel, parseWorkIndexPath, workIndexRoot } from "../ia/workTree";
import { assetUrl } from "../lib/assets";
import { usePrefs } from "../prefs/PrefsProvider";
import "../styles/develop-work.css";
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
  if (work.capturedOn) {
    items.push({ label: "拍摄", value: formatStartedOn(work.capturedOn) });
  }
  if (work.startedOn) {
    items.push({ label: "开始", value: formatStartedOn(work.startedOn) });
  }
  if (work.siteType) {
    items.push({ label: "类型", value: work.siteType });
  }
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
  if (work.place) {
    items.push({ label: "地点", value: work.place });
  } else if (work.placeAlias) {
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
 * 显影详情回到细目列表；层境详情回到分类室。
 */
function resolveDetailBack(
  parsed: ReturnType<typeof parseWorkIndexPath>,
  work: WorkRecord | undefined,
  category: ReturnType<typeof findCategoryByPath>,
  iaId: string,
): { path: string; label: string } {
  if (parsed?.layer === "detail") {
    return {
      path: hrefForDevelopChannel(parsed.kind, parsed.channel),
      label: channelTitleZh(parsed.channel),
    };
  }
  if (parsed) {
    const kind = work ? kindOfChannel(work.channel) : undefined;
    if (kind && work) {
      return {
        path: hrefForDevelopChannel(kind, work.channel),
        label: channelTitleZh(work.channel),
      };
    }
    return { path: workIndexRoot(), label: lexicon.workIndex.zh };
  }
  if (iaId === "develop-editorial" && work) {
    const kind = kindOfChannel(work.channel);
    if (kind) {
      return {
        path: hrefForDevelopChannel(kind, work.channel),
        label: channelTitleZh(work.channel),
      };
    }
  }
  return { path: category?.path ?? "/", label: category?.title ?? "分类" };
}

/**
 * 共用作品详情骨架：返回、标题、元信息、正文、图组；栏目差异仅字段显隐。
 */
export function WorkDetailPage() {
  const location = useLocation();
  const { currentSkin } = usePrefs();
  const ia = iaOfSkin(currentSkin);
  const parsed = parseWorkIndexPath(location.pathname);
  const segments = location.pathname.split("/").filter(Boolean);
  const id = segments[segments.length - 1] ?? "";
  const category = findCategoryByPath(location.pathname);
  const collectionFromPath = findCollectionByPath(location.pathname);
  const workFromTree =
    parsed?.layer === "detail"
      ? findPublishedWork(parsed.channel, parsed.id)
      : parsed?.layer === "legacy-id"
        ? findPublishedWorksById(parsed.id)[0]
        : undefined;
  const collection =
    collectionFromPath ??
    (workFromTree ? findCollectionByChannel(workFromTree.channel) : undefined) ??
    (parsed?.layer === "detail" ? findCollectionByChannel(parsed.channel) : undefined);
  const work = parsed
    ? workFromTree
    : collection
      ? findPublishedWork(collection.id, id)
      : undefined;
  const back = resolveDetailBack(parsed, work, category, ia.id);
  const theme = collection?.theme ?? category?.theme ?? (parsed ? lexicon.workIndex.key : "home");

  if (!work) {
    return (
      <div className="page" data-theme={theme}>
        <Link className="back" to={back.path}>
          ← 返回{back.label}
        </Link>
        <h1>档案不存在</h1>
        <p className="page-lead">这条档案未开放，或不在当前分类中。</p>
      </div>
    );
  }

  const metaItems = buildMetaItems(work);
  const photoChannel =
    work.channel === lexicon.landscapePhoto.key ||
    work.channel === lexicon.humanistPhoto.key ||
    work.channel === lexicon.portraitPhoto.key;
  const galleryVariant =
    work.channel === lexicon.digitalTwin.key ? "twin" : photoChannel ? "photo" : undefined;
  const galleryProject = {
    id: work.id,
    title: work.title,
    date: work.capturedOn ?? work.startedOn ?? work.year,
    place: work.place,
    summary: work.summary,
    images: listWorkImages(work).flatMap((image) =>
      image.src ? [{ src: image.src, label: image.label }] : [],
    ),
  };

  const tags =
    work.tags && work.tags.length > 0 ? (
      <div className="archive-tags">
        {work.tags.map((tag) => (
          <span className="archive-tag" key={tag}>
            {tag}
          </span>
        ))}
      </div>
    ) : null;
  const meta = (
    <dl className="archive-meta">
      {metaItems.map((item) => (
        <div key={item.label}>
          <dt>{item.label}</dt>
          <dd>{item.value}</dd>
        </div>
      ))}
    </dl>
  );
  const body = work.body && work.body !== work.summary ? <p className="archive-body">{work.body}</p> : null;
  const gallery =
    work.channel === lexicon.landscapeRendering.key ? (
      <LandscapeProjectBoard works={[work]} />
    ) : galleryVariant ? (
      <ProjectGallery
        projects={[galleryProject]}
        variant={galleryVariant}
        showHead={galleryVariant !== "photo"}
      />
    ) : (
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
    );

  if (photoChannel) {
    return (
      <div className="develop-channel" data-theme={theme}>
        <ChannelHead backTo={back.path} backLabel={back.label} title={work.title} lead={work.summary} />
        <div className="develop-project-gallery">
          {tags}
          {meta}
          {body}
          {gallery}
        </div>
      </div>
    );
  }

  return (
    <div className="page" data-theme={theme}>
      <Link className="back" to={back.path}>
        ← 返回{back.label}
      </Link>
      {parsed ? null : (
        <div className="page-kicker">
          {work.year} / {collection?.titleDeco ?? "ARCHIVE"}
        </div>
      )}
      <h1>{work.title}</h1>
      <p className="page-lead">{work.summary}</p>
      {tags}
      {meta}
      {body}
      {gallery}
      {work.channel === lexicon.landscapeCDs.key ? (
        <p className="archive-disclaimer">仅供作品展示，不作为施工依据。</p>
      ) : null}
    </div>
  );
}
