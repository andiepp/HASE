# ADR-0043 — Increment 43B4C — Explicit Single-Instance Runtime Host Protection

## Status

Approved, implemented, and physically validated on 2026-08-01.

## Decision

The Desktop Runtime Host acquires the Windows session-local named mutex
`Local\HASE.DesktopRuntimeHost` before Prism creates the shell, parses deployment
profiles, constructs the production backend, discovers endpoints, opens serial
ports, or starts the private-network API.

When the mutex is already owned, the second process displays a concise
already-running message and exits. The existing process and its endpoint,
diagnostic, client, and identity state remain unaffected.

The lease is retained for the complete application lifetime and released after
the asynchronous main-window shutdown has stopped and disposed the Runtime Host.
Windows releases ownership automatically if the process terminates abnormally.

This increment deliberately reports and exits instead of discovering and
activating the existing process window. Window activation remains optional
future usability work and is not required for exclusive endpoint ownership.

## Validation

The automated baseline completed with 4,190 passing tests. The lease tests
confirmed first acquisition, deterministic same-process duplicate rejection,
independent names, release, and reacquisition.

Physical Release validation confirmed:

- the first shortcut launch published both configured endpoints as Ready;
- a second shortcut launch displayed the already-running message before
  creating another Runtime Host window or competing for endpoint ownership;
- the original Runtime Host remained healthy;
- Task Manager showed exactly one Runtime Host process;
- orderly closure terminated that process and released the mutex;
- a later shortcut launch acquired ownership and published both endpoints; and
- final orderly closure again terminated the process.
