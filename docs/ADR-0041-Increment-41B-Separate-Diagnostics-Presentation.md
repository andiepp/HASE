# ADR-0041 Increment 41B — Separate Diagnostics Presentation

## Scope

Make the separate modeless diagnostics window the sole live diagnostics
presentation while preserving the ADR-0040 presentation contract.

## Presentation

`DiagnosticsWindow` binds directly to the singleton
`RuntimeDiagnosticsViewModel` supplied by the window service. It presents:

- immutable capture level;
- cumulative display filter;
- local clear action;
- Bytes-capture payload warning;
- displayed and retained record counts;
- newest-first record list;
- selected-record structured details; and
- bounded hexadecimal bytes.

The Desktop Runtime Host refresh timer continues to refresh the same singleton
view model whether the diagnostics window is open or closed.

The legacy embedded group is removed from the live main-window visual tree
during construction. The main window retains only `Open Diagnostics` as the
diagnostics presentation entry point.

## Preserved behavior

Moving presentation does not change capture level, bounded retention, filtering,
selection, clearing, record projection, diagnostic publication, endpoint
behavior, or northbound contracts.

Presentation pause and resume remain deferred to Increment 41C.

## Verification

Automated presentation-structure tests verify the complete diagnostics binding
surface and its direct shared-view-model boundary.
