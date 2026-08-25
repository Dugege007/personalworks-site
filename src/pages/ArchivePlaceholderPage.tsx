import { Link, useLocation } from "react-router-dom";
import { findNavByPath, workSections } from "../content/site";
import "../styles/placeholder.css";

export function ArchivePlaceholderPage() {
  const location = useLocation();
  const nav = findNavByPath(location.pathname);
  const section = workSections.find((item) => item.path === nav.path);

  return (
    <div className="page" data-theme={nav.theme}>
      <div className="page-kicker">
        {section?.index ?? "00"} / {section?.indexEn ?? nav.labelEn}
      </div>
      <h1>{nav.label}</h1>
      <p className="page-lead">{section?.lead ?? "栏目占位页。作品图入库后替换。"}</p>
      <div className="card-grid">
        {(section?.frames ?? ["占位 A", "占位 B", "占位 C"]).map((label, index) => (
          <Link className="card" key={label} to={`${nav.path}/sample-${index + 1}`}>
            <small>0{index + 1} / SAMPLE</small>
            <div>
              <h2>{label}</h2>
              <p>待替换作品。点击进入详情占位。</p>
            </div>
          </Link>
        ))}
      </div>
      <p className="note">第一批仅验证路由与栏目气质，不加载真实媒体。</p>
    </div>
  );
}
