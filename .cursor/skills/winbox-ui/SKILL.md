---
name: winbox-ui
description: >-
  WinBox launcher and settings UI design system: visual language, interaction
  patterns, Host vs plugin UI ownership, and PR checklist. Use when changing
  overlay, tray, settings, result rows, scrolling, theming, or any user-visible
  Host.Ui surface; also when adding plugin result presentation that must match
  launcher chrome.
---

# WinBox UI (launcher design system)

WinBox’s primary UX is a **summonable command launcher**, not a document app.
Benchmarks: Alfred / Raycast / Spotlight / Listary / PowerToys Command Palette.
Match their *interaction grammar*; do not paste their brand chrome.

Read [reference.md](reference.md) for tokens, anatomy, and known gaps.

## Intent filter (before coding)

1. **Outcome**: what the user feels faster / clearer after the change
2. **Non-goals**: do not redesign settings, tray, and overlay in one PR unless asked
3. **Surface**: Host shell chrome vs plugin-provided *content* (title/subtitle/action)

If the change is only visual/interaction, still follow `winbox-change-loop` + `make test`
for any behavior that affects activation, hotkeys, or result actions.

## Ownership (architecture)

| Concern | Owner |
|---------|--------|
| Overlay window, hotkey, focus, dismiss, keyboard nav | `WinBox.Host` `Ui/` |
| Theme tokens, result row template, scroll policy | Host shell (shared) |
| Tray icon asset + menu | Host |
| Settings window chrome + layout patterns | Host |
| Result *data*: Title, Subtitle, Payload, Action | Plugins via `QueryResultItem` |
| Search-specific empty hints / index status copy | Search plugin → Host binds |

Plugins **must not** invent their own floating windows for launcher results.
Prefer extending `QueryResultItem` (additive fields) when the shell needs new
presentation hooks (icon key, accessory text, score). Host renders.

## Design pillars (non-negotiable)

1. **Keyboard is primary** — mouse is optional polish. Every action has a key path.
2. **One focus** — input owns attention; results are subordinate list, never a second app.
3. **No horizontal scroll** — truncate with ellipsis; show full path in subtitle or tooltip.
4. **Calm selection** — subtle highlight + clear selected row; avoid default WinForms/WPF chrome.
5. **Instant summon / dismiss** — Esc always dismisses; reopen restores a clean or last-query policy (document which).
6. **Density with air** — Raycast-like row height (~40–48px), not spreadsheet rows, not sparse cards.
7. **Same tokens everywhere** — overlay, settings, tray menu accents share one palette/radius/type scale.
8. **Progressive settings** — advanced lists behind sections; primary actions obvious (Save / Cancel).

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

## Visual baseline (stage 1)

Until a full theme system lands, Host UI must converge on:

- **Surface**: dark neutral (`#1C1C1C`–`#252525`), 1px hairline border (`#3A3A3A`), **corner radius 12–16** on overlay
- **Accent**: single cool accent (`#8AC7FF`) for mode label / focus ring / primary button — not rainbow
- **Type**: Segoe UI Variable / Segoe UI; input ~18–20px; title 14px semibold; subtitle 12px muted
- **Results**: two-line template (title + subtitle), optional 20px glyph/icon left; **never** `"Title — Subtitle"` as one string
- **Scroll**: vertical auto; **horizontal disabled**; thin overlay scrollbar or hide until hover
- **Motion**: 80–120ms opacity/position ease on show/hide; no bounce

Do **not** ship: default ListBox blue selection, visible horizontal scrollbar, sharp 0-radius rectangles, system MessageBox as the only settings feedback for success paths.

## Tray & brand

- Ship a real **multi-size `.ico`** (16/20/24/32), silhouette readable at 16px
- Avoid runtime `DrawString("W")` as the long-term brand
- Tooltip: `WinBox`; menu labels short and verb-led (`Open launcher`, `Settings`, `Quit`)

## Settings

- Prefer **sectioned layout** (General / Index / Shortcuts) over one infinite form
- Lists of paths: compact rows + clear Add/Remove; show truncated path with tooltip
- Primary button = destructive-or-heavy action labeled honestly (`Save & rebuild`)
- Status line for async work; never freeze UI without progress text

## PR checklist (UI)

```
UI:
- [ ] Follows design pillars above
- [ ] No horizontal scrollbar on launcher results
- [ ] Result rows use title + subtitle template (not concatenated string)
- [ ] Colors/spacing from shared tokens (or documented exception)
- [ ] Keyboard paths unchanged or intentionally updated + tested
- [ ] Plugin only supplies content; Host owns chrome
- [ ] Manual: `make run` — summon, type, arrow, Enter, Esc, Alt+Enter
- [ ] If behavior changed: automated tests updated (winbox-testing)
```

## Anti-patterns

- Hard-coding a second palette inside a plugin window
- Growing overlay width to “fit” long paths (causes horizontal scroll)
- Modal dialogs for routine launcher actions
- Putting index rebuild controls inside the overlay
- Drive-by redesign of unrelated surfaces in a feature PR

## Related skills

- Placement: [winbox-architecture](../winbox-architecture/SKILL.md)
- Process: [winbox-change-loop](../winbox-change-loop/SKILL.md)
- Verify: [winbox-testing](../winbox-testing/SKILL.md)
