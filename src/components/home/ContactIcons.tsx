import { useLayoutEffect, useRef, useState, type ReactElement, type ReactNode } from "react";
import { lexicon } from "../../content/lexicon";
import { assetUrl } from "../../lib/assets";
import type { ContactChannel, ContactChannelId } from "../../content/site";

type IconProps = {
  className?: string;
};

function BaseIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.35"
        strokeLinejoin="round"
        d="M10 17.2s-5.4-4.08-5.4-8.05A5.4 5.4 0 0 1 10 3.75a5.4 5.4 0 0 1 5.4 5.4c0 3.97-5.4 8.05-5.4 8.05z"
      />
      <circle cx="10" cy="8.9" r="1.7" fill="none" stroke="currentColor" strokeWidth="1.35" />
    </svg>
  );
}

function MailIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <rect x="3.2" y="4.8" width="13.6" height="10.4" rx="1.1" fill="none" stroke="currentColor" strokeWidth="1.35" />
      <path fill="none" stroke="currentColor" strokeWidth="1.35" strokeLinejoin="round" d="M3.6 5.4 10 10.4 16.4 5.4" />
    </svg>
  );
}

function PhoneIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.35"
        strokeLinejoin="round"
        d="M6.2 3.8h2.3l.9 2.4-1.4 1.1a9.2 9.2 0 0 0 4.7 4.7l1.1-1.4 2.4.9v2.3c0 .7-.6 1.4-1.4 1.5-5.2.7-10.2-4.3-9.5-9.5.1-.8.8-1.4 1.5-1.4z"
      />
    </svg>
  );
}

function WechatIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.35"
        d="M8.1 4.4c-3.1 0-5.5 2.1-5.5 4.7 0 1.5.8 2.8 2.1 3.7l-.4 1.7 2-.9c.5.1 1 .2 1.6.2.1-.3.1-.5.1-.8.2-2.3 2.6-4.1 5.4-4.1.2 0 .3 0 .5.1C13.5 6.3 11 4.4 8.1 4.4z"
      />
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.35"
        d="M14.9 9.1c-2.7 0-4.9 1.8-4.9 4.1s2.2 4.1 4.9 4.1c.5 0 .9-.1 1.3-.2l1.7.7-.4-1.4c1-.8 1.7-1.9 1.7-3.2 0-2.3-2.2-4.1-4.3-4.1z"
      />
      <circle cx="6.7" cy="8.5" r="0.7" fill="currentColor" />
      <circle cx="9.6" cy="8.5" r="0.7" fill="currentColor" />
      <circle cx="13.6" cy="13.1" r="0.6" fill="currentColor" />
      <circle cx="16.1" cy="13.1" r="0.6" fill="currentColor" />
    </svg>
  );
}

function QqIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <ellipse cx="10" cy="8.2" rx="3.4" ry="3.6" fill="none" stroke="currentColor" strokeWidth="1.35" />
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="1.35"
        strokeLinejoin="round"
        d="M5.6 11.4c.4 2.4 2.2 4.1 4.4 4.1s4-1.7 4.4-4.1c-1 .8-2.6 1.3-4.4 1.3s-3.4-.5-4.4-1.3z"
      />
      <path fill="none" stroke="currentColor" strokeWidth="1.35" d="M7.2 5.1 6 3.4M12.8 5.1 14 3.4" />
    </svg>
  );
}

function GithubIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <path
        fill="currentColor"
        d="M10 2.4a7.6 7.6 0 0 0-2.4 14.8c.38.07.52-.16.52-.36 0-.18-.01-.76-.01-1.38-2.1.45-2.54-.9-2.54-.9-.34-.88-.84-1.11-.84-1.11-.69-.47.05-.46.05-.46.76.05 1.16.78 1.16.78.68 1.16 1.77.83 2.2.63.07-.49.26-.83.48-1.02-1.68-.18-3.44-.83-3.44-3.75 0-.83.3-1.51.78-2.04-.08-.19-.34-.97.07-2.02 0 0 .64-.2 2.08.78a7.2 7.2 0 0 1 3.78 0c1.44-.98 2.08-.78 2.08-.78.41 1.05.15 1.83.08 2.02.48.53.78 1.21.78 2.04 0 2.93-1.76 3.57-3.45 3.75.27.24.51.7.51 1.41 0 1.02-.01 1.84-.01 2.09 0 .2.14.44.52.36A7.6 7.6 0 0 0 10 2.4z"
      />
    </svg>
  );
}

