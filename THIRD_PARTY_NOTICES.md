# Third-party notices

The HmacManager library, the ext-authz service and the operator declare their
dependencies in the usual places — `src/HmacManager/HmacManager.csproj`,
`kubernetes/service/HmacManager.Kubernetes.csproj`,
`kubernetes/operator/HmacManager.Operator.csproj` and
`client/lib/package.json` — and those are restored from their registries at
build time. This file records the assets that are committed into the repository
instead.

## FlexSearch

- **Version:** 0.8.143
- **Source:** https://github.com/nextapps-de/flexsearch
- **License:** Apache-2.0
- **Vendored at:** `site/assets/vendor/flexsearch/` (`flexsearch.bundle.min.js`, `LICENSE`)

Powers the documentation site's search box. The file is the unmodified
upstream dist bundle, fetched from jsDelivr's npm mirror.

It is committed rather than fetched because the Hextra theme would otherwise
pull it at *build* time with `resources.GetRemote` and republish it locally.
The published page is self-hosted either way, but the default makes a site
deploy depend on a CDN being reachable — so a jsDelivr outage would fail a
deploy that has nothing to do with jsDelivr. `site/hugo.toml` points
`params.search.flexsearch.js` at the vendored copy, which is what selects the
local-asset branch of the theme's `scripts/search.html`.

## Inter

- **Version:** the Google Fonts build (v20), as packaged by `@fontsource-variable/inter` 5.3.0
- **Source:** https://github.com/rsms/inter
- **License:** SIL Open Font License 1.1
- **Vendored at:** `site/assets/vendor/inter/` (`inter-latin-wght-normal.woff2`, `LICENSE`)

The documentation site's text face. The file is the unmodified variable
font, Latin subset, from Fontsource's npm package; `site/layouts/_partials/oss/fonts.html`
declares it.

## JetBrains Mono

- **Version:** the Google Fonts build (v24), as packaged by `@fontsource-variable/jetbrains-mono` 5.3.0
- **Source:** https://github.com/JetBrains/JetBrainsMono
- **License:** SIL Open Font License 1.1
- **Vendored at:** `site/assets/vendor/jetbrains-mono/` (`jetbrains-mono-latin-wght-normal.woff2`, `LICENSE`)

The documentation site's code face, used for commands and code blocks.
Unmodified, Latin subset, from the same source as Inter.
