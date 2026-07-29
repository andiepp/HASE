# ADR-0037 — Descriptor-Driven Property Editing

## Status

Accepted, implemented, remotely validated, and closed.

## Context

The Laptop Client and Desktop Runtime Host originally exposed explicit Boolean
Property writes. ADR-0036 added ByteArray values and typed Command arguments,
but application Property editing still depended on type-specific UI code.
Independent parsers would allow local and remote applications to accept
different syntax for the same descriptor.

## Decision

HASE uses one UI-neutral typed-input boundary in `Hase.Operator.Input`.
`PropertyInputParser` accepts an authoritative `PropertyDescriptor` and user
text and returns either a normalized typed value or a stable validation
failure. It performs no Property operation and depends on neither WPF, gRPC,
nor runtime-host services.

Supported mappings are:

| Descriptor | Normalized value | Editor |
| --- | --- | --- |
| `BooleanDataDescriptor` | `bool` | Check box |
| `NumericDataDescriptor` | finite invariant `double` | Text |
| `StringDataDescriptor` | exact `string` | Text |
| `ByteArrayDataDescriptor` | `ByteArrayValue` | Hexadecimal text |

Numeric values use `.` as decimal separator, may use exponent notation, must be
finite, and must satisfy `ValueRange`. Resolution does not define application
step size. Strings preserve empty and whitespace-only values. ByteArray uses
the ADR-0036 whitespace-insensitive hexadecimal syntax and requires at least
one complete byte.

Read-only and unsupported descriptors have no editor. Expected invalid input
does not throw and never invokes an operation.

## Application paths

The Laptop Client converts a successful result to `RemoteValue` and performs
the existing generation-qualified gRPC write. The Desktop Runtime Host passes
the typed value to its local normalized operator service. Neither application
owns endpoint connections.

Requested editor state is independent of authoritative state. Observation,
read, or write confirmation updates the displayed value without overwriting
requested input. Reset copies the authoritative value into the editor and does
not write. Writes remain explicit, single-shot, endpoint-confirmed, and never
automatically retried.

Confirmed Laptop reads survive observation reprojection while the exact
attachment generation remains published. They are discarded when that
attachment disappears.

## Validation simulation

The opt-in `simulation-byte-buffer-validation` endpoint exposes instrument
`byte-buffer-01` with four writable Properties:

| Property | Type | Initial value | Constraint |
| --- | --- | --- | --- |
| `Editor.Enabled` | Boolean | `False` | none |
| `Editor.Setpoint` | Numeric | `20` | `-40..125 °C` |
| `Editor.Label` | String | `HASE` | none |
| `Buffer.Value` | ByteArray | empty | none |

`Buffer.Replace` remains a typed ByteArray Command and updates the same
`Buffer.Value` Property.

## Consequences

- Local and remote editors have identical input semantics.
- Runtime validation remains authoritative; client validation is an early
  usability and safety boundary.
- No protocol-version change is required.
- Richer controls such as enumerations, sliders, spinners, and
  resolution-based stepping remain separate work.

## Implementation

1. 9A — shared typed Property input semantics.
2. 9B — Laptop Client typed Property editors.
3. 9C — Desktop Runtime Host typed Property editors.
4. 9D — writable multi-type validation simulation.
5. 9E — local and remote end-to-end validation.
6. 9F — documentation and closure.

## Acceptance

The final baseline is 3,643 automated tests passing. Boolean, Numeric, String,
and ByteArray writes were validated locally and remotely. Invalid input stayed
local; numeric boundaries were inclusive; empty and whitespace Strings were
preserved; ByteArray values remained exact; observations propagated in both
directions; `Buffer.Replace` updated the shared Property; and reconnect
restored authoritative state automatically.