const iconDict: Record<ContactChannelId, (props: IconProps) => ReactElement> = {
  profileBase: BaseIcon,
  profileMail: MailIcon,
  profilePhone: PhoneIcon,
  profileWechat: WechatIcon,
  profileQq: QqIcon,
  profileGithub: GithubIcon,
};

type ContactIconsProps = {
  channels: ContactChannel[];
};

type IconItemProps = {
  item: ContactChannel;
  copied: boolean;
  open: boolean;
  onOpen: () => void;
  onClose: () => void;
  onCopied: (id: string) => void;
};

/**
 * 单个联系图标：面板锚在图标正下/正上，不跟随指针。
 */
function ContactIconItem({ item, copied, open, onOpen, onClose, onCopied }: IconItemProps) {
  const wrapRef = useRef<HTMLLIElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [place, setPlace] = useState<"below" | "above">("below");
  const [qrBroken, setQrBroken] = useState(false);

  const entry = lexicon[item.id];
  const Icon = iconDict[item.id];
  const value = item.value?.trim() ?? "";
  const href = item.href;
  const label = value ? `${entry.zh}：${value}` : entry.zh;
  const icon = <Icon className="contact-icon-svg" />;
  const qrSrc = item.qrSrc && !qrBroken ? assetUrl(item.qrSrc) : "";

  useLayoutEffect(() => {
    if (!open || !wrapRef.current || !panelRef.current) {
      return;
    }
    const iconBox = wrapRef.current.getBoundingClientRect();
    const panelH = panelRef.current.offsetHeight;
    const gap = 8;
    const spaceBelow = window.innerHeight - iconBox.bottom - gap;
    setPlace(spaceBelow < panelH + 12 ? "above" : "below");
  }, [open, qrSrc, copied]);

  const copyValue = async () => {
    if (!value) {
      return;
    }
    try {
      await navigator.clipboard.writeText(value);
      onCopied(item.id);
    } catch {
      onCopied("");
    }
  };

  const panel: ReactNode =
    open && value ? (
      <div ref={panelRef} className={`contact-panel is-${place}`} role="tooltip">
        <div className="contact-panel-card">
          <div className="contact-panel-kicker">{entry.deco}</div>
          <div className="contact-panel-value">{copied ? "已复制" : value}</div>
          {qrSrc ? (
            <img
              className="contact-qr"
              src={qrSrc}
              alt={`${entry.zh}二维码`}
              onError={() => setQrBroken(true)}
            />
          ) : null}
        </div>
      </div>
    ) : null;

  const triggerClass = `contact-icon${value ? "" : " is-empty"}`;

  let trigger: ReactNode;
  if (href && value) {
    trigger = (
      <a
        className={triggerClass}
        href={href}
        aria-label={label}
        target={item.id === "profileGithub" ? "_blank" : undefined}
        rel={item.id === "profileGithub" ? "noreferrer noopener" : undefined}
      >
        {icon}
      </a>
    );
  } else if (item.copy && value) {
    trigger = (
      <button type="button" className={triggerClass} aria-label={label} onClick={() => void copyValue()}>
        {icon}
      </button>
    );
  } else {
    trigger = (
      <span className={triggerClass} aria-label={label}>
        {icon}
      </span>
    );
  }

  return (
    <li
      ref={wrapRef}
      className="contact-icon-item"
      onMouseEnter={onOpen}
      onMouseLeave={onClose}
      onFocusCapture={onOpen}
      onBlurCapture={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          onClose();
        }
      }}
    >
      {trigger}
      {panel}
    </li>
  );
}

/**
 * 首屏与页脚共用的联系通道图标：现居地、邮箱、手机、微信、QQ、GitHub。
 */
export function ContactIcons({ channels }: ContactIconsProps) {
  const [copiedId, setCopiedId] = useState("");
  const [openId, setOpenId] = useState("");

  const markCopied = (id: string) => {
    setCopiedId(id);
    if (id) {
      window.setTimeout(() => setCopiedId(""), 1600);
    }
  };

  return (
    <ul className="contact-icons">
      {channels.map((item) => (
        <ContactIconItem
          key={item.id}
          item={item}
          copied={copiedId === item.id}
          open={openId === item.id}
          onOpen={() => setOpenId(item.id)}
          onClose={() => setOpenId((cur) => (cur === item.id ? "" : cur))}
          onCopied={markCopied}
        />
      ))}
    </ul>
  );
}
