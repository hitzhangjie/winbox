---
name: winbox-ship
description: >-
  WinBox PR review, merge readiness, release and distribution checklist. Use when
  opening or reviewing a pull request, preparing a release, packaging, or asking
  whether a change is ready to ship.
---

# WinBox Ship (PR → release → distribute)

Shipping is part of quality, not an afterthought. Agents help contributors clear gates; humans approve product intent.

## PR readiness checklist

```
PR:
- [ ] Intent stated (outcome + non-goals)
- [ ] Dependency law intact (winbox-architecture)
- [ ] Tests added/updated for behavior (winbox-testing)
- [ ] `make test` green locally
- [ ] `make ci` green for merge-worthy changes
- [ ] No secrets / credential files
- [ ] Docs updated if contributor-facing process or public behavior changed
- [ ] Diff scoped — no unrelated refactors
```

### PR summary template

```markdown
## Summary
- Why this change exists (user/dev outcome)

## Test plan
- [ ] `make test`
- [ ] `make ci` (if merge-ready)
- [ ] Manual: `make run` scenario (if user-visible)

## Risk / rollback
- API/protocol impact:
- Rollback: revert PR / disable plugin registration
```

## Review posture (agents reviewing)

Prioritize:

1. **Correctness & regressions** (search ranking, lifecycle)
2. **Architecture law** (no plugin→Host)
3. **Tests as specification**
4. **Operability** (clear failure modes, no silent catch-all)

Feedback labels:

- **Blocking**: must fix before merge
- **Should**: strong recommendation
- **Nit**: optional polish

Do not demand C# idiom perfection if behavior + tests + boundaries are sound.

## Release & distribution (forward-looking)

Even before packaging exists, keep changes releasable:

| Track | Expectation |
|-------|-------------|
| Version story | Plugin `Version` + eventual Host version stay coherent |
| Artifacts | Prefer `dotnet publish` / CI artifacts over checking bins into git |
| Channels | Design for: CI build → GitHub Release → (later) installer/winget |
| Signing | Dev may use `UseAppHost=false`; release builds should plan Authenticode when distributing `.exe` |
| Notes | Every release lists user-visible search/host changes + upgrade steps |

When adding release automation later: put workflows under `.github/workflows/`, keep `make ci` as the authenticity gate before publish jobs.

## Merge rules of thumb

- Prefer small PRs that finish the change loop over epic branches.
- Protocol breaks need explicit callout in PR title/body.
- “Works on my machine” is not a gate; `make ci` is.

## Related

- [winbox-change-loop](../winbox-change-loop/SKILL.md)
- [winbox-testing](../winbox-testing/SKILL.md)
