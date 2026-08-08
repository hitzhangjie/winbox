# WinBox UI reference

Companion to [SKILL.md](SKILL.md). Agents read this when implementing or reviewing UI.

## Benchmark interaction map

What users expect after Alfred / Raycast / Spotlight / Listary / PowerToys:

| Expectation | Why it matters | WinBox today (Host `Ui/`) |
|-------------|----------------|---------------------------|
| Fixed, memorable summon position | Muscle memory | Hard-coded top-center (`Top = workArea.Top + 120`); not draggable; not persisted |
| Soft material / rounded chrome | Feels “OS integrated” | Flat `#202020` rectangle, `WindowStyle=None`, no radius/shadow/Mica |
| Two-line results + icon | Scanability | `ListBox` of concatenated `"Title  —  Subtitle"` strings |
| Vertical scroll only | Long paths must not shove layout | Default `ListBox` can show **horizontal** scrollbar |
| Selection chrome | Orientation while arrowing | Stock ListBox selection |
| Empty / no-result states | Trust | Results panel collapses; no explicit empty state |
| Footer shortcut hints | Discoverability | None (Alt+Enter exists but invisible) |
| Settings as preferences | Authority | Single dark form; stock buttons; functional but “dev tool” |
| Tray as brand mark | Always-visible identity | 16×16 runtime-drawn circle + “W” |
| Theme / position prefs | Personalization | Not exposed |

Severity guide for prioritization:

- **P0 (trust/usability)**: horizontal scroll, string-concat rows, missing keyboard discoverability for existing actions
- **P1 (product feel)**: radius/material, selection style, empty states, position drag+persist
- **P2 (brand)**: tray ICO, settings IA, motion, accent theming
- **P3 (platform)**: Mica/Acrylic, multi-monitor quirks, light theme

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

## Design tokens (proposed Host constants)

Centralize in something like `Host.Ui/WinBoxTheme.cs` (name flexible):

| Token | Value | Use |
|-------|-------|-----|
| `SurfaceOverlay` | `#1C1C1C` | Overlay background |
| `SurfaceRaised` | `#242424` | Settings panels |
| `SurfaceSunken` | `#141414` | Result list well |
| `BorderSubtle` | `#3A3A3A` | Hairlines |
| `TextPrimary` | `#F2F2F2` | Input + titles |
| `TextSecondary` | `#9A9A9A` | Subtitles / hints |
| `Accent` | `#8AC7FF` | Mode, focus, primary CTA |
| `Selection` | `#2A3A48` or Accent @ 22% opacity | Selected row |
| `RadiusOverlay` | 12–16 | Overlay + clip |
| `FontInput` | 18–20 | Query box |
| `FontTitle` | 14 | Result title |
| `FontSubtitle` | 12 | Result subtitle |
| `RowHeight` | 44–48 | Result row |
| `OverlayWidth` | 560–640 | Fixed; do not grow with path length |

Light theme is optional later; do not fork layouts when adding it — swap tokens only.

## Scrolling rules

1. `ScrollViewer.HorizontalScrollBarVisibility = Disabled` on results and settings forms.
2. Long `Subtitle` / paths: `TextTrimming = CharacterEllipsis`; full string in `ToolTip`.
3. Prefer custom thin vertical scrollbar or `ScrollBar` style; avoid chunky classic bars if easy.
4. Never increase window width to avoid ellipsis.

## Result model evolution (Abstractions)

Current:

```csharp
QueryResultItem(Id, Title, Subtitle?, Payload?, Action)
```

Additive fields to consider (only when a real UI needs them):

| Field | Purpose |
|-------|---------|
| `IconKey` or `Glyph` | Host maps to frozen icon / Segoe Fluent glyph |
| `Accessory` | Right-aligned hint (`Enter`, score, type) |
| `Group` | Section headers (Apps / Files) later |

Do not put WPF `ImageSource` into Abstractions.

## Settings IA (target)

```text
[ Index ] [ General ] [ Shortcuts ]     ← tabs or nav list
  Index roots …
  Excludes …
  Extensions …
  [Save & rebuild]  [Close]
```

Short-term acceptable: keep one page but visually group with spacing + section headers that match overlay typography (not stock gray GroupBox).

## Tray asset checklist

- Source SVG/vector in `assets/` (or `src/WinBox.Host/Assets/`)
- Export `.ico` with 16, 20, 24, 32 (and 256 if packaging)
- Monochrome-friendly silhouette; accent optional at 32+
- Load from resource, not `Graphics.DrawString`

## Technical route (WPF Host)

Stay on **WPF + WinForms NotifyIcon** for stage 1 (already shipping).

Recommended incremental path:

1. **Tokens + result DataTemplate** (fixes concat string + selection + ellipsis)
2. **Clip + radius + optional drop shadow / Mica** (`DwmSetWindowAttribute`)
3. **Drag reposition + persist** (JSON beside index options or `%AppData%/WinBox/ui.json`)
4. **Footer hints + empty states**
5. **Tray `.ico` + settings visual pass**
6. Only then: plugin-authored accessory fields

Avoid rewriting in WinUI 3 mid-flight unless packaging strategy changes.

## Manual QA script

```
make run
1. Hotkey → overlay appears, caret in input
2. Type query with long path hit → no horizontal scrollbar; ellipsis OK
3. ↓↑ moves selection; Enter opens; Alt+Enter reveals; Esc hides
4. Mode prefix (if any) → Backspace at start exits mode
5. Tray → Open launcher / Settings / Quit
6. Settings → edit root → Save & rebuild → status updates; Close
7. Multi-monitor (if available): summon on active screen’s work area
```

## Known gap backlog (status)

Ordered for product feel vs effort:

1. ~~Result row template + kill horizontal scroll (P0)~~ — done (Host `ResultRowView` + scroll policy)
2. ~~Overlay corner radius + selection styling (P1)~~ — done (`WinBoxTheme` + chrome border)
3. ~~Position drag + persist (P1)~~ — done (`ui-options.json`)
4. ~~Empty / no-result + footer shortcut hints (P1)~~ — done
5. Tray multi-size ICO (P2) — interim geometric tray mark; still want packaged `.ico`
6. Settings section visual + button styling (P2) — token pass done; tabbed IA still open
7. Mica / shadow / motion (P2–P3)
8. Theme preference (P3)
