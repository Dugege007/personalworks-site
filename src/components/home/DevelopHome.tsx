import { useState } from "react";
import { Link } from "react-router-dom";
import { lexicon } from "../../content/lexicon";
import { profile } from "../../content/site";
import { workCoverSrc } from "../../content/stockMedia";
import { hrefForWork, channelTitleZh } from "../../ia/href";
import { queryNotes, queryWorks } from "../../ia/query";
import type { HomeBlock, IaRecord } from "../../ia/types";
import { assetUrl } from "../../lib/assets";
import { usePrefs } from "../../prefs/PrefsProvider";
import { ContactIcons } from "./ContactIcons";
import "../../styles/develop-home.css";

type DevelopHomeProps = {
  ia: IaRecord;
};

/**
 * 显影首页：通幅头图、简介、切片、精选、心得、墨底收束。
 */
export function DevelopHome({ ia }: DevelopHomeProps) {
  return (
    <div className="develop-home">
      <div className="develop-progress" aria-hidden="true" />
      {ia.home.blocks.map((block, index) => (
        <DevelopBlock key={`${block.type}-${index}`} block={block} ia={ia} />
      ))}
    </div>
  );
}

type DevelopBlockProps = {
  block: HomeBlock;
  ia: IaRecord;
};

function DevelopBlock({ block, ia }: DevelopBlockProps) {
  if (block.type === "hero-bleed") {
    return <HeroBleed block={block} />;
  }
  if (block.type === "intro-portrait") {
    return <IntroPortrait />;
  }
  if (block.type === "waypoint-slices") {
    return <WaypointSlices />;
  }
  if (block.type === "selected-frames") {
    return <SelectedFrames block={block} ia={ia} />;
  }
  if (block.type === "notes-tease") {
    return <NotesTease block={block} />;
  }
  if (block.type === "contact-close") {
    return <ContactClose />;
  }
  return null;
}

function HeroBleed({ block }: { block: HomeBlock }) {
  const [mediaFailed, setMediaFailed] = useState(false);
  const src = block.heroSrc && !mediaFailed ? assetUrl(block.heroSrc) : "";
  const year = block.heroYear ?? profile.portraitYear;

  return (
    <section className="develop-hero" id="hero-bleed" data-tone={src ? "dark" : "light"}>
      {src ? (
        <img
          className="develop-hero-media"
          src={src}
          alt=""
          onError={() => setMediaFailed(true)}
        />
      ) : (
        <div className="develop-hero-well" aria-hidden="true" />
      )}
      <div className="develop-hero-veil" aria-hidden="true" />
      <div className="develop-hero-copy">
        {src ? (
          <p className="develop-hero-shot">
            {lexicon.shotIn.deco} · {year}
          </p>
        ) : null}
        <h1 className="develop-hero-name">{profile.name}</h1>
        <p className="develop-hero-en">{profile.nameEn}</p>
        <p className="develop-hero-id">{block.identity}</p>
        <div className="develop-hero-cta">
          <Link className="develop-btn is-solid" to={`/${lexicon.profileResume.key}`}>
            {lexicon.profileResume.zh}
          </Link>
          <Link className="develop-btn is-line" to={`/${lexicon.workIndex.key}`}>
            {lexicon.workIndex.zh}
          </Link>
        </div>
      </div>
    </section>
  );
}

function IntroPortrait() {
  const [failed, setFailed] = useState(false);
  const src = profile.portraitSrc && !failed ? assetUrl(profile.portraitSrc) : "";

  return (
    <section className="develop-intro" id="intro-portrait">
      <div className="develop-intro-figure">
        {src ? (
          <img
            src={src}
            alt={`${profile.name} / ${lexicon.profile.zh}`}
            onError={() => setFailed(true)}
          />
        ) : (
          <div className="develop-intro-well" aria-hidden="true" />
        )}
        <span className="develop-intro-mark">
          {lexicon.shotIn.deco} · {profile.portraitYear}
        </span>
      </div>
      <div className="develop-intro-copy">
        <p className="develop-intro-en">{profile.nameEn}</p>
        {profile.bio.map((line) => (
          <p key={line}>{line}</p>
        ))}
      </div>
    </section>
  );
}

