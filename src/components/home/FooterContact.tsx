import { profile } from "../../content/site";
import { ContactIcons } from "./ContactIcons";
import { HomeBeian } from "./HomeBeian";

export function FooterContact() {
  return (
    <footer className="site-footer">
      <div>
        <h3>
          {profile.siteLabel} / {profile.siteLabelEn}
        </h3>
        <p className="work-lead">待填写描述</p>
      </div>
      <ContactIcons channels={profile.contactChannels} />
      <HomeBeian />
    </footer>
  );
}
