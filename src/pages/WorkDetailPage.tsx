import { useLocation } from "react-router-dom";
import { ChannelHead } from "../components/work/ChannelHead";
import { ConstructionSheetDesk } from "../components/work/ConstructionSheetDesk";
import { LandscapeProjectBoard } from "../components/work/LandscapeProjectBoard";
import { ProjectGallery } from "../components/work/ProjectGallery";
import { resolveLead } from "../content/copyDisplay";
import { toProjectAlbumShots } from "../content/projectAlbum";
import { lexicon } from "../content/lexicon";
import { findCategoryByPath, findCollectionByChannel, findCollectionByPath } from "../content/site";
import { studioAliasForDisplay } from "../content/studios";
import {
  findPublishedWork,
  findPublishedWorksById,
  formatStartedOn,
  isPhotoPoolChannel,
  listWorkShots,
  photoDeliveryChannels,
  themesOfPhotoWork,
  type WorkMedia,
  type WorkRecord,
} from "../content/works";
import { channelTitleZh } from "../ia/href";
import { iaOfSkin } from "../ia";
import { kindOfChannel } from "../ia/query";
import { hrefForDevelopChannel, hrefForPhotoCatalog, parseWorkIndexPath, workIndexRoot } from "../ia/workTree";
import { readCatalogFromState } from "../ia/photoNav";
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
  const studio = studioAliasForDisplay(work);
  if (studio) {
    items.push({ label: lexicon.workStudio.zh, value: studio });
  }
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
 * 详情页未走画册时的视频框：只作占位，交互由画册与灯箱承接。
 */
/**
 * 解码路径段；已解码或含非法百分号时原样返回。
 */
function decodePathSegment(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

/**
 * 现实摄影、游戏摄影、AI摄影详情：`/photo/{channel}/{项目夹名}`。
 */
function findPhotoDeliveryWork(pathname: string): WorkRecord | undefined {
  const parts = pathname.split("/").filter(Boolean);
  if (parts.length < 3 || parts[0] !== lexicon.photography.key) {
    return undefined;
  }
  const channel = parts[1] ?? "";
  if (!(photoDeliveryChannels as readonly string[]).includes(channel)) {
    return undefined;
  }
  return findPublishedWork(channel, decodePathSegment(parts.slice(2).join("/")));
}

function renderVideoFrame(item: WorkMedia) {
  const href = item.src ? assetUrl(item.src) : "";
  const poster = item.poster ? assetUrl(item.poster) : undefined;
  return (
    <figure className="media-frame is-video" key={item.label}>
      {href ? (
        <video src={href} poster={poster} muted playsInline />
      ) : (
        <span>视频框 · {item.label}</span>
      )}
    </figure>
  );
}

/**
 * 显影详情回到细目列表；摄影详情回到带来源查询的总览或拍摄列表。
 */
function resolveDetailBack(
  parsed: ReturnType<typeof parseWorkIndexPath>,
  work: WorkRecord | undefined,
  category: ReturnType<typeof findCategoryByPath>,
  iaId: string,
  fromState?: string,
): { path: string; label: string } {
  if (work && isPhotoPoolChannel(work.channel)) {
    const catalogPath = `/${lexicon.photography.key}/${lexicon.photoCatalog.key}`;
    const shootsPath = `/${lexicon.photography.key}/${lexicon.photoShoots.key}`;
    const from = readCatalogFromState(fromState, [catalogPath, shootsPath]);
    const fromShoots = Boolean(from && (from.split("?")[0] ?? "") === shootsPath);
    const types = themesOfPhotoWork(work);
    return {
      path: from ?? (types.length === 1 ? hrefForPhotoCatalog(types[0]) : catalogPath),
      label: fromShoots ? lexicon.photoShoots.zh : lexicon.photoCatalog.zh,
    };
  }
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
      ? findPublishedWork(parsed.channel, decodePathSegment(parsed.id))
      : parsed?.layer === "legacy-id"
        ? findPublishedWorksById(parsed.id)[0]
        : undefined;
  const collection =
    collectionFromPath ??
    (workFromTree ? findCollectionByChannel(workFromTree.channel) : undefined) ??
    (parsed?.layer === "detail" ? findCollectionByChannel(parsed.channel) : undefined);
  const deliveryWork = findPhotoDeliveryWork(location.pathname);
  const work = deliveryWork
    ? deliveryWork
    : parsed
      ? workFromTree
      : collection
        ? findPublishedWork(collection.id, decodePathSegment(id))
        : undefined;
  const fromState =
    location.state && typeof location.state === "object" && "from" in location.state
      ? typeof (location.state as { from?: unknown }).from === "string"
        ? (location.state as { from: string }).from
        : undefined
      : undefined;
  const back = resolveDetailBack(parsed, work, category, ia.id, fromState);
  const theme =
    collection?.theme ??
    (work && isPhotoPoolChannel(work.channel) ? lexicon.photography.key : undefined) ??
    category?.theme ??
    (parsed ? lexicon.workIndex.key : "home");

  if (!work) {
    return (
      <div className="develop-channel" data-theme={theme}>
        <ChannelHead
          backTo={back.path}
          backLabel={back.label}
          title="档案不存在"
          lead="这条档案未开放，或不在当前分类中。"
        />
      </div>
    );
  }

  const metaItems = buildMetaItems(work);
  const photoChannel = isPhotoPoolChannel(work.channel);
  const galleryVariant =
    work.channel === lexicon.digitalTwin.key || work.channel === lexicon.lineSimulation.key
      ? "twin"
      : photoChannel
        ? "photo"
        : undefined;
  const galleryProject = {
    id: work.id,
    title: work.title,
    date: work.capturedOn ?? work.startedOn ?? work.year,
    place: work.place,
    summary: resolveLead(work.summary),
    images: toProjectAlbumShots(listWorkShots(work)),
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
    work.channel === lexicon.landscapeCDs.key ? (
      <ConstructionSheetDesk title={work.title} media={work.media} />
    ) : work.channel === lexicon.landscapeRendering.key ? (
      <LandscapeProjectBoard works={[work]} showHead={false} />
    ) : galleryVariant ? (
      <ProjectGallery
        projects={[galleryProject]}
        variant={galleryVariant}
        showHead={false}
      />
    ) : (
      <div className="media-grid">
        {work.media.map((item) => {
          if (item.kind === "video") {
            return renderVideoFrame(item);
          }
          const href = item.src ? assetUrl(item.src) : "";
          return (
            <figure className="media-frame" key={item.label}>
              {href ? <img src={href} alt="" /> : null}
              <span>{item.label}</span>
            </figure>
          );
        })}
      </div>
    );

  return (
    <div className="develop-channel" data-theme={theme}>
      <ChannelHead backTo={back.path} backLabel={back.label} title={work.title} lead={resolveLead(work.summary)} />
      <div className="develop-project-gallery">
        {tags}
        {meta}
        {body}
        {gallery}
      </div>
    </div>
  );
}
