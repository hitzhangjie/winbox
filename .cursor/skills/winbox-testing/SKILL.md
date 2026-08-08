---
name: winbox-testing
description: >-
  WinBox testing and CI standards: what must be covered, xUnit patterns, make
  test/ci gates, and how agents fix failures. Use when writing tests, changing
  behavior, debugging CI, or verifying a change is done.
---

# WinBox Testing

Tests are the quality backstop so contributors need not be language experts: **if tests pass and process was followed, the change is shippable**.

## Gates

| Gate | Command | When |
|------|---------|------|
| Local fast | `make test` | After every behavior change |
| Local CI-parity | `make ci` | Before PR / release claims |
| Remote | GitHub Actions `CI` workflow | On push/PR |

Never mark work complete if `make test` fails.

## What must have tests

Add or update tests when you change:

- Ranking / matching behavior
- Index upsert / dedupe / case rules
- Plugin start/stop and “not started” guards
- PluginRegistry registration rules
- Any new public method on Abstractions implementers

Skip new tests only for pure docs/chore with no runtime impact (say so explicitly).

## Project map

| Area | Test project |
|------|----------------|
| Search index/query/plugin | `tests/WinBox.Search.Tests` |
| Host registry / composition | `tests/WinBox.Host.Tests` |

Framework: **xUnit** (`[Fact]`, `[Theory]` as needed).

## Authoring rules

1. Name tests by behavior: `Search_PrefersFileNamePrefixMatch`.
2. Arrange–Act–Assert; one main behavior per test.
3. Prefer deterministic in-memory fixtures; no real whole-disk scans in unit tests.
4. For Windows path quirks, cover case-insensitive path identity when relevant.
5. Do not assert on incidental formatting unless that formatting is the product.

## Failure loop

```
test fail → read assertion/message → fix code or test (not by deleting coverage) → make test → only then continue
```

If CI fails but local passes: run `make ci` (Release). Check `global.json` SDK roll-forward and `nuget.config`.

## Coverage expectations (practical)

- New branch logic: at least one happy path + one edge (empty query, duplicate register, not-started, limit).
- Do not chase vanity %; chase **regression locks** for search relevance and plugin lifecycle.

## Related

- Change process: [winbox-change-loop](../winbox-change-loop/SKILL.md)
- PR/CI narrative: [winbox-ship](../winbox-ship/SKILL.md)
