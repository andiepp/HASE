# ADR-0045 — Runtime-Hosted SCPI Instrument Publication

- Status: Implemented, physically validated, and closed — Increment 45J
- Date: 2026-08-03
- Closure date: 2026-08-04

## Context

ADR-0044 completed a dependency-free serialized SCPI text-session boundary and
physically validated read-only KEL-103 identification over USB virtual serial.
The KEL-103 is not yet owned, supervised, synchronized, or published by a HASE
Runtime Host.

HASE already publishes Native Protocol Version 1, Compact Serial Protocol
Version 1, and explicitly configured in-process endpoints through one normalized
endpoint, instrument, Property, Command, Event, attachment-generation, and
northbound model. A commercial SCPI instrument must reuse that model without
introducing SCPI-specific Client or remote contracts.

Only `*IDN?` is physically verified. Documented measurement or state queries
are not authoritative HASE capabilities until separately characterized.

## Decision

### Layering

`Hase.Scpi` remains transport-independent and unchanged. A reusable serial-to-
SCPI bridge adapts the existing serial byte-stream abstraction. KEL-103 command
text, parsing, identity policy, units, sentinels, and definition versioning
remain in device-specific infrastructure below the normalized runtime model.

Runtime Host composition owns connection creation, verification,
synchronization, supervision, recovery, replacement, and disposal. Runtime,
gRPC, Desktop Host presentation, and Client presentation receive only existing
normalized HASE contracts. SCPI command text and types do not cross northbound.

### Identity and configuration

The following remain distinct:

- the serial target, which is reachability only;
- the externally configured stable HASE `EndpointId`;
- the versioned KEL-103 definition reference;
- the KEL-103 identification response, which is verification evidence; and
- the attachment generation, which identifies one publication lifetime.

The external Runtime Host profile supplies explicit enablement, endpoint
identity, definition reference, and serial reachability. Configuration never
implies discovery, automatic attachment, or automatic replacement.

The per-instrument serial identity returned by `*IDN?` is not used as endpoint
identity and is not reproduced in source, profiles, ordinary diagnostics,
Client presentation, or validation reports.

### Initial read-only capability

The first definition contains one endpoint and one instrument. Initially it may
publish only physically verified, read-only identity information:

- product identity; and
- firmware version.

Candidate input-state, function, voltage, current, and power Properties require
individual read-only characterization before descriptor publication. A vendor
manual alone is insufficient evidence.

ADR-0045 initially exposes no writable Property and no Command. Input
enablement, setpoints, function selection, triggers, storage, protection,
battery, and dynamic modes remain excluded.

### Attachment lifecycle

For one explicit attachment the Runtime Host:

1. validates the external profile;
2. opens the selected serial target;
3. creates one serialized SCPI session;
4. verifies KEL-103 identity using read-only `*IDN?`;
5. synchronizes every descriptor-backed initial Property;
6. publishes only after complete successful synchronization;
7. serializes later operations through the same session;
8. invalidates the session after a desynchronizing failure;
9. replaces the complete connection and session during recovery;
10. reverifies identity and completely resynchronizes before Ready; and
11. disposes deterministically on detach or host shutdown.

Physical connection recovery follows the established endpoint-supervisor
semantics. Explicit detach and reattach create a new attachment generation.
Operations remain qualified by endpoint identity and attachment generation.

The Runtime Host is the sole owner. Protocol Explorer, vendor software, and
terminal applications must not share an open port with the host.

### Property authority

The instrument remains authoritative. Publication requires an initial value for
every declared initial Property. A partial synchronization failure prevents
publication. Authoritative reads execute a new bounded SCPI query. Cached values
and connection state retain their existing normalized meanings.

No query is retried automatically inside an operation. Recovery creates a new
session, revalidates identity, and resynchronizes the complete published model.

### Diagnostics

The first implementation emits sanitized Operational diagnostics for lifecycle,
verification, synchronization, read category, duration, and bounded outcome.

Diagnostics must not contain serial targets, raw identification responses,
instrument serial identity, SCPI command text, Property values, credentials,
configuration paths, or exception text.

SCPI Protocol and Bytes diagnostics remain deferred until a separate
sanitization and presentation policy is approved. Existing Native and Compact
byte interpreters are not reused for arbitrary SCPI text.

### Read-only characterization gate

Every additional candidate query is first executed through a narrowly bounded
Protocol Explorer scenario. Validation must establish exact request text,
terminators, response syntax, units, sentinels, latency, maximum size, error
behavior, and absence of state mutation. Only successful candidates may enter a
versioned descriptor.

