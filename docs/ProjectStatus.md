# Project Status

## Current architectural work
**ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause — Complete**

### Current status
- ADR-0042 accepted, physically validated, and complete
- ADR-0042 closure baseline: 4,017 automated tests passing
- All thirty-seven combined physical validation checks passed without
  deviation with ESP32 and Arduino Uno published concurrently
- ADR-0042 Increments 42A through 42E accepted
- ADR-0042 42E baseline: 4,012 automated tests passing
- ADR-0042 Increment 42F Structured Northbound Presentation implemented
- The laptop collector captures cumulatively through Protocol level
- Observe subscription, initial snapshot, later observation, Property read and
  write, Command execution, and Event delivery publish structured Protocol
  request, response, completion, cancellation, and failure records
- Protocol records share operation correlation with their Operational activity
- Protocol fields include direction, target identity, descriptor path,
  observation sequence and kind, normalized result status, duration, and
  outcome without payload values
- Property values and requested values, Command arguments and return values,
  Event payloads, exception messages, paths, addresses, credentials, and
  certificate material remain excluded
- Level filtering is cumulative: Protocol includes Operational and Protocol;
  Bytes includes every captured level
- Selecting Bytes displays an explicit explanation that exact gRPC, HTTP/2,
  TLS, and transport bytes are unavailable at the client application boundary
- No reconstructed value is labelled or presented as captured wire bytes
- Five focused tests cover Observe correlation, Property and Command structured
  targets and redaction, cumulative filtering, and Bytes-unavailable behavior

### Next
Select the next architectural objective after ADR-0042.
