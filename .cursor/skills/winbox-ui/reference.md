# WinBox UI reference

Companion to [SKILL.md](SKILL.md). Agents read this when implementing or reviewing UI.
For look-and-feel evaluation, use [craft-audit.md](craft-audit.md).

## Benchmark interaction map

What users expect after Alfred / Raycast / Spotlight / Listary / PowerToys:

| Expectation | Why it matters | WinBox today (Host `Ui/`) |
|-------------|----------------|---------------------------|
| Fixed, memorable summon position | Muscle memory | Draggable + persisted (`ui-options.json`) |
| Soft material / rounded chrome | Feels “OS integrated” | Radius + shadow; settings Mica/round when available |
| Two-line results + icon | Scanability | `ResultRowView` title/subtitle + **shell file icons** (Explorer association); MDL2 glyph fallback for toolbox |
| Vertical scroll only | Long paths must not shove layout | Horizontal scroll disabled; ellipsis + tooltip |
| Selection chrome | Orientation while arrowing | Themed selection (not stock blue) |
| Empty / no-result states | Trust | Empty/no-result text present |
| Footer shortcut hints | Discoverability | Footer hints present |
| Settings as preferences | Authority | Tabbed Index / General / Shortcuts + tokens |
| Tray as brand mark | Always-visible identity | Multi-size `Assets/winbox.ico` |
| Theme / position prefs | Personalization | Dark / light / system + layout knobs |

## Maturity

| Stage | Status | Goal |
|-------|--------|------|
| **1 Foundation** | Largely done | Tokens, rows, scroll, radius, empty/footer, tray, settings IA, theme |
| **2 Craft** | Active | Hierarchy, scanability, teaching chrome, settings polish, microcopy |
| **3 Platform** | Later | Deeper Fluent/Mica, a11y depth, optional native Fluent theme |

Do not reopen stage-1 work unless a regression appears. Prefer craft-audit → ranked P1/P2 fixes.

## Overlay anatomy (target)

```text
┌──────────────────────────────────────────────┐  ← radius 12–16, 1px border
│  [mode?]  │  query input…………………            │  ← 56–60px row
├──────────────────────────────────────────────┤
│  📄  Title (semibold)                    ⌘↵ │  ← ~44px row
│      subtitle path truncated…               │
│  📄  Title                                  │
│      subtitle…                              │
│  … vertical scroll only …                   │
├──────────────────────────────────────────────┤
│  Enter open · Alt+Enter reveal · Esc close  │  ← 28px footer (optional)
└──────────────────────────────────────────────┘
Width: ~560–640px. Max results height: ~320–360px (~6–7 rows).
```

### Mode chrome

Keep `ModeLabel | payload` pattern. Accent color on label only; separator muted.
Backspace-at-start exits mode (already implemented) — preserve.

## Design tokens (Host)

Centralized in `WinBoxTheme` + live knobs in `UiLayout`:

| Token | Approx value | Use |
|-------|--------------|-----|
| `SurfaceOverlay` | dark `#1C1C1C` / light pair | Overlay background |
| `SurfaceRaised` | raised panel | Settings panels |
| `SurfaceSunken` | list wells in **settings** only | Settings cards / fields — **not** launcher results |
| `BorderSubtle` | hairline | Borders |
| `TextPrimary` / `TextSecondary` | high / muted | Input, titles / subtitles |
| `Accent` | cool single accent | Mode, focus, primary CTA |
| `Selection` | accent-tinted wash | Selected row |
| `Hover` | theme-aware wash | Unselected row hover |
| `TextOnAccent` | white | Primary button label |
| `OverlayRadius` | 14 | Overlay + clip |
| `ControlRadius` | 8 | Rows, buttons, inner chrome |
| `FontInput` / `FontTitle` / `FontSubtitle` | 18 / 14 / 12 | Type ramp |
| `ResultRowMinHeight` | 44 | Result row |
| `OverlayWidth` | 600 default | Fixed; do not grow with path length |
| Spacing rhythm | 4 / 8 / 12 / 16 | Margins & gaps |

Light / system themes swap colors only — do not fork layouts.

## Scrolling rules

1. Horizontal scroll **disabled** on results and settings forms.
2. Long `Subtitle` / paths: `TextTrimming = CharacterEllipsis`; full string in `ToolTip`.
3. Prefer thin vertical scrollbar (`ThemedScrollBars`) or hide-until-hover.
4. Never increase window width to avoid ellipsis.

