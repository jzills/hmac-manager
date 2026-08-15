# Site generators

Both scripts write files that are **committed**. Neither runs during a build —
`hugo` never invokes them, and `.github/workflows/pages.yml` never installs
their toolchains. Run them by hand when the mark changes, and commit the
result.

| Script | Writes | Needs |
| --- | --- | --- |
| `gen-marks.py` | `site/assets/hm-wordmark.svg`, `site/assets/hm-mark.svg` | Python 3, no packages |
| `gen-favicons.mjs` | the seven files in `site/static/` | Node, `playwright` + its chromium |

```bash
python3 site/tools/gen-marks.py      # first: the SVGs
node site/tools/gen-favicons.mjs     # then: the raster set, from hm-mark.svg
```

`gen-favicons.mjs` rasterizes through headless Chromium rather than a
standalone SVG converter, so the PNGs are what the browser would have drawn.
It resolves `playwright` by walking up from `site/tools/`, so a `node_modules`
anywhere at or above the repository root will do.

The marks are geometry, not text. `assets/logo.svg` — the copy the README
uses — is the original `<text>` drawing, and `gen-marks.py` asserts nothing
about it; if that banner is ever edited, update `ART` in `gen-marks.py` to
match and regenerate.
