#!/usr/bin/env python3
"""Draw the HmacManager marks as vector geometry.

    python3 site/tools/gen-marks.py

Writes three files, all committed. This script exists so the geometry is
reproducible and reviewable, not so it can run at build time.

    site/assets/hm-wordmark.svg   the hero wordmark, currentColor
    site/assets/hm-mark.svg       the compact navbar/favicon mark, currentColor
    assets/logo.svg               the README's copy, a fixed accent

Why this exists at all: assets/logo.svg used to draw this banner as twelve
<tspan> lines of box-drawing and slash characters, held in alignment by
textLength/lengthAdjust hints. That only renders correctly where the reader
has a monospace font carrying the box-drawing glyphs at the exact metrics the
hints assume; anywhere else it shears into offset blocks. ART below is that
original art, transcribed character for character, so what is drawn here is
the same banner and not a redesign.

The art is a fixed character grid, so the conversion is mechanical: each cell
becomes geometry, and there are only six distinct glyph shapes in the whole
banner. The frame is the exception -- ╔═╗║╚╝ is redrawn as two nested
rectangle outlines rather than per-cell corner glyphs, which is what those
characters are already trying to look like.

The output uses fill="currentColor" so both marks take their colour from the
active theme, which an <img> could never do.
"""

import pathlib

# The banner interior, exactly as it appeared between the ║ characters in the
# original assets/logo.svg. HMAC over MANAGER in the figlet "slant" face.
# This is now the source of that file rather than a copy of it.
ART = [
    "     __  ____  ______   ______                   ",
    "    / / / /  |/  /   | / ____/                   ",
    "   / /_/ / /|_/ / /| |/ /                        ",
    "  / __  / /  / / ___ / /___                      ",
    " /_/ /_/_/  /_/_/  |_\\____/                      ",
    "     __  ______    _   _____   ________________  ",
    "    /  |/  /   |  / | / /   | / ____/ ____/ __ \\ ",
    "   / /|_/ / /| | /  |/ / /| |/ / __/ __/ / /_/ / ",
    "  / /  / / ___ |/ /|  / ___ / /_/ / /___/ _, _/  ",
    " /_/  /_/_/  |_/_/ |_/_/  |_\\____/_____/_/ |_|   ",
]

# One monospace cell. The 3:5 ratio is roughly what a terminal cell is, which
# is what keeps the slanted strokes at the angle the art was drawn for.
CELL_W = 12.0
CELL_H = 20.0

# Stroke weight, measured horizontally. Measuring it horizontally rather than
# perpendicular to the stroke is deliberate: a "/" runs from the bottom-left
# corner of its cell to the top-right, so the "/" one row down and one column
# left ends exactly where this one begins. Horizontal thickness makes that
# junction seamless, which is what turns a column of separate "/" cells into
# one continuous diagonal.
STROKE = 3.2

# One cell of margin on every side, which the frame is drawn into.
MARGIN_COLS = 1
MARGIN_ROWS = 1

# The colour baked into the repository-root assets/logo.svg. See main().
README_FILL = "#58a6ff"


# Every shape below is a polygon, and they are all emitted into a single
# <path> filled with the default nonzero rule. Nonzero rather than evenodd
# because the shapes genuinely overlap: a "/" is thickened horizontally, so it
# bleeds half a stroke into the neighbouring cell, and where that neighbour is
# an "_" the two intersect. Under evenodd every one of those intersections
# would punch a hole through the letterform. Nonzero unions them instead --
# but only while every subpath winds the same way, which is what polygon()
# enforces; a subpath wound the other way would cancel exactly like evenodd.


def _clockwise(pts):
    area = 0.0
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        area += x0 * y1 - x1 * y0
    return pts if area >= 0 else pts[::-1]


def polygon(pts):
    pts = _clockwise([(round(x, 3), round(y, 3)) for x, y in pts])
    return "M" + "L".join(f"{x:g} {y:g}" for x, y in pts) + "Z"


def parallelogram(x0, y0, x1, y1, t):
    """A stroke from (x0,y0) to (x1,y1) with horizontal thickness t."""
    h = t / 2.0
    return polygon([(x0 - h, y0), (x0 + h, y0), (x1 + h, y1), (x1 - h, y1)])


def rect(x, y, w, h):
    return polygon([(x, y), (x + w, y), (x + w, y + h), (x, y + h)])


def ring(x, y, w, h, t):
    """A rectangle outline of thickness t, as four overlapping bars.

    Four bars rather than an outer rectangle minus an inner one, so the shape
    needs no fill rule of its own and can share the single nonzero path with
    everything else.
    """
    return "".join([
        rect(x, y, w, t),                      # top
        rect(x, y + h - t, w, t),              # bottom
        rect(x, y, t, h),                      # left
        rect(x + w - t, y, t, h),              # right
    ])


