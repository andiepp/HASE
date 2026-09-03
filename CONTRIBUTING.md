# Contributing to HASE

HASE is a laboratory automation framework: a Runtime Host that owns physical
connections, a normalized descriptor model, a versioned northbound API, and
clients in .NET and Python. This repository is the base. It ships the
protocols, the runtime, the clients and two validation boards' firmware, and
it names no laboratory instrument; instrument families live in add-on
repositories that consume this one. Contributions are welcome within that
shape, and this page says how the repository works so that a contribution
lands the way the maintainers' own work does.

## Before you start

- Read [Getting Started](docs/Getting-Started.md) and run the simulated
  system on one PC; every later example builds on it.
- Read [Architecture](docs/Architecture.md) for the model and its
  boundaries, and skim the [ADR collection](docs/adr/) for the decisions
  behind them. A change that contradicts an accepted decision needs a new
  decision first, not a pull request.
- Open an issue before a large change. Say what you want to change, why, and
  which decision it touches; agree the shape before writing code.

## How work is done here

The repository is developed in explicit increments, each with a stated goal
and scope, a starting commit, the files it touches, its automated validation,
its physical effects, its rollback boundary, and its definition of done. A
pull request is one increment. Keep it reviewable: one purpose, the smallest
diff that achieves it, and a description that says what changed and how it
was validated.

Architectural decisions are recorded as ADRs under `docs/adr/`, numbered in
sequence, each with a status line, a date, and the baseline it started from.
When your change is a decision, propose the ADR in the pull request; when it
implements an accepted decision, name the decision and the increment.

Documentation is part of the change. `docs/ProjectStatus.md` and
`docs/Roadmap.md` record what is complete and what remains; a change that
completes or opens work updates them. Markdown wraps at 80 columns and uses
sober declarative prose.

## Validation

Focused first, then complete:

```
dotnet test tests/<Project>.Tests/<Project>.Tests.csproj -c Release
dotnet test HASE.slnx -c Release
```

The complete suite is self-contained and needs no attached hardware. Report
exact totals, failures and skips, and the warning count of a cold build
(`dotnet build HASE.slnx -c Release --no-incremental`); the accepted baselines
are stated in `CLAUDE.md`. The Python client has its own suite, run with
`python -m pytest` from `python/hase-client/` with its virtual environment
active.

Tests are contracts. A test that guards a boundary, such as the tests that
fail when an instrument name enters the base or when a project references
one, is not to be weakened to make a change pass; the change is what has to
fit the boundary.

## PowerShell tooling

Scripts under `tools/` and `python/hase-client/tools/` mutate installations
and physical state and are held to the rules in `AGENTS.md`: Windows
PowerShell 5.1 compatibility, `$ErrorActionPreference = "Stop"` and
`Set-StrictMode -Version Latest`, `@(...)` before `.Count`, explicit native
exit-code checks, and no dependence on presentation text. Every script must
parse under `System.Management.Automation.Language.Parser`, and a tooling
change carries focused tests for parsing and for its success, rejection,
failure and recovery paths. A script that must run on one computer takes
that computer's name as `-ExpectedComputer`; no computer is named in the
base.

## Adding an instrument

An instrument family is not added to the base. The base offers seams an
add-on implements: an endpoint provider registered by a derived application,
compact definitions contributed the same way, an optional client panel, and
declared command presentation. The
[SCPI Instrument Authoring Guide](docs/SCPI-Instrument-Authoring-Guide.md)
and the [ESP32 Endpoint Authoring Guide](docs/ESP32-Endpoint-Authoring-Guide.md)
describe the layers; ADR-0068 records the boundary and why it is drawn where
it is. A contribution that needs a new seam proposes the seam; a
contribution that needs a name in the base is asking for the wrong thing.

## What must never enter the repository

Credentials, certificates, private keys, addresses of any real network,
user-profile paths, and computer names. Example addresses use the ranges
reserved for documentation. The firmware's Wi-Fi credentials live in an
untracked, ignored file, `HaseESP32/HaseSecrets.h`, copied from the template
under `templates/HaseESP32/`; do not commit yours.

## Licence

HASE is licensed under the [MIT License](LICENSE). By contributing, you agree
that your contribution is licensed under the same terms.
