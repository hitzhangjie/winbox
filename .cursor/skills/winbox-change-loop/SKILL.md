---
name: winbox-change-loop
description: >-
  Mandatory WinBox change workflow: clarify intent, place code correctly, implement
  minimally, add tests, verify with make test/ci, prepare ship notes. Use for any
  feature, bugfix, refactor, protocol change, or when the user asks to implement
  something in this repository.
---

# WinBox Change Loop (mandatory)

This is the default process for **every** substantive change. Creativity is welcome; skipping gates is not.

## Progress tracker

```
Change loop:
- [ ] 1. Intent — one sentence outcome + non-goals
- [ ] 2. Placement — winbox-architecture decision
- [ ] 3. Contract — change Abstractions only if behavior is cross-cutting
- [ ] 4. Implement — smallest diff that achieves intent
- [ ] 5. Tests — winbox-testing (new behavior needs coverage)
- [ ] 6. Verify — `make test` (ship-critical: `make ci`)
- [ ] 7. Ship notes — winbox-ship checklist if PR/release
```

## Step details

### 1. Intent

Write (internally or to the user):

- **Outcome**: what a user/dev can do after merge
- **Non-goals**: what this change deliberately skips
- **Risk**: API break? indexer perf? Windows-only assumptions?

If intent is vague, ask **one** clarifying question before coding.

### 2. Placement

Read [winbox-architecture](../winbox-architecture/SKILL.md). Wrong folder is a process failure even if tests pass.

If the change is user-visible UI (overlay, tray, settings, result presentation, scroll/theme), also read [winbox-ui](../winbox-ui/SKILL.md) before implementing.

### 3. Contract

- Prefer extending implementations under `plugins/` or Host helpers.
- Touch `WinBox.Abstractions` only when Host and plugins must share a new capability.
- Any Abstractions change ⇒ update all implementers **and** tests in the same change set.

### 4. Implement

- Match existing style; no drive-by refactors.
- No secrets in repo; no unrelated doc churn unless process docs must stay in sync.
- Keep plugins ignorant of Host types.

### 5–6. Tests & verify

Follow [winbox-testing](../winbox-testing/SKILL.md).

Hard gate:

```bash
make test
```

Before claiming ready to merge / release:

```bash
make ci
```

If verification fails: fix or revert; do not “leave for later” on the same PR narrative.

### 7. Ship

Follow [winbox-ship](../winbox-ship/SKILL.md) when opening/updating a PR or cutting a release.

## Anti-patterns

- “Just a small fix” without `make test`
- New public behavior with zero tests
- Plugin project referencing `WinBox.Host`
- Expanding into PowerToys-scale features in one PR
- Updating README slogans without updating tests/CI when behavior changes
