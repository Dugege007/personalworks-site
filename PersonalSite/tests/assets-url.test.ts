import assert from "node:assert/strict";
import test from "node:test";
import { resolveAssetUrl } from "../src/lib/assets.ts";

test("notes-assets stay on the site root instead of the media CDN", () => {
  const src = "/notes-assets/n-20261008-205600/kalman_1d.png";
  assert.equal(resolveAssetUrl(src, ""), src);
  assert.equal(resolveAssetUrl(src, "https://cdn.example.com/media"), src);
});

test("object keys still resolve through placeholders or the CDN", () => {
  assert.equal(resolveAssetUrl("photo/a.webp", ""), "/placeholders/photo/a.webp");
  assert.equal(
    resolveAssetUrl("/placeholders/photo/a.webp", ""),
    "/placeholders/photo/a.webp",
  );
  assert.equal(
    resolveAssetUrl("photo/a.webp", "https://cdn.example.com/media/"),
    "https://cdn.example.com/media/photo/a.webp",
  );
  assert.equal(resolveAssetUrl("https://example.com/a.png", ""), "https://example.com/a.png");
});

test("notes stay on the site root instead of the media CDN", () => {
  const src = "/notes/n-20261008-205600/images/kalman_1d.webp";
  assert.equal(resolveAssetUrl(src, ""), src);
  assert.equal(resolveAssetUrl(src, "https://cdn.example.com/media"), src);
});
