import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./App";
import { PrefsProvider } from "./prefs/PrefsProvider";
import "./styles/global.css";

const root = document.getElementById("root");
if (!root) {
  throw new Error("未找到根节点 #root");
}

createRoot(root).render(
  <StrictMode>
    <BrowserRouter>
      <PrefsProvider>
        <App />
      </PrefsProvider>
    </BrowserRouter>
  </StrictMode>,
);

const signalSiteReady = () => {
  window.__siteBoot?.ready();
};

const holdPreview =
  import.meta.env.DEV && new URLSearchParams(window.location.search).has("boot");

if (holdPreview) {
  window.setTimeout(signalSiteReady, 4500);
} else {
  requestAnimationFrame(() => {
    requestAnimationFrame(signalSiteReady);
  });
}
