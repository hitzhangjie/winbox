---
name: winbox-onboarding
description: >-
  Zero-cost WinBox onboarding for humans and agents: verify .NET SDK, run make
  build/test/run, orient to solution layout and CONTRIBUTING. Use when starting
  work in this repo, onboarding a contributor, environment setup fails, or the
  user asks how to get started without deep C# knowledge.
---

# WinBox Onboarding (zero-cost start)

Goal: anyone with enthusiasm can start; Agent removes environment and orientation friction.

## Checklist

Copy and track:

```
Onboarding:
- [ ] Read CONTRIBUTING.md (human map)
- [ ] Confirm .NET 8 SDK (`dotnet --list-sdks`)
- [ ] `make help` works (or use README dotnet equivalents)
- [ ] `make test` green
- [ ] Optional: `make run` (demo host; empty line quits)
- [ ] Know which skill applies next (usually winbox-change-loop)
```

## Environment

1. Require **.NET 8 SDK** (see `global.json`). Runtimes alone are not enough.
2. Prefer **Makefile** targets; if `make` missing, use README `dotnet` commands.
3. Host may use `UseAppHost=false` so `dotnet run` avoids blocked native `.exe` on some Windows policies. Prefer `make run` / `dotnet run`, not double-clicking `WinBox.Host.exe` in dev. Distribution packages from `make dist` force `UseAppHost=true`.
4. Host `OutputType`: **Debug = Exe** (console for `make run` / Ctrl+C); **Release = WinExe** (quiet tray resident for `make ci` / `make dist`). Do not flip the csproj by hand when testing.

## Orientation (60-second model)

| Path | Role |
|------|------|
| `src/WinBox.Abstractions` | Contracts only |
| `src/WinBox.Host` | Compose + run plugins |
| `plugins/search/WinBox.Search` | Search capability |
| `tests/*` | Quality gate |
| `.github/workflows/ci.yml` | Remote gate |
| `.cursor/skills/` | Agent process manuals |

Dependency rule: **Host → Search → Abstractions**; Search never references Host.

## What to tell a first-time contributor

- You do not need to be a C# expert.
- Bring a clear idea; Agent + skills handle scaffolding, tests, and CI alignment.
- Definition of done is **process complete** (`make test` / PR checklist), not “code compiles on my machine only”.

## Next step

After onboarding succeeds, switch to [winbox-change-loop](../winbox-change-loop/SKILL.md) for the actual change.
