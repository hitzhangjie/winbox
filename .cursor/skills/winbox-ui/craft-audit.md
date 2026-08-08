# WinBox UI craft audit

Companion to [SKILL.md](SKILL.md). Use when judging whether the product looks/feels
friendly—not only whether features exist.

**Sources distilled (do not cargo-cult):**

| Source | What we keep | What we discard |
|--------|--------------|-----------------|
| Fluent / Windows 11 geometry & type | 4px spacing rhythm; overlay vs control radius; Segoe hierarchy; calm accent | Full WinUI control kit mid-flight; cloning Office chrome |
| Launcher UX (Raycast / Alfred / PowerToys grammar) | Keyboard-first, dense rows, inline shortcut teaching, helpful empty copy, &lt;~100ms feel | macOS vibrancy fetish; confetti; Inter/ss03 brand typography |
| Design-system discipline (Figma-style tokens) | No magic colors; reuse Host tokens/components; one system | Pixel-perfect Figma pipeline (we have no Figma source of truth yet) |
| Cursor web frontend taste rules | One job per surface; reduce clutter; motion with purpose | Landing-page hero rules (not applicable to summonable overlay) |

## When to run

- User says UI is ugly / unfriendly / “可用但不好看”
- Planning a polish PR or UI epic
- Reviewing a PR that touches `Host.Ui`
- After stage-1 foundation work, before declaring “UI done”

## How to run (agent)

1. Open overlay + settings via `make run` (or reason from `Host.Ui` code if headless).
2. Score each lens below: **Good / OK / Weak**.
3. List concrete gaps with file hints under `src/WinBox.Host/Ui/`.
4. Rank **Top 3** by (user-visible friendliness × feasibility).
5. Propose **one PR slice** — one craft theme only.

## Audit scorecard

Copy and fill:

```
WinBox UI craft audit:
Date / branch:
Surfaces reviewed: [ ] Overlay  [ ] Results  [ ] Empty/footer  [ ] Settings  [ ] Tray

Lenses:
- [ ] L1 Visual hierarchy     Good / OK / Weak — notes:
- [ ] L2 Density & rhythm     Good / OK / Weak — notes:
- [ ] L3 Selection & focus    Good / OK / Weak — notes:
- [ ] L4 Scanability          Good / OK / Weak — notes:
- [ ] L5 Discoverability      Good / OK / Weak — notes:
- [ ] L6 Empty & error copy   Good / OK / Weak — notes:
- [ ] L7 Settings product feel Good / OK / Weak — notes:
- [ ] L8 Brand & tray         Good / OK / Weak — notes:
- [ ] L9 Motion & feedback    Good / OK / Weak — notes:
- [ ] L10 A11y & contrast     Good / OK / Weak — notes:

Top 3 fixes:
1.
2.
3.

Non-goals this pass:
Proposed PR slice (one theme):
```

## Lenses (what “Good” means)

### L1 Visual hierarchy

- Input is the loudest element; results quieter; footer quietest.
- Title semibold / subtitle muted; glyph does not overpower title.
- No competing accents (mode label + selection + buttons all screaming).

### L2 Density & rhythm

- Row ~44–48px; overlay width stable (~560–640 default).
- Padding/gaps on 4px grid (8/12/16 common).
- Inner controls use smaller radius than overlay shell.
- Not sparse “card dashboard”; not cramped spreadsheet.

### L3 Selection & focus

- Selected row obvious without default Windows blue.
- Caret/focus ring on query is clear; tab order sane in settings.
- Hover ≠ selected (if hover exists); keyboard selection always wins.

### L4 Scanability

- Two-line rows; ellipsis + tooltip for long paths.
- Optional: match highlighting in title (craft backlog).
- Optional: right-side accessory (`Enter`, type) without crowding subtitle.
- Icons/glyphs consistent by action kind; not random emoji.

### L5 Discoverability

- Footer (or equivalent) shows Enter / Alt+Enter / Esc (and stays accurate).
- Mode chrome readable; backspace-to-exit discoverable or hinted.
- Settings primary actions verb-led and honest about cost (`Save & rebuild`).

### L6 Empty & error copy

- Empty query: calm placeholder or recents—not a dead ListBox.
- No results: includes the query text; suggests a next step when possible.
- Index/rebuild failures: human status line, not only exception dialogs.

### L7 Settings product feel

- Same tokens/type as overlay (not “dev WinForms form”).
- Clear sections; lists don’t cause horizontal scroll.
- Destructive/heavy actions visually primary but labeled honestly.
- Async work shows progress; UI stays responsive.

### L8 Brand & tray

- Multi-size ICO readable at 16px.
- Menu copy short; tooltip `WinBox`.
- No temporary DrawString glyph as permanent identity.

### L9 Motion & feedback

- Summon/dismiss ~80–120ms; no bounce; respect reduced motion when feasible.
- Activation feedback is instant (window opens even if query still running).
- No animation tax on typing / arrowing.

### L10 Accessibility & contrast

- Text/background contrast adequate in dark and light themes.
- Keyboard-only path complete for launcher + settings critical flows.
- Tooltips available where truncation hides meaning.

## Severity → backlog mapping

| Severity | Meaning | Examples |
|----------|---------|----------|
| **P0** | Trust / usability break | Horizontal scroll, broken keyboard, unreadable contrast |
| **P1** | Feels unfinished daily | Weak selection, no teaching chrome, empty dead-ends, settings still “dev tool” |
| **P2** | Craft / delight | Match highlight, accessories, icon fidelity, microcopy, motion polish |
| **P3** | Platform depth | Mica everywhere, per-monitor DPI edge cases, full Fluent theme migration |

Stage-1 P0–P1 foundation items are mostly done; prefer **P1 leftovers + P2 craft** unless a regression appears.

## Output template (to user)

```markdown
## UI craft verdict
<1–2 sentences>

## Top fixes
1. ...
2. ...
3. ...

## Suggested next PR
Theme: <one craft theme>
In: <files/areas>
Out: <explicit non-goals>
```
