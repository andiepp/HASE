# ADR-0040 Increment 40D4 — Production Protocol Activation

## Scope

Activate the payload-free Native Protocol Version 1 and Compact Serial Protocol
Version 1 diagnostics introduced by Increments 40D2 and 40D3 in production
runtime endpoint composition.

No presentation changes or documentation closure are included.

## Native activation

Every operational native protocol binding is decorated with
`NativeRuntimeProtocolDiagnosticConnection` when the binding is created.
The runtime endpoint's authoritative descriptor identity and its shared
`RuntimeDiagnosticPublisher` are supplied to the decorator.

One `NativeProtocolNotificationDiagnosticObserver` is retained by the native
coordinator's existing replacement-aware notification subscription set. It is
attached to the current protocol generation, detached before that generation
is discarded, and attached once to its replacement.

The diagnostic decorator preserves notification and transport-trace
capabilities, so existing event routing and exchange statistics continue to use
their established ownership paths.

## Compact activation

After an explicitly selected Compact endpoint has completed authoritative
bootstrap, its owned operational protocol connection is replaced in place by a
`CompactRuntimeProtocolDiagnosticConnection`. The stable
`CompactEndpointConnection` identity and initialization result are preserved.

The decorator owns exactly one
`CompactProtocolNotificationDiagnosticObserver` subscription for that physical
connection generation. Disposal removes the subscription before disposing the
underlying connection. Reconnection creates a new decorated generation after
the previous generation has been detached and disposed.

Bootstrap and verification traffic remains outside operational runtime tracing.
This avoids recording discovery probes as traffic owned by an attached runtime
endpoint.

## Runtime behavior

Both production paths:

- use the authoritative runtime endpoint identity;
- publish records only when the configured sink enables Protocol diagnostics;
- retain the original response and exception behavior;
- retain transport statistics, notification delivery, and lifecycle ownership;
- record metadata and payload lengths without payload bytes, decoded values,
  exception messages, or credentials; and
- replace diagnostic subscriptions with the physical connection generation,
  preventing duplication after reconnect.

## Verification

Composition-focused tests verify:

- the Native production binding publishes request and response records;
- Native replacement produces exactly one record pair per generation;
- Compact synchronization runs through the production diagnostic decorator;
- the Compact activated decorator owns and removes exactly one notification
  subscription; and
- all focused failure, disabled-level, payload-isolation, and sink-isolation
  guarantees from Increments 40D2 and 40D3 remain applicable.

Full architecture and project-status closure remains deferred to Increment
40D5.
