---
name: winbox-architecture
description: >-
  WinBox architecture guardrails: project boundaries, dependency direction,
  where to put index/query/host/UI code, and how to add plugins without breaking
  the small-core model. Use when adding files or projects, changing references,
  designing features, or debating Host vs plugin responsibilities.
---

# WinBox Architecture Guardrails

Principle: **small core, large plugins**. Host composes; plugins deliver capability; Abstractions stabilize the contract.

## Dependency law (non-negotiable)

```text
WinBox.Host ──► WinBox.Search ──► WinBox.Abstractions
     │               ▲
     └─► WinBox.Toolbox ──┘
```

Allowed:

- Host → Abstractions, Host → plugin projects
- Plugin → Abstractions
- Tests → projects under test

Forbidden:

- Plugin → Host
- Abstractions → Host or plugins
- Circular project references

## Where code goes

| Change type | Put it here |
|-------------|-------------|
| Shared plugin lifecycle / search / query-handler shapes | `src/WinBox.Abstractions` |
| Register/start/stop plugins, query router, launcher shell | `src/WinBox.Host` |
| Path storage, scanning, incremental index | `plugins/search/WinBox.Search/Index` |
| Matching, ranking, filters | `plugins/search/WinBox.Search/Query` |
| Search plugin façade implementing contracts | `plugins/search/WinBox.Search/SearchPlugin.cs` |
| Calculator / shell / web-prefix / AI launcher handlers | `plugins/toolbox/WinBox.Toolbox` |
| Launcher overlay, tray, settings chrome, theme tokens, result *row templates* | `src/WinBox.Host/Ui/` — Host owns shell; follow `winbox-ui` |
| Open/Save dialog assist (detect, docked strip, path fill) | `src/WinBox.Host/Ui/DialogAssist/` — Host owns Win32/focus; queries via `ISearchService` only (not QueryRouter) |
| Future search-specific copy/status helpers | prefer `plugins/search/.../Ui` helpers only — keep Host shell thin; plugins must not ship a second launcher window |
| Automated checks | `tests/WinBox.*.Tests` |

## Adding a new plugin (template)

1. Create `plugins/<name>/WinBox.<Name>/` class library `net8.0`.
2. Reference **only** `WinBox.Abstractions`.
3. Implement `IWinBoxPlugin` (+ capability interfaces as needed).
4. Register from Host (today: explicit `Register`; later: discovery).
5. Add `tests/WinBox.<Name>.Tests` with start/stop + capability smoke tests.
6. Add project to `WinBox.sln` and document in README structure if user-facing.

## Abstractions change policy

Treat interface edits as **semver-sensitive** even before 1.0:

- Additive optional members / new interfaces: preferred
- Breaking signature changes: require migration notes in PR + updating all call sites/tests same PR
- Do not sneak Host-only types into Abstractions

## Design defaults (stage 0–1)

- In-process plugins first; process isolation later
- Search MVP: correctness and testability over clever indexes
- Windows-first APIs OK; isolate P/Invoke behind small facades for testability
- Avoid premature multi-repo or package sprawl

## Search indexing defaults (aligned)

Canonical write-up: `plugins/search/README.md`. Agents implementing scan/index/incremental must follow it.

- **Scope**: roots + extensions + optional path/ext allow/deny lists (capability required; deny wins)
- **P1 index**: filename metadata only (`FullPath`, `FileName`, `Extension`, optional mtime/size); extension is a field, not a second index
- **P1 query**: name-first substring/prefix; path secondary; no default full-text
- **Full-text / title-summary**: optional later layers (`ContentIndex`), off by default
- **Incremental**: cold full scan → runtime USN (primary) or `FileSystemWatcher` (interim on configured roots) → reconcile / rebuild on journal loss
- **Symlinks/junctions**: do not follow in MVP
- Put scanner policy, persistence, and watchers under `plugins/search/WinBox.Search/Index/`

## Decision test

Before merging structure changes, answer:

1. Can Host still treat this as a replaceable plugin?
2. Can we unit-test the core logic without UI?
3. Did we preserve dependency law?
