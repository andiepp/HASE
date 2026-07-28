# ADR-0035 Increment 5 Fix 1 — Post-Command Authoritative Property Refresh

## Status

Implemented for validation.

## Problem

Physical validation showed that the ESP32 `Toggle Status LED` Command returned
the correct new Boolean state while the projected `Status LED Enabled` Property
remained at its previous cached value.

The Command exchange was successful. The stale display resulted from different
health-probe semantics:

- compact endpoint health probing authoritatively refreshes all mapped
  Properties; and
- native endpoint health probing verifies endpoint identity through `Discover`
  without reading Properties.

Command execution intentionally does not mutate the Property cache. Protocol
Version 1 has no unsolicited Property-change notification, and the current
Command descriptor does not declare a relationship between a return value and a
Property.

## Resolution

The Desktop Runtime Host operator boundary now exposes normalized authoritative
Property reads in addition to Property writes and Command execution.

After a successful operator Command, the Desktop console:

1. retains the captured Command endpoint identity, attachment generation, and
   instrument identity;
2. selects readable Property targets from that same generation and instrument;
3. reads each target authoritatively exactly once and sequentially;
4. refreshes the projected inventory after the reads complete; and
5. reports either the refreshed Property count or an explicit reconciliation
   warning.

The Command return value is displayed but is never assigned to a Property.
Only endpoint-confirmed Property reads update the runtime cache.

If reconciliation fails or is cancelled after the Command completed, the
Command remains `Succeeded`. The console reports the incomplete reconciliation
and performs no retry.

## Verification

Automated coverage includes:

- exact authoritative-read delegation through the operator boundary;
- no read retry after a returned or thrown failure;
- selecting the readable Property from the same endpoint generation and
  instrument;
- refreshing the projected Boolean value from the post-Command authoritative
  read; and
- retaining Command success while reporting a reconciliation warning.

No generic runtime Command semantics, native health-probe semantics, Protocol
Version 1 message, ESP32 firmware behavior, automatic retry, or inferred cache
mutation is changed.