def glyph(ch, col, row):
    """Geometry for one character cell, or None for blank."""
    x = (col + MARGIN_COLS) * CELL_W
    y = (row + MARGIN_ROWS) * CELL_H

    if ch == " ":
        return None
    if ch == "_":
        # Sits on the cell's baseline; these form every horizontal bar in the
        # letterforms, so they must span the full advance width to meet the
        # underscore in the next cell.
        return rect(x, y + CELL_H - STROKE, CELL_W, STROKE)
    if ch == "/":
        return parallelogram(x, y + CELL_H, x + CELL_W, y, STROKE)
    if ch == "\\":
        return parallelogram(x, y, x + CELL_W, y + CELL_H, STROKE)
    if ch == "|":
        return rect(x + (CELL_W - STROKE) / 2.0, y, STROKE, CELL_H)
    if ch == ",":
        # The one piece of punctuation in the banner -- the leg of MANAGER's
        # R. A short tail hanging below the midline.
        return parallelogram(
            x + CELL_W * 0.62, y + CELL_H * 0.55,
            x + CELL_W * 0.34, y + CELL_H,
            STROKE * 0.85,
        )
    raise ValueError(f"no geometry for {ch!r} at row {row} col {col}")


def wordmark(fill="currentColor"):
    cols = max(len(line) for line in ART)
    rows = len(ART)
    width = (cols + 2 * MARGIN_COLS) * CELL_W
    height = (rows + 2 * MARGIN_ROWS) * CELL_H

    paths = []
    for row, line in enumerate(ART):
        for col, ch in enumerate(line):
            d = glyph(ch, col, row)
            if d:
                paths.append(d)

    # The ╔═╗ frame, as two nested outlines. Inset so the inner one clears the
    # art's margin cell.
    t = STROKE
    paths.append(ring(t, t, width - 2 * t, height - 2 * t, t))
    inset = CELL_W * 0.55
    paths.append(
        ring(t + inset, t + inset, width - 2 * (t + inset), height - 2 * (t + inset), t)
    )

    return f"""<!-- Generated by site/tools/gen-marks.py. Do not edit.
     The HmacManager banner as geometry rather than text: drawn as <text> it
     only lines up where the reader has a monospace font carrying the
     box-drawing glyphs at the metrics the textLength hints assume, and shears
     apart where they don't. -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:g} {height:g}"
     fill="{fill}"
     role="img" aria-label="HmacManager">
  <path d="{''.join(paths)}"/>
</svg>
"""


def mark():
    """The compact square mark: the banner's frame around a bold H.

    The wordmark is 612 units wide and unreadable at the 32px a favicon gets
    or the 2rem a navbar allows, so the navbar and the tab icon need their own
    drawing. This keeps the banner's frame and puts the H of HMAC inside it.

    A single frame, not the wordmark's double one. At 16px -- which is the
    size the browser actually reads a favicon at -- two concentric rings plus
    a letter collapse into a smudge; one thick ring and one thick letter still
    resolve. The double line is the wordmark's job, where there is room for it.
    """
    t = 6.0
    paths = [
        ring(3, 3, 58, 58, t),
        # H: two stems and a crossbar.
        rect(21, 18, 8, 28),
        rect(35, 18, 8, 28),
        rect(21, 28, 22, 8),
    ]
    return f"""<!-- Generated by site/tools/gen-marks.py. Do not edit.
     The compact mark: the banner's double-line frame around the H of HMAC,
     for the navbar and the favicon, where the full wordmark is illegible. -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"
     fill="currentColor"
     role="img" aria-label="HmacManager">
  <path d="{''.join(paths)}"/>
</svg>
"""


def main():
    site = pathlib.Path(__file__).resolve().parent.parent
    out = site / "assets"
    (out / "hm-wordmark.svg").write_text(wordmark())
    (out / "hm-mark.svg").write_text(mark())
    print(f"wrote {out}/hm-wordmark.svg and {out}/hm-mark.svg")

    # The README's copy. Same geometry, but a fixed colour rather than
    # currentColor: GitHub renders it through an <img>, where the SVG has no
    # page to inherit from and currentColor resolves to black — invisible on
    # GitHub's dark theme. #58a6ff is the site's dark-mode accent and reads on
    # both of GitHub's.
    readme = site.parent / "assets" / "logo.svg"
    readme.write_text(wordmark(fill=README_FILL))
    print(f"wrote {readme}")


if __name__ == "__main__":
    main()
