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

## Release & distribution

Supported product target today: **Windows 11 amd64** (`win-x64` self-contained zip).

| Track | Expectation |
|-------|-------------|
| Local package | `make dist` (optional `VERSION=x.y.z`) → `artifacts/dist/WinBox-<ver>-win-x64.zip` |
| Script | `scripts/dist.ps1` — `dotnet publish` self-contained + zip; forces `UseAppHost=true` |
| Version | Tag `v*` (strip leading `v`) > `-Version` / `WINBOX_VERSION` > `Directory.Build.props` `<Version>` |
| Tag push | `.github/workflows/dist.yml` verifies (`build`+`test`) then packages; uploads workflow artifact |
| GitHub Release | Same workflow on `release: published` re-packages and **uploads zip as release assets** |
| Channels | CI → tag dist check → GitHub Release zip → (later) winget / signed installer |
| Signing | Dev may use `UseAppHost=false`; release zip ships native `WinBox.Host.exe` (Authenticode still TODO) |
| Notes | Every release lists user-visible search/host changes + upgrade steps (unzip → run exe) |

`make ci` remains the authenticity gate inside Dist before publish. Do not commit `artifacts/` or bins.

## Merge rules of thumb

- Prefer small PRs that finish the change loop over epic branches.
- Protocol breaks need explicit callout in PR title/body.
- “Works on my machine” is not a gate; `make ci` is.

## Related

- [winbox-change-loop](../winbox-change-loop/SKILL.md)
- [winbox-testing](../winbox-testing/SKILL.md)
