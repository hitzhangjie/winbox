---
name: winbox-ui
description: >-
  WinBox launcher and settings UI design system: visual language, interaction
  craft, Host vs plugin UI ownership, systematic look-and-feel audit, and PR
  checklist. Use when changing overlay, tray, settings, result rows, scrolling,
  theming, or any user-visible Host.Ui surface; when adding plugin result
  presentation that must match launcher chrome; or when the user asks why the
  UI looks rough, how to polish it, or to evaluate visual / interaction / UX
  quality.
---

# WinBox UI (launcher design system)

WinBox’s primary UX is a **summonable command launcher**, not a document app.
Benchmarks: Alfred / Raycast / Spotlight / Listary / PowerToys Command Palette.
Match their *interaction grammar*; do not paste their brand chrome.
Feel Windows-native (Fluent geometry, Segoe, calm surfaces)—not a web landing page.

**Stage 1 (foundation) is largely shipped** (tokens, two-line rows, radius, empty/footer,
drag+persist, tray ICO, settings tabs, theme). Further work is **craft**: hierarchy,
discoverability, density rhythm, copy, and “product not prototype” polish.

Read [reference.md](reference.md) for tokens, anatomy, and stage-2 backlog.
Read [craft-audit.md](craft-audit.md) when evaluating or planning UI polish.

## Intent filter (before coding)

1. **Outcome**: what the user feels faster / clearer / calmer after the change
2. **Non-goals**: do not redesign settings, tray, and overlay in one PR unless asked
3. **Surface**: Host shell chrome vs plugin-provided *content* (title/subtitle/action)
4. **Maturity**: foundation fix vs craft polish — name which; prefer one craft theme per PR

If the change is only visual/interaction, still follow `winbox-change-loop` + `make test`
for any behavior that affects activation, hotkeys, or result actions.

## Ownership (architecture)

| Concern | Owner |
|---------|--------|
| Overlay window, hotkey, focus, dismiss, keyboard nav | `WinBox.Host` `Ui/` |
| Theme tokens, result row template, scroll policy | Host shell (shared) |
| Tray icon asset + menu | Host |
| Settings window chrome + layout patterns | Host |
| Open/Save dialog assist strip (file search only) | Host `Ui/DialogAssist/` — system light/dark palette; not launcher QueryRouter |
| Result *data*: Title, Subtitle, Payload, Action | Plugins via `QueryResultItem` |
| Search-specific empty hints / index status copy | Search plugin → Host binds |

Plugins **must not** invent their own floating windows for launcher results.
Prefer extending `QueryResultItem` (additive fields) when the shell needs new
presentation hooks (icon key, accessory text, score). Host renders.

Hard rule: **never hardcode a second palette** — use `WinBoxTheme` / `UiLayout`.

## Design pillars (non-negotiable)

1. **Keyboard is primary** — mouse is optional polish. Every action has a key path.
2. **One focus** — input owns attention; results are subordinate list, never a second app.
3. **No horizontal scroll** — truncate with ellipsis; show full path in subtitle or tooltip.
4. **Calm selection** — subtle highlight + clear selected row; avoid default WinForms/WPF chrome.
5. **Instant summon / dismiss** — Esc always dismisses; reopen policy stays documented & stable.
6. **Density with air** — Raycast-like row height (~40–48px), not spreadsheet rows, not sparse cards.
7. **Same tokens everywhere** — overlay, settings, tray menu accents share one palette/radius/type scale.
8. **Progressive settings** — advanced lists behind sections; primary actions obvious (Save / Cancel).

## Craft pillars (stage 2 — “好看、友好”)

Borrowed from Fluent (Windows), launcher UX (Raycast/Alfred grammar), and design-system discipline
(tokens over magic numbers). Adapt to WinBox; do not clone macOS vibrancy or web marketing layouts.

1. **Hierarchy before decoration** — type weight/size + muted subtitle do more than extra borders/shadows.
2. **4px rhythm** — padding/gaps on 4/8/12/16; nested radius: overlay ~12–16, inner controls ~4–8.
3. **One accent job** — accent marks focus / mode / primary CTA only; never rainbow chrome.
4. **Teach in the chrome** — shortcuts visible (footer or row accessory), not only in docs.
5. **Empty states that help** — name the query; suggest next step; never “Error: 0 results”.
6. **Speed is a feature** — UI paints immediately; heavy work is async with calm status text.
7. **Personality without latency** — warmer copy OK; no confetti/motion that costs summon time.
8. **Platform fit** — Segoe UI Variable, Fluent-ish geometry, respect light/dark/system; reduced motion when easy.

