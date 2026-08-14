# Third-party notices

The HmacManager library, the ext-authz service and the operator declare their
dependencies in the usual places — `src/HmacManager.csproj`,
`kubernetes/service/HmacManager.Kubernetes.csproj`,
`kubernetes/operator/HmacManager.Operator.csproj` and
`client/lib/package.json` — and those are restored from their registries at
build time. This file records the one asset that is committed into the
repository instead.

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
