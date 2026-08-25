import { profile } from "../../content/site";

export function FooterContact() {
  return (
    <footer className="site-footer">
      <div>
        <h3>
          {profile.siteLabel} / {profile.siteLabelEn}
        </h3>
        <p className="work-lead">继续往下的内容，在顶栏各档案里。本页只负责把路打开。</p>
      </div>
      <div className="contacts">
        {profile.contacts.map((item) => (
          <a key={item.label} className="contact-row" href={item.href}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
            <em>{item.note}</em>
          </a>
        ))}
      </div>
    </footer>
  );
}