function WaypointSlices() {
  const frames = queryWorks({ source: "works", featured: true, limit: 1 });
  const cover = frames[0] ? workCoverSrc(frames[0]) : undefined;
  const slices = [
    {
      id: lexicon.profileResume.key,
      label: lexicon.profileResume.zh,
      deco: lexicon.profileResume.deco,
      to: `/${lexicon.profileResume.key}`,
      src: profile.portraitSrc,
    },
    {
      id: lexicon.profileSkills.key,
      label: lexicon.profileSkills.zh,
      deco: lexicon.profileSkills.deco,
      to: `/${lexicon.profileSkills.key}`,
      src: "home-page/skills.webp",
    },
    {
      id: lexicon.workIndex.key,
      label: lexicon.workIndex.zh,
      deco: lexicon.workIndex.deco,
      to: `/${lexicon.workIndex.key}`,
      src: cover,
    },
    {
      id: lexicon.notes.key,
      label: lexicon.notes.zh,
      deco: lexicon.notes.deco,
      to: `/${lexicon.notes.key}`,
      src: "notes/cover.webp",
    },
  ];

  return (
    <section className="develop-slices" id="waypoint-slices">
      {slices.map((item) => (
        <SliceCard key={item.id} {...item} />
      ))}
    </section>
  );
}

type SliceCardProps = {
  label: string;
  deco: string;
  to: string;
  src?: string;
};

function SliceCard({ label, deco, to, src }: SliceCardProps) {
  const [failed, setFailed] = useState(false);
  const href = src && !failed ? assetUrl(src) : "";

  return (
    <Link className="develop-slice" to={to}>
      {href ? <img src={href} alt="" onError={() => setFailed(true)} /> : <span className="develop-slice-well" />}
      <span className="develop-slice-copy">
        <strong>{label}</strong>
        <em>{deco}</em>
      </span>
    </Link>
  );
}

function SelectedFrames({ block, ia }: { block: HomeBlock; ia: IaRecord }) {
  const works = block.query ? queryWorks(block.query) : [];

  return (
    <section className="develop-frames" id="selected-frames">
      {works.map((work, index) => {
        const src = workCoverSrc(work, index);
        return (
          <FrameCard
            key={`${work.channel}-${work.id}`}
            title={work.title}
            caption={channelTitleZh(work.channel)}
            href={hrefForWork(work, ia.id)}
            src={src}
            featured={index === 0}
          />
        );
      })}
    </section>
  );
}

type FrameCardProps = {
  title: string;
  caption: string;
  href: string;
  src?: string;
  featured: boolean;
};

function FrameCard({ title, caption, href, src, featured }: FrameCardProps) {
  const [failed, setFailed] = useState(false);
  const media = src && !failed ? assetUrl(src) : "";

  return (
    <Link className={`develop-frame${featured ? " is-featured" : ""}`} to={href}>
      {media ? <img src={media} alt="" onError={() => setFailed(true)} /> : null}
      <span className="develop-frame-copy">
        <strong>{title}</strong>
        <em>{caption}</em>
      </span>
    </Link>
  );
}

function NotesTease({ block }: { block: HomeBlock }) {
  const notes = block.query ? queryNotes(block.query) : [];
  const note = notes[0];
  if (!note) {
    return null;
  }

  return (
    <section className="develop-notes" id="notes-tease">
      <Link className="develop-notes-link" to={`/${lexicon.notes.key}/${note.slug}`}>
        <time dateTime={note.date.replaceAll(".", "-")}>{note.date}</time>
        <h2>{note.title}</h2>
        <p>{note.summary}</p>
      </Link>
    </section>
  );
}

function ContactClose() {
  const { currentSkin } = usePrefs();

  return (
    <section className="develop-close" id="contact-close">
      <div>
        <p className="develop-close-brand">
          <span>{currentSkin.brand.zh}</span>
          <span>{currentSkin.brand.deco}</span>
        </p>
        <p className="develop-close-lead">写信或到场都可以。作品在墙里，照片在册里。</p>
      </div>
      <ContactIcons channels={profile.contactChannels} />
    </section>
  );
}