## Interaction grammar (launcher)

| Input | Behavior |
|-------|----------|
| Hotkey | Show + focus input + select-all or clear (pick one policy; keep stable) |
| Esc | Dismiss; never leave a zombie topmost window |
| ↑ / ↓ | Move selection; keep selected row visible (vertical only) |
| Enter | Activate selected (or first) default action |
| Alt+Enter | Reveal in Explorer when action is path-like (already established) |
| Backspace at start in mode chrome | Exit mode prefix (already established) |
| Click outside (future) | Dismiss if we add click-through shield; until then Esc/hotkey only |

Empty query: hide results or show recents/actions — **do not** show a broken empty ListBox chrome.
No results: one calm “No results” row, not a blank panel.

## Visual baseline

Host UI converges on tokens in `WinBoxTheme` / `UiLayout` (see [reference.md](reference.md)):

- **Surface**: dark/light neutrals, 1px hairline, **corner radius 12–16** on overlay
- **Accent**: single cool accent for mode / focus / primary — not rainbow
- **Type**: Segoe UI Variable / Segoe UI; input ~18–20px; title 14px semibold; subtitle 12px muted
- **Results**: two-line template (title + subtitle), optional glyph left; **never** `"Title — Subtitle"` as one string
- **Scroll**: vertical auto; **horizontal disabled**; thin overlay scrollbar or hide until hover
- **Motion**: ~80–120ms opacity/position ease on show/hide; no bounce

Do **not** ship: default ListBox blue selection, visible horizontal scrollbar, sharp 0-radius rectangles, system MessageBox as the only settings feedback for success paths.

## Systematic audit (required for polish work)

When the user asks to improve look/feel, or before a UI craft PR, run the audit in
[craft-audit.md](craft-audit.md) and report:

1. **Verdict** — foundation-complete vs craft gaps (1–2 sentences)
2. **Top 3 fixes** — ranked by user-visible friendliness vs effort
3. **Non-goals** — what this pass will not touch
4. **Proposed PR slice** — one craft theme (e.g. “result scanability” only)

Do not start a drive-by restyle of every surface.

## Tray & brand

- Ship a real **multi-size `.ico`** (16/20/24/32), silhouette readable at 16px
- Avoid runtime `DrawString("W")` as the long-term brand
- Tooltip: `WinBox`; menu labels short and verb-led (`Open launcher`, `Settings`, `Quit`)

## Settings

- Prefer **sectioned layout** (General / Index / Shortcuts) over one infinite form
- Lists of paths: compact rows + clear Add/Remove; show truncated path with tooltip
- Primary button = destructive-or-heavy action labeled honestly (`Save & rebuild`)
- Status line for async work; never freeze UI without progress text
- Settings should feel like the same product as the overlay (tokens, type, button chrome)

## PR checklist (UI)

```
UI:
- [ ] Follows design + craft pillars above
- [ ] No horizontal scrollbar on launcher results
- [ ] Result rows use title + subtitle template (not concatenated string)
- [ ] Colors/spacing from WinBoxTheme / UiLayout (or documented exception)
- [ ] Keyboard paths unchanged or intentionally updated + tested
- [ ] Plugin only supplies content; Host owns chrome
- [ ] If polish: craft-audit top findings addressed or explicitly deferred
- [ ] Manual: `make run` — summon, type, arrow, Enter, Esc, Alt+Enter
- [ ] If behavior changed: automated tests updated (winbox-testing)
```

## Anti-patterns

- Hard-coding a second palette inside a plugin window
- Growing overlay width to “fit” long paths (causes horizontal scroll)
- Modal dialogs for routine launcher actions
- Putting index rebuild controls inside the overlay
- Drive-by redesign of unrelated surfaces in a feature PR
- Web-landing aesthetics (hero gradients, card grids, marketing type) on the launcher
- Adding UI libraries (Wpf.Ui, MahApps, etc.) without an explicit architecture decision

## Related skills

- Placement: [winbox-architecture](../winbox-architecture/SKILL.md)
- Process: [winbox-change-loop](../winbox-change-loop/SKILL.md)
- Verify: [winbox-testing](../winbox-testing/SKILL.md)
