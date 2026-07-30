# ADR-0039 — Descriptor-Driven Event Presentation

## Status

Accepted and implemented.

## Context

HASE already transported transient Event occurrences from physical and
simulated endpoints through the runtime, normalized northbound observation
service, authenticated gRPC API, Desktop Runtime Host, and Laptop Client.
However, an `EventDescriptor` described only the Event itself. Applications
could display an occurrence value only through application-specific type
inspection or generic `ToString()` behavior.

ADR-0037 made Property editing descriptor-driven and ADR-0038 did the same for
Command arguments. Event presentation was the remaining interaction primitive
without a complete descriptor-driven application model.

## Decision

An Event descriptor may declare zero or one typed payload. Both operator
applications resolve the authoritative Event descriptor for each occurrence
and use one shared, UI-neutral formatter to produce presentation text and a
stable status.

Events remain transient occurrences. A payload descriptor does not turn an
Event into synchronized state, add replay, or change endpoint authority.

## Payload descriptor

`EventPayloadDescriptor` contains:

- a human-readable display name;
- an optional description; and
- one existing `DataDescriptor`.

`EventDescriptor.Payload` is optional. A null payload descriptor defines a
parameterless Event. The supported typed payload families are:

| Data descriptor | Normalized runtime value | Presentation |
| --- | --- | --- |
| `BooleanDataDescriptor` | `bool` | `True` or `False` |
| `NumericDataDescriptor` | finite numeric CLR value | invariant round-trip-safe text |
| `StringDataDescriptor` | `string` | exact string |
| `ByteArrayDataDescriptor` | `ByteArrayValue` | uppercase hexadecimal without separators |

The existing zero-payload Event constructor and endpoint behavior remain
compatible.

## Protocol and remote compatibility

The Protocol V1 base Event descriptor encoding is unchanged. Optional Event
payload metadata is carried by endpoint descriptor extension type `0x02`.
Readers that do not understand the extension can skip it and retain the base
Event descriptor.

The remote gRPC Event descriptor contains an optional payload descriptor at
field 4. Host and client mappers preserve the payload name, description, and
data descriptor. Occurrence values continue to use the established remote
value union.

No new occurrence message kind, transport, or Event replay mechanism is
introduced.

## Shared presentation boundary

`Hase.Operator.Presentation` owns descriptor-driven Event payload formatting.
`EventPayloadFormatter` accepts:

- an optional authoritative `EventPayloadDescriptor`; and
- an optional normalized core value.

It returns an immutable `EventPayloadFormatResult` rather than throwing for
endpoint-originated descriptor/value inconsistencies.

Stable results distinguish:

- `NoPayload`;
- `Formatted`;
- `MissingPayload`;
- `UnexpectedPayload`;
- `TypeMismatch`; and
- `UnsupportedDescriptor`.

The associated stable texts are `No payload`, `Missing payload`,
`Unexpected payload`, `Invalid payload`, and `Unsupported payload`. Valid
payloads do not display a diagnostic.

## Descriptor resolution

An occurrence is resolved by the complete authoritative identity:

- endpoint identity;
- attachment generation;
- instrument identity; and
- Event path.

Attachment generation is mandatory. An occurrence from an earlier attachment
must never acquire descriptor metadata from a replacement attachment with the
same endpoint identity.

If the exact descriptor is no longer available, the occurrence remains
visible with fallback identity and a stable presentation diagnostic.

## Application presentation

The Laptop Client:

1. resolves the Event descriptor from the attachment-specific remote snapshot;
2. normalizes the remote value union to a supported core value;
3. calls the shared formatter; and
4. stores immutable payload metadata, text, and status in its occurrence
   ViewModel.

The Desktop Runtime Host retains complete Event descriptors in its inventory,
resolves each local occurrence using the same generation-qualified identity,
and calls the same formatter.

Both applications show Event identity, payload name, payload description where
present, and formatted value. A diagnostic is shown only for an invalid or
unsupported descriptor/value combination.

Applications may retain bounded Event history for presentation. That history
is application state and is not runtime cache state or protocol replay.

## Validation endpoint

The opt-in `simulation-byte-buffer-validation` endpoint exposes deterministic
parameterless Commands that publish:

| Event payload | Value |
| --- | --- |
| none | `null` |
| Boolean | `true` |
| Numeric temperature in Celsius | `23.5` |
| String | `HASE event validation` |
| ByteArray | bytes `01 AB 00 FF` |

The executor publishes through `RuntimeEvent.PublishOccurrence`. Therefore
local and remote validation uses the production observation path without a
validation-only transport or presentation bypass.

## Consequences

- Event payload presentation is determined by descriptors rather than Event
  paths, display names, endpoints, or application-specific formatting.
- Desktop and Laptop applications use identical formatting rules.
- Parameterless Events remain compatible.
- Typed metadata remains optional at both protocol boundaries.
- Descriptor/value inconsistencies remain visible and do not crash an
  operator application.
- Event occurrence identity remains generation-qualified.
- Events remain transient and non-replayed.
- Enumeration payloads, structured payloads, multiple named payload fields,
  localized numeric formatting, and persistent Event history remain outside
  this decision.

## Implementation

1. 10A — typed Event payload descriptors and protocol mappings.
2. 10B — shared Event payload formatting.
3. 10C — Laptop Client Event presentation.
4. 10D — Desktop Runtime Host Event presentation.
5. 10E — multi-type local and remote validation.
6. 10F — documentation and closure.

## Acceptance

ADR-0039 is complete because:

- parameterless and typed Events are represented by authoritative descriptors;
- Boolean, Numeric, String, and ByteArray metadata survives Protocol V1 and
  gRPC mapping;
- both applications use the shared formatter;
- valid payloads have identical local and remote presentation;
- invalid combinations produce stable diagnostics without throwing;
- descriptor resolution includes attachment generation;
- automated coverage passes with 3,762 tests; and
- controlled Desktop Host and Laptop Client validation succeeded for every
  supported payload family.
