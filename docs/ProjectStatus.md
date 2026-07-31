# Project Status

## Current architectural work
**ADR-0041 — Desktop Diagnostics Window and Presentation Pause — Complete**

### Current status
- ADR-0037 Complete
- ADR-0038 Complete
- ADR-0039 Complete
- ADR-0040 Increment 40A Complete
- ADR-0040 Increment 40B Complete
- ADR-0040 Increment 40C Complete
- ADR-0040 Increment 40D Complete
- ADR-0040 Increment 40E Complete
- ADR-0040 Increment 40F Complete
- ADR-0040 Increment 40G Complete
- ADR-0040 accepted, physically validated, and closed
- ADR-0040 closure baseline: 3,913 automated tests passing
- Structured operational diagnostic foundation implemented
- Native and Compact Serial lifecycle diagnostics implemented
- Recovery scheduling diagnostics active for both physical endpoint families
- Northbound attachment publication and ending diagnostics include
  authoritative attachment generation
- Authoritative Property reads and writes publish correlated operational
  diagnostics without values
- Normalized Command execution publishes correlated operational diagnostics
  without arguments or return values
- Runtime Event occurrences publish one payload-free diagnostic before
  observer fan-out
- Native and Compact operational connections publish payload-free Protocol
  request, response, failure, and notification diagnostics
- Protocol diagnostics preserve authoritative endpoint identity, correlation,
  reconnect ownership, existing results, exceptions, and transport statistics
- Compact discovery, verification, and bootstrap traffic remains outside
  attached-runtime Protocol tracing
- Native and Compact operational generations publish bounded exact-frame
  diagnostics only through explicit local `Bytes` enablement
- Byte snapshots retain at most 256 bytes with original length and truncation
  metadata
- Default, Operational-only, and Protocol-only configurations install no
  production byte observer
- Connection replacement and disposal remove generation-owned byte observers
  before transport teardown
- Desktop Runtime Host owns one bounded 2,000-record diagnostic session per
  production start
- Desktop diagnostics present deterministic structured metadata and bounded
  hexadecimal bytes through a scroll-safe master/detail panel
- Capture level is selected locally before startup; cumulative display
  filtering never changes capture or discards retained records
- Local clearing affects only the process-local collector
- Bytes capture displays an explicit application-payload warning
- Physical ESP32 Native Protocol Version 1 diagnostics validated at
  Operational, Protocol, and Bytes display levels
- Physical Arduino Uno Compact Serial Protocol Version 1 diagnostics validated
  at Operational, Protocol, and Bytes display levels
- Property, Command, Event, clear, disconnect, reconnect, recovery, and
  post-recovery stability behavior validated through the Desktop Runtime Host
  and WPF client
- Both physical endpoints returned to `Ready` and remained operational after
  recovery
- Capture level remained startup-owned and independent of cumulative display
  filtering throughout physical validation

- ADR-0041 accepted and complete
- Desktop diagnostics moved into a separate, modeless, single-instance window
- Open Diagnostics restores or activates the existing window
- Closing Diagnostics does not stop the runtime host; main-host shutdown closes
  the owned diagnostics window
- Presentation Pause/Resume freezes only projected records while capture and
  bounded retention continue
- Paused/running state, filter, records, and selection survive window reopen;
  application restart begins in Running state
- Raw hexadecimal bytes remain visible
- Native Protocol V1 byte records expose version, role, type, correlation,
  payload length, and payload boundaries
- Compact Serial Protocol V1 byte records expose marker, version, type,
  correlation, payload length, payload, and CRC-16/CCITT-FALSE validity
- Structured interpretation is read-only and cannot affect protocol, transport,
  runtime, or endpoint behavior
- 3,981 automated tests passing
- ADR-0041 physical validation completed across Operational, Protocol, and Bytes
  capture levels for ESP32 and Arduino Uno
- Final structured-byte validation completed for Native and Compact request,
  response, and notification records

### Next
Select the next architectural objective after ADR-0041.
