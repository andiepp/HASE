# HASE — Claude Code Working Notes

@AGENTS.md

The rules imported above from `AGENTS.md` are mandatory for every change in this
repository. This file adds mechanics only. It does not modify, relax, or
reinterpret those rules. Where the two disagree, `AGENTS.md` is authoritative.

## Process

The public GitHub repository `andiepp/HASE` is the authoritative source.

- Reconstruct state from `origin/main`, per `AGENTS.md` §1. Start every
  increment with `git fetch origin main` and record `HEAD`, `origin/main`,
  branch status, and working-tree status.
- Work locally. Push only after the change has proven successful: focused and
  complete tests pass, the final diff is reviewed, and the changed-path set
  matches the approved scope.
- After pushing, synchronize every computer of the operator's estate from
  `origin/main` and confirm each is clean, per `AGENTS.md` §3 and §8. The
  computers, their roles, and which tool runs where are named in the
  operator's own operating notes, not here; tools that must run on one
  computer take its name as `-ExpectedComputer`.
- Physical validation is separate and always explicitly approved.

Do not stage, commit, or push without explicit per-increment approval.

## Repository layout

- `src/` — 34 .NET projects: runtime, protocol, transport, northbound gRPC,
  WPF client, SCPI, simulation, diagnostics export and offline analysis,
  deployment tools.
- `tests/` — 28 xUnit projects mirroring `src/`, plus `tests/Arduino/`
  (PowerShell-driven endpoint validation, not a .NET test project).
- `docs/adr/` — the authoritative ADR set, including per-increment files.
- `docs/ProjectStatus.md`, `docs/Roadmap.md` — closure state per §8.
- `tools/` — operational PowerShell for Arduino/ESP32, Deployment, and
  PrivateNetwork. Treat every script here as physical-mutation tooling.
- `python/hase-client/` — asyncio Python client, pytest, setuptools.
- `libraries/`, `templates/`, `HaseESP32/`, and
  `HaseArduinoUno/` — endpoint firmware sources.

Solution file is `HASE.slnx`. Target framework is `net10.0` throughout, except
six WPF/Windows projects on `net10.0-windows`. Installed SDK: .NET 10.0.301.

## Build and test

Focused first, then complete, per `AGENTS.md` §7.

```
dotnet test tests/Hase.Core.Tests/Hase.Core.Tests.csproj -c Release
dotnet test tests/Hase.Core.Tests/Hase.Core.Tests.csproj -c Release --filter "FullyQualifiedName~SomeClass"
dotnet test HASE.slnx -c Release
dotnet build HASE.slnx -c Release
```

Python client, from `python/hase-client/` with the local `.venv` active:

```
python -m pytest
```

Notes:

- The complete suite takes approximately 100 seconds across 28 test projects.
  Running it unprompted is authorized.
- Expected result: 5,817 passed, 0 failed, 0 skipped. Report exact totals.
- 63 warnings is the accepted baseline for a successful complete build. Report
  the count and any drift from it. Warnings appear only on a cold build.
- No test carries a `Skip` or hardware `Trait`. The complete suite is
  self-contained and needs no attached hardware.

## Autonomy boundary

Without asking: read, search, and analyse the repository; run read-only Git
inspection; build and run tests; write and modify source files in the working
tree.

Only with explicit per-increment approval: `git add`, `git commit`, `git push`,
or any history mutation; any script under `tools/`; any deployment, install,
publish, credential, certificate, ACL, or enrollment operation; anything
touching physical hardware or another computer; deleting retained evidence.

A repository change never implies permission to deploy. A passing test run
never implies permission to commit.

## Conventions

- Markdown wraps at 80 columns and uses sober declarative prose. Match it.
- PowerShell follows §5: `$ErrorActionPreference = "Stop"`, `Set-StrictMode
  -Version Latest`, `@(...)` before `.Count`, explicit native exit-code checks,
  ASCII control tokens in validation.
- Validate semantics, not counts or presentation text. When checking a
  changed-path set, compare the actual set and name the offending path. Use
  `git diff --cached --name-only --no-renames`, because rename detection
  collapses a moved file into a single entry.
- Never print certificate contents, private keys, secrets, or remote addresses.
  `HaseESP32/HaseSecrets.h` is untracked and must never be read aloud,
  echoed, or committed.
- End every handoff with the exact current baseline and the single next
  authorized action.
