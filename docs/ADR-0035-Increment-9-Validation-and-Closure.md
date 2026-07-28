# ADR-0035 Increment 9 — Validation and Closure

## Status

Completed.

## Final baseline

```text
3,442 automated tests pass
.NET solution builds
ESP32 physical validation succeeds
Arduino Uno physical validation succeeds
Desktop Runtime Host and Laptop Client interoperability succeeds
Desktop Runtime Host process shuts down orderly
```

## Validated operator capabilities

The production Desktop Runtime Host:

- projects persistent endpoint, instrument, Property, Command, and Event
  ViewModels;
- preserves requested Boolean input independently from authoritative state;
- writes Boolean Properties explicitly through normalized services;
- executes parameterless Commands explicitly without automatic retry;
- reconciles readable Properties authoritatively after successful Commands;
- preserves attachment-generation-aware operation targets;
- projects normalized operation lifecycle and outcomes;
- retains the latest 100 completed local operator actions;
- retains the latest 100 live endpoint Event occurrences;
- attributes every Event occurrence to its own endpoint, attachment generation,
  instrument, and Event path; and
- cancels Event observation and stops the runtime process orderly.

Physical validation covered ESP32 and Arduino Uno Property writes, Commands,
post-Command state reconciliation, Event descriptors, and live
`Controller.ButtonPressed` occurrences. Alternating Arduino and ESP32 Events in
both orders retained correct source attribution.

## Deferred work

Typed Command argument entry is not implemented because the current
`CommandDescriptor` has no argument descriptor and the physical Commands are
parameterless. Typed arguments require a separate architecture decision
covering descriptor semantics, serialization, compatibility, remote contracts,
and a concrete endpoint capability.

Numeric and string Property editors, persistent audit or Event history,
filtering, export, automatic Event-subscription recovery, discovery controls,
configuration editing, and lifecycle administration also remain outside
ADR-0035.
