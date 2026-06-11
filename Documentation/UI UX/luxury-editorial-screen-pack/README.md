# Mabuntle Luxury Editorial Screen Pack

This pack contains deterministic high-fidelity mockup screenshots for the current Angular route surface. It is a design reference only; it does not change runtime Angular code.

## Visual Direction

The direction is a Mabuntle-specific luxury editorial hybrid informed by South African and global luxury commerce references: restrained black and warm ivory, champagne borders, plum workspace navigation, rose accents, product-led editorial surfaces, and dense operational seller/admin dashboards.

The references were used for broad commerce patterns only. No third-party product imagery, brand assets, layouts, or copy are copied into the mockups.

## Contents

- `source/mockup.html`, `source/mockup.js`, `source/styles.css`: static mockup source.
- `source/render-pack.ps1`: screenshot generator using local Chrome headless.
- `desktop/*.png`: 1440px-wide route screenshots.
- `mobile/*.png`: 390px-wide route screenshots.
- `contact-sheets/*.png`: grouped review sheets.
- `route-index.json`: route-to-file coverage map.

## Regenerate

```powershell
powershell -ExecutionPolicy Bypass -File "Documentation\UI UX\luxury-editorial-screen-pack\source\render-pack.ps1"
```

Expected route screenshot count: `136` (`68` routes at desktop and mobile widths), plus `12` grouped contact-sheet PNGs.
