# The documentation site

A [Hugo](https://gohugo.io/) site on the [Hextra](https://github.com/imfing/hextra)
theme. Landing page at `/`, documentation under `/docs/`, published to
<https://jzills.github.io/hmac-manager/> by `.github/workflows/pages.yml`.

## Running it

```bash
hugo server        # from this directory
```

**Hugo must be the extended build, version 0.146.0 or newer.** Hextra declares
that minimum, and the site uses template functions that do not exist before it.
CI pins **0.164.0** (`HUGO_VERSION` in `pages.yml`); matching that locally is the
safest choice.

### If `hugo server` fails

```
Error: ... _partials/scripts/asciinema.html:37:1": parse failed:
       template: ...: function "try" not defined
WARN  Module "github.com/imfing/hextra" is not compatible with this Hugo version: Min 0.146.0
```

Your Hugo is too old. `try` arrived in 0.141.0. Ubuntu's `hugo` package is a
common cause — `apt` ships 0.123.7 on 24.04, which is three years of template
functions behind Hextra.

```bash
hugo version       # check what you actually have
```

Install the pinned build without touching the system package:

```bash
HUGO_VERSION=0.164.0
curl -sSL -o /tmp/hugo.tar.gz \
  "https://github.com/gohugoio/hugo/releases/download/v${HUGO_VERSION}/hugo_extended_${HUGO_VERSION}_linux-amd64.tar.gz"
tar xzf /tmp/hugo.tar.gz -C /tmp hugo
install /tmp/hugo ~/.local/bin/hugo
```

`~/.local/bin` is not on `PATH` on every system. Either add it, or run the
binary directly:

```bash
~/.local/bin/hugo server
```

The **extended** build is required, not the standard one — Hextra compiles SCSS.
`hugo version` prints `+extended` when you have the right one.

## Layout

| Path | What it is |
| --- | --- |
| `content/_index.md` | The landing page — hand-written HTML in Markdown |
| `content/docs/` | The documentation; sidebar order comes from `weight` in front matter |
| `assets/css/hm-theme.css` | The palette: light and dark, plus Hextra's `--primary-*` |
| `assets/css/custom.css` | Everything else; reads `--hm-*` and hardcodes no colour |
| `layouts/` | Three theme overrides and the shortcodes |
| `static/` | Favicons, named to shadow Hextra's own |
| `tools/` | The mark and favicon generators — see [tools/README.md](tools/README.md) |

Theme updates are deliberate: Hextra is pinned in `go.mod` and kept out of
`dependabot.yml`.

## Publishing

`pages.yml` builds on every PR and publishes from `develop`. The `gh-pages`
branch is **shared with the Helm chart repository** — `index.yaml` and the chart
tarballs are served from the same root and must survive every site deploy. See
the `pages.yml` section of [CLAUDE.md](../CLAUDE.md) before changing anything
that writes to that branch.
