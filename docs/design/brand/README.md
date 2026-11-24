# Brand assets

The single source for CodeBench's mark, lockup and icons. Original work (ADR 0006); see the identity section of
[../brief.md](../brief.md).

## The mark

A prompt chevron `›` and a block cursor on an accent tile — a cursor waiting for input. 32 × 32 grid:

| Part | Geometry | Colour |
| --- | --- | --- |
| Tile | `rect 32×32, rx 7` | `--cb-accent` (#0a6670 light · #56c6cf dark) |
| Chevron | `path d="M9.5 10.5 15 16l-5.5 5.5"`, stroke 3, round caps/joins | `--cb-on-accent` (#ffffff · #062a2e) |
| Cursor | `rect x 17.5, y 14.5, 7×8, rx 1.25` — sits on the chevron's baseline | `--cb-on-accent` |

Contrast is the accent / on-accent pair already measured in `tokens.css`: 6.67:1 light, 7.53:1 dark. The cursor
is a short block on the baseline on purpose: a full-height bar next to the chevron reads as the media
"skip to end" icon at 16–20 px, and an underscore is the generic terminal icon.

## The lockup

Mark at 20 px (`size-5`) + 8 px gap + `codebench` — lowercase, mono font, semibold, `tracking-tight`, `text-ink`.
Implemented by `apps/web/src/components/Wordmark.tsx` and `apps/marketing/src/components/Wordmark.astro`, which draw
the mark inline with `fill-accent` / `stroke-on-accent` so it follows light and dark. Use `<Wordmark />` everywhere
the brand appears; never type the name with a text caret (`▍`) again.

Don't: recolour the tile outside the accent role, put the mark on the accent colour, stretch it, add effects, or
draw the name in another font.

## Files

| File | Use | Copied to |
| --- | --- | --- |
| `mark.svg` | Reference artwork (light colours), slides, README badges | — |
| `favicon.svg` | Browser tab icon; switches to the dark colours under `prefers-color-scheme: dark` | `apps/web/public/`, `apps/marketing/public/` |
| `apple-touch-icon.png` | 180 × 180 home-screen icon, full-bleed (iOS rounds the corners), light colours | `apps/web/public/`, `apps/marketing/public/` |

The copies must stay byte-identical, and every file that draws the mark must use the geometry above —
`scripts/check-rules.ps1` **FE-18** fails otherwise. After changing an asset here:

```powershell
foreach ($app in 'web', 'marketing') {
  Copy-Item docs/design/brand/favicon.svg, docs/design/brand/apple-touch-icon.png "apps/$app/public/" -Force
}
```

`apple-touch-icon.png` is rendered from the mark (square tile, no radius) at 180 × 180 with any headless browser,
e.g. Playwright: `page.setContent(svg)` → `page.screenshot({ clip: { x: 0, y: 0, width: 180, height: 180 } })`.