## Consequences

- A commercial instrument can join the existing runtime and northbound model
  without changing Client or gRPC contracts.
- Explicit definitions replace unavailable machine-readable discovery.
- Publication cannot precede verification and complete synchronization.
- Faulted SCPI sessions are replaced rather than reused.
- Read-only capability growth is deliberately gated by physical evidence.
- The first useful runtime slice is narrow but safe and independently testable.
- Serial exclusivity, parsing, polling load, and recovery require device-specific
  validation.

## Approved increment sequence

1. 45A — Decision and read-only safety boundary.
2. 45B — Reusable serial-to-SCPI bridge.
3. 45C — Versioned KEL-103 identity definition.
4. 45D — Read-only measurement characterization.
5. 45E — Normalized KEL-103 runtime adapter.
6. 45F — Attachment, supervision, and synchronization.
7. 45G — External Runtime Host profile integration.
8. 45H — Desktop Host and Client presentation validation.
9. 45I — Physical recovery and multi-host validation.
10. 45J — Documentation and closure.

Every increment requires explicit approval and remains independently buildable
and testable.

## Validation strategy

Automated validation covers strict configuration, definition versioning,
descriptor stability, invariant parsing, redaction, verification rejection,
complete synchronization, readiness-gated publication, session replacement,
stale-generation rejection, deterministic disposal, and diagnostic
sanitization.

Physical validation proceeds from isolated read-only query characterization to
identity-only Runtime Host attachment, verified measurements, authoritative
Client reads, unplug/reconnect, reset or power-cycle recovery, simultaneous
multi-host regression, orderly shutdown, and independent port reopening.

## Deferred

- all state-changing KEL-103 operations;
- arbitrary operator-entered SCPI;
- automatic discovery or attachment;
- generic VISA, USBTMC, or GPIB;
- SCPI Protocol and Bytes diagnostics;
- Python automation;
- diagnostic export and offline analysis; and
- remote media feedback.

## Baseline

ADR-0045 starts from the clean ADR-0044 closure baseline of 4,515 automated
tests. The Runtime Hosts and Client are stopped. Machine-specific reachability
and instrument identity remain external deployment data.

## Implemented result

ADR-0045 adds a reusable serial-to-SCPI bridge, versioned KEL-103 definitions,
strict invariant response parsing, a normalized read-only runtime adapter,
readiness-gated attachment, supervised connection replacement, explicit
external endpoint composition, authoritative inventory publication, and
unchanged Desktop Host, Client, and gRPC presentation boundaries.

The production KEL-103 definition publishes one electronic-load instrument and
exactly five read-only Properties:

- product identity;
- firmware version;
- measured voltage;
- measured current; and
- measured power.

There is no writable Property, Command, arbitrary SCPI console, automatic
discovery, or automatic replacement of an existing attachment. The configured
serial target remains reachability only. The configured HASE endpoint identity
and attachment generation remain authoritative northbound identity.

Explicit offline profile administration adds and removes KEL-103 entries using
atomic replacement, timestamped backup, exact endpoint-identity confirmation
for removal, the fixed supported definition, and the physically verified serial
profile. It performs no port access or Runtime Host startup and does not print
the serial target.

## Physical validation and closure

Physical validation confirmed:

- production publication beside the existing Desktop Arduino and ESP32;
- complete identity and measurement synchronization before `Ready`;
- correct Host and Client presentation of five read-only Properties;
- successful authoritative reads with `Good` quality and advancing UTC
  timestamps;
- no writable Property or Command exposure;
- bounded operation-triggered detection of USB loss and instrument power loss;
- supervised serial/SCPI connection replacement, identity reverification, and
  complete resynchronization;
- stable attachment generation across USB reconnect and instrument power-cycle
  recovery;
- unaffected Arduino and ESP32 Properties, Commands, and Events;
- correct endpoint and Runtime Host scoping in simultaneous Desktop and MiniPC
  sessions;
- independent Client-session disconnect and reconnect;
- sanitized Host and Client Operational diagnostics; and
- orderly shutdown followed by immediate independent serial-port reopening and
  redacted read-only identity verification.

The final physical topology contained three endpoints on the Desktop Runtime
Host (Arduino, ESP32, and KEL-103), one Arduino on the MiniPC Runtime Host, and
both authenticated Runtime Host profiles in the Laptop Client. No
machine-specific address, serial target, certificate identity, credential, or
instrument serial identity is part of this record.

ADR-0045 closes at 4,772 automated tests passing in Visual Studio 2026 Release
configuration on .NET 10.
