import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./App";
import { syncProfileDocumentTitle } from "./hooks/useProfileName";
import { releaseSiteBootWhenFirstScreenReady } from "./lib/siteBoot";
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

syncProfileDocumentTitle();
releaseSiteBootWhenFirstScreenReady();
