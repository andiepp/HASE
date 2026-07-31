# ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause

- Status: Proposed
- Date: 2026-07-31

## Context

ADR-0040 and ADR-0041 established bounded process-local diagnostics for the
Desktop Runtime Host, moved their presentation into a separate modeless window,
and defined Presentation Pause/Resume without interrupting capture. Operators
also need an independently positionable diagnostics window in the WPF laptop
client.

The laptop client and Desktop Runtime Host observe different architectural
boundaries. The client owns authenticated northbound connection and session
behavior, snapshot retrieval, normalized Property and Command operations,
observation processing, and client-side recovery. It does not own or directly
observe the host's physical ESP32 TCP or Arduino serial connections.

Presenting reconstructed normalized values as captured Native or Compact wire
bytes would therefore be misleading. A client diagnostics design must preserve
the distinction between evidence captured at the northbound client boundary
and physical-protocol evidence captured locally by the runtime host.

## Decision

### Client-local diagnostic boundary

The WPF laptop client will own one bounded, process-local diagnostic session.
It may record client lifecycle and northbound activities that the client
actually observes, including:

- configuration and connection outcomes;
- authenticated session establishment and termination;
- snapshot retrieval;
- normalized Property reads and writes;
- normalized Command execution;
- observation subscription, delivery, cancellation, and failure;
- client recovery, reconnect, disconnect, and faults; and
- client-side presentation or mapping failures relevant to those operations.

Diagnostic records must not contain private keys, certificate passwords,
credentials, or unnecessary private-network addresses. Property values,
Command arguments, return values, and Event payloads remain excluded from
Operational records unless a later explicitly approved capture contract defines
their treatment.

### No implied physical-protocol visibility

Client diagnostics will not claim to contain Native Protocol Version 1 or
Compact Serial Protocol Version 1 traffic unless those exact bytes were
received at the client boundary. Normalized fields may be presented as named
semantic fields, but reconstructed data must never be labelled as captured wire
bytes.

Remote retrieval or streaming of Desktop Runtime Host diagnostics is not part
of ADR-0042. The existing host diagnostic collector, physical byte observers,
capture-level ownership, and security boundary remain unchanged.

### Separate modeless window

The laptop-client main window will expose `Open Diagnostics`. One modeless
diagnostics window exists at a time. Repeated invocation restores and activates
the existing window. Closing Diagnostics leaves the client application,
connection, observation session, and diagnostic capture running.

The diagnostic presentation session is independent of window instances so
that applicable filter, selection, paused/running state, and retained records
survive close and reopen within one application process. Closing the main
client window closes its owned diagnostics window and permits deterministic
process exit.

### Presentation pause

Pause freezes presentation reconciliation and automatic scrolling only.
Diagnostic publication, sequence assignment, bounded retention, and eviction
continue. The window shows that presentation is paused and reports the number
of captured records awaiting reconciliation.

Resume reconciles the current retained snapshot deterministically. Records
evicted while paused do not reappear. Clear remains an explicit local action
and does not affect the runtime host, physical endpoints, northbound protocol,
or client connection.

### Capture and presentation

Capture policy and display filtering remain separate concerns. Display filters
are cumulative views over retained records and never enable unavailable
evidence. Where serialized payload bytes genuinely exist at the client
boundary, raw hexadecimal display and any structured interpretation must be
derived from immutable bounded snapshots and remain read-only.

No diagnostic action may change Property, Command, Event, connection,
authentication, recovery, or physical-endpoint behavior.

## Consequences

- Laptop-client diagnostics can be positioned beside the main client UI.
- Operators can inspect a stable presentation while client capture continues.
- Client connection and northbound activity become observable without exposing
  secrets or pretending to observe physical transport traffic.
- Host and client diagnostic sessions remain independent and process-local.
- Correlating client operations with host physical diagnostics remains manual
  until a separately approved secure northbound diagnostic contract exists.
- Presentation Pause/Resume is not transport flow control and cannot delay or
  suppress observation processing.

## Planned increments

1. 42A — Decision and Client Diagnostics Boundary.
2. 42B — Client Diagnostics Capture Model.
3. 42C — Client Instrumentation.
4. 42D — Separate Diagnostics Window.
5. 42E — Presentation Pause/Resume.
6. 42F — Structured Northbound Presentation.
7. 42G — Physical Validation and Closure.

## Validation required before acceptance

- Automated tests for bounded capture, ordering, filtering, redaction,
  Pause/Resume reconciliation, clear, and window lifecycle.
- Desktop/laptop validation with authenticated connection and both physical
  endpoints published.
- Property, Command, Event, disconnect, reconnect, recovery, pause, resume,
  clear, close, and reopen behavior validated without changing client or host
  operation.
- Presentation verified to distinguish normalized northbound evidence from
  physical Native and Compact protocol evidence.

## Deferred

- secure remote retrieval or streaming of Desktop Runtime Host diagnostics;
- cross-process correlation between client and physical host records;
- persistent diagnostic storage and rotation;
- diagnostic export, import, search, and time-range filtering;
- live capture-policy changes; and
- semantic decoding of application payloads.
