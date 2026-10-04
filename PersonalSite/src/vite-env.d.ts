/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ASSET_BASE: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

interface SiteBootGate {
  mark: (progress: number) => void;
  ready: () => void;
}

interface Window {
  __siteBoot?: SiteBootGate;
}
