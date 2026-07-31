# ADR-0041 Increment 41D — Pause/Resume Controls and Status

## Scope

Expose the Increment 41C presentation state in the separate Desktop Runtime Host
diagnostics window.

## Controls

The stable diagnostics toolbar contains separate `Pause` and `Resume` buttons.
Their command availability is mutually exclusive through the tested view-model
commands. Stable locations avoid control movement when presentation state
changes.

Each button has an explicit automation name and explanatory tooltip.

## Status

The view model owns the operator wording:

- `Presentation: Running`
- `Presentation: Paused`

Running presentation states that updates are automatic. Paused presentation
states explicitly that diagnostic capture and bounded retention continue.

The wording describes presentation only. It does not imply that protocol,
transport, endpoint, runtime, or diagnostic publication activity is paused.

## Verification

Focused tests cover running and paused wording, property-change notification,
command bindings, visible labels, tooltips, and automation names.

Physical validation exercises active Native and Compact Serial diagnostics while
presentation is paused and resumed.
