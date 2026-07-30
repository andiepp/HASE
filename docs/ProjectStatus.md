# Project Status

## Current architectural work
**ADR-0040 — Structured Runtime Diagnostics and Tracing**

### Current status
- ADR-0037 Complete
- ADR-0038 Complete
- ADR-0039 Complete
- ADR-0040 Increment 40A Complete
- ADR-0040 Increment 40B Complete
- ADR-0040 Increment 40C Complete
- ADR-0040 Increment 40D Complete
- 3,855 automated tests passing
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

### Next
Implement ADR-0040 Increment 40E — bounded opt-in native and Compact byte
tracing.
