// Rasterize the favicon set from site/assets/hm-mark.svg.
//
//     node site/tools/gen-favicons.mjs
//
// Writes site/static/: favicon.svg, favicon.ico, favicon-16x16.png,
// favicon-32x32.png, apple-touch-icon.png, android-chrome-192x192.png,
// android-chrome-512x512.png. All are committed; like gen-marks.py this runs
// by hand, never at build time.
//
// The filenames are Hextra's own. Hugo's static/ takes precedence over a
// module's, so dropping these here shadows the theme's stock icons with no
// template override needed.
//
// Chromium does the rasterizing, via Playwright. That is not incidental: it
// is the same engine that renders the site, so what lands in the PNG is what
// a reader would have seen. Rasterizing SVGs of this repo's marks through a
// standalone converter has burned us before -- a converter's idea of a glyph
// or a metric is not the browser's -- so the browser is the reference.
//
// Requires Playwright with its chromium download. If it is missing, install
// it and re-run; the committed output is what the site actually ships, so a
// missing toolchain is never on the build path.

import { chromium } from "playwright";
import { readFileSync, writeFileSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const here = dirname(fileURLToPath(import.meta.url));
const assets = resolve(here, "..", "assets");
const staticDir = resolve(here, "..", "static");

// The accent from site/assets/css/hm-theme.css. A favicon renders standalone,
// with no page to inherit from, so currentColor there would resolve to black.
const ACCENT = "#58a6ff";
// The background for the installed-app icons. Apple and Android composite
// these onto a tile of their own choosing, so a transparent mark would sit on
// whatever the OS picked; the browser-tab favicons stay transparent.
const TILE_BG = "#0d1117";

const mark = readFileSync(resolve(assets, "hm-mark.svg"), "utf8");
const solid = mark.replace('fill="currentColor"', `fill="${ACCENT}"`);

if (solid === mark) {
  throw new Error("hm-mark.svg no longer carries fill=\"currentColor\" - check gen-marks.py");
}

writeFileSync(resolve(staticDir, "favicon.svg"), solid);

// size, filename, background (null = transparent), padding as a fraction
const TARGETS = [
  [16, "favicon-16x16.png", null, 0],
  [32, "favicon-32x32.png", null, 0],
  [180, "apple-touch-icon.png", TILE_BG, 0.12],
  [192, "android-chrome-192x192.png", TILE_BG, 0.12],
  [512, "android-chrome-512x512.png", TILE_BG, 0.12],
];

const browser = await chromium.launch();
const page = await browser.newPage({ deviceScaleFactor: 1 });
const png = {};

for (const [size, name, bg, pad] of TARGETS) {
  const inset = Math.round(size * pad);
  await page.setViewportSize({ width: size, height: size });
  await page.setContent(`<!doctype html><meta charset="utf-8">
    <style>
      html,body { margin:0; padding:0; width:${size}px; height:${size}px; }
      body { ${bg ? `background:${bg};` : ""} display:flex; align-items:center; justify-content:center; }
      svg { width:${size - 2 * inset}px; height:${size - 2 * inset}px; display:block; }
    </style>${solid}`);
  const buf = await page.screenshot({ omitBackground: !bg });
  writeFileSync(resolve(staticDir, name), buf);
  png[size] = buf;
  console.log(`wrote static/${name}`);
}

await browser.close();

// favicon.ico, holding the 16 and 32 PNGs.
//
// An .ico may carry PNG payloads rather than raw DIBs -- every browser that
// still asks for favicon.ico reads them -- so this is the two PNGs above in
// an ICONDIR wrapper rather than a second rasterization path.
function ico(images) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // 1 = icon
  header.writeUInt16LE(images.length, 4);

  const dir = Buffer.alloc(16 * images.length);
  let offset = header.length + dir.length;

  images.forEach(({ size, data }, i) => {
    const e = i * 16;
    dir.writeUInt8(size >= 256 ? 0 : size, e + 0); // width, 0 means 256
    dir.writeUInt8(size >= 256 ? 0 : size, e + 1); // height
    dir.writeUInt8(0, e + 2); // palette size
    dir.writeUInt8(0, e + 3); // reserved
    dir.writeUInt16LE(1, e + 4); // colour planes
    dir.writeUInt16LE(32, e + 6); // bits per pixel
    dir.writeUInt32LE(data.length, e + 8);
    dir.writeUInt32LE(offset, e + 12);
    offset += data.length;
  });

  return Buffer.concat([header, dir, ...images.map((i) => i.data)]);
}

writeFileSync(
  resolve(staticDir, "favicon.ico"),
  ico([
    { size: 16, data: png[16] },
    { size: 32, data: png[32] },
  ]),
);
console.log("wrote static/favicon.ico");
console.log("wrote static/favicon.svg");
