# ADR-0035 Increment 3 — Independent Boolean Property Input

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host now distinguishes authoritative current Property state
from operator-requested Boolean input.

Property inventory snapshots add:

- a presentation-neutral Property data kind;
- descriptor-derived write capability; and
- an optional authoritative Boolean value separate from formatted display text.

The production inventory maps the current HASE data descriptors to Boolean,
numeric, string, or unknown data kinds. Write capability is derived from the
descriptor access flags.

Each persistent `DesktopRuntimePropertyViewModel` now owns:

- current Boolean state;
- independent nullable requested Boolean state;
- Boolean-editor availability; and
- a local reset command.

For a writable Boolean Property, requested state is initialized once when the
ViewModel is created. Compatible inventory refreshes update authoritative value,
quality, timestamp, and known state without changing requested state.

If data kind or write capability changes incompatibly, requested Boolean state is
reinitialized for the new descriptor shape or cleared when a Boolean editor is
no longer applicable.

The WPF Property card displays:

- the existing authoritative formatted current value;
- an independent three-state requested-value checkbox for writable Boolean
  Properties; and
- a `Reset to current` action that copies the current authoritative Boolean value
  into requested state.

Automated tests cover:

- initial writable-Boolean state;
- operator input surviving an unchanged authoritative refresh;
- operator input surviving an authoritative Boolean change;
- resetting requested state to the authoritative value;
- hiding requested input for read-only Boolean Properties; and
- clearing requested state after an incompatible data-kind change.

No Property write is executed. The ADR-0035 operator service is not invoked.
There is no automatic retry, optimistic authoritative update, numeric editor,
string editor, Command execution, activity logging, runtime contract change,
transport change, protocol change, or gRPC change.