## Result model evolution (Abstractions)

Current:

```csharp
QueryResultItem(Id, Title, Subtitle?, Payload?, Action, IconKey?)
```

Additive fields to consider (only when a real UI needs them):

| Field | Purpose |
|-------|---------|
| ~~`IconKey` or `Glyph`~~ | ~~Host maps to frozen icon / Segoe Fluent glyph~~ — shipped (`ResultIconKeys` + Host glyph map) |
| `Accessory` | Right-aligned hint (`Enter`, score, type) |
| `Group` | Section headers (Apps / Files) later |

Do not put WPF `ImageSource` into Abstractions.

## Settings IA

```text
[ Index ] [ General ] [ Shortcuts ]     ← tabs
  Index roots …
  Excludes …
  Extensions …
  [Save & rebuild]  [Close]
```

Craft bar: same type/spacing/button language as overlay; section headers not stock gray GroupBox.

## Tray asset checklist

- Source SVG/vector in `assets/` (or `src/WinBox.Host/Assets/`)
- Export `.ico` with 16, 20, 24, 32 (and 256 if packaging)
- Monochrome-friendly silhouette; accent optional at 32+
- Load from resource (`TrayIconFactory`), not `Graphics.DrawString`

## Technical route (WPF Host)

Stay on **WPF + WinForms NotifyIcon** for craft stage.

Recommended craft path (pick one theme per PR):

1. **Scanability** — match highlight; optional accessory column; tighter glyph alignment
2. **Teaching chrome** — dynamic footer by context; mode exit hint
3. **Empty/recents** — empty-query useful defaults (recents/actions) without clutter
4. **Settings craft** — control templates, spacing rhythm, list rows matching overlay density
5. **Feedback** — activation/status microcopy; reduced-motion path
6. Only with Abstractions need: `IconKey` / `Accessory` / `Group`

Avoid rewriting in WinUI 3 or adopting Wpf.Ui mid-flight unless packaging strategy changes.

## Manual QA script

```
make run
1. Hotkey → overlay appears, caret in input
2. Type query with long path hit → no horizontal scrollbar; ellipsis OK
3. ↓↑ moves selection; Enter opens; Alt+Enter reveals; Esc hides
4. Mode prefix (if any) → Backspace at start exits mode
5. Tray → Open launcher / Settings / Quit
6. Settings → edit root → Save & rebuild → status updates; Close
7. Theme dark/light/system → overlay + settings stay coherent
8. Multi-monitor (if available): summon on active screen’s work area
```

## Stage-2 craft backlog (status)

Ordered for friendliness vs effort (re-audit after each slice):

1. ~~**Theme craft (light/dark)** (P1)~~ — refined tokens, hover, shadow, empty/idle copy, settings rebind
2. ~~**Settings panel craft** (P1)~~ — path list cards, underline tabs, padding rhythm, card/footer shadows
2b. ~~**Settings density + tray chrome** (P1)~~ — content-sized lists, custom combo/slider, window icon, themed tray menu + mark
2c. ~~**Tray menu + settings flatten** (P1)~~ — rounded/centered tray menu, 2×2 window tray mark, flat settings (no list cards), filled slider
3. **Result match highlighting** (P2) — bold/accent the matched substring in title
4. **Row accessory hints** (P2) — right-aligned action/key hint without widening window
5. **Empty-query recents** (P2) — optional recent/actions list beyond idle hint
6. **Dynamic footer** (P2) — ~~basic context footer done~~; richer per-action accessories still open
7. **Contrast / focus ring pass** (P2) — deepen keyboard focus visuals beyond caret accent
8. ~~**Icon fidelity** (P2)~~ — Explorer shell icons for path results; `IconKey` + MDL2 glyph fallback for toolbox
9. **Reduced motion** (P3) — skip fade when OS requests reduced motion

Stage-1 checklist (historical, done):

1. ~~Result row template + kill horizontal scroll~~
2. ~~Overlay corner radius + selection styling~~
3. ~~Position drag + persist~~
4. ~~Empty / no-result + footer shortcut hints~~
5. ~~Tray multi-size ICO~~
6. ~~Settings section visual + button styling~~
7. ~~Mica / shadow / motion~~
8. ~~Theme preference~~
