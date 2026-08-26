import { profile } from "../../content/site";
import { ContactIcons } from "./ContactIcons";

export function FooterContact() {
  return (
    <footer className="site-footer">
      <div>
        <h3>
          {profile.siteLabel} / {profile.siteLabelEn}
        </h3>
        <p className="work-lead">继续往下的内容，在顶栏各档案里。本页只负责把路打开。</p>
      </div>
      <ContactIcons channels={profile.contactChannels} />
    </footer>
  );
}
