# ADR-0038 — Descriptor-Driven Command Argument Editing

## Status

Accepted for implementation.

## Context

ADR-0036 introduced typed Command arguments and complete local, protocol, gRPC,
and client mapping for Boolean, Numeric, String, and ByteArray values. ADR-0037
then established shared descriptor-driven Property input semantics in
`Hase.Operator.Input` and equivalent typed editors in the Laptop Client and
Desktop Runtime Host.

Command execution still has application-specific presentation. Parameterless
Commands are directly executable, while typed Command arguments require
Command-specific editor logic. Independent local and remote implementations
could therefore accept different syntax or validation behavior for the same
Command descriptor.

## Decision

HASE will use descriptor-driven Command argument editing in both WPF
applications. A Command's authoritative descriptor determines whether an
argument editor exists, which editor is shown, how input is parsed, and whether
execution may begin.

ADR-0038 is limited to the existing protocol model of zero or one Command
argument. Multiple named arguments, argument grouping, optional argument lists,
and structured editors are outside this decision.

A Command without an argument descriptor remains immediately executable when
its endpoint is ready and no execution is already active. A Command with an
argument descriptor is executable only after local conversion succeeds.

## Shared input boundary

`Hase.Operator.Input` remains the UI-neutral application input boundary. The
implementation will introduce Command-specific parsing that reuses the same
low-level typed conversion semantics as Property editing without manufacturing
a Property descriptor or applying Property access rules.

The supported mappings are:

| Argument descriptor | Normalized value | Editor |
| --- | --- | --- |
| no descriptor | `null` | none |
| `BooleanDataDescriptor` | `bool` | check box |
| `NumericDataDescriptor` | finite invariant `double` | text |
| `StringDataDescriptor` | exact `string` | text |
| `ByteArrayDataDescriptor` | `ByteArrayValue` | hexadecimal text |

Numeric input uses `.` as decimal separator, may use exponent notation, must be
finite, and must satisfy `ValueRange`. Resolution does not define an application
step size. String input preserves empty and whitespace-only values. ByteArray
input uses the ADR-0036 whitespace-insensitive hexadecimal syntax and requires
complete bytes.

Unsupported descriptors have no executable editor. Expected invalid input does
not throw and never invokes a local operation or remote RPC.

## Application paths

The Laptop Client converts a successful argument to `RemoteValue` and performs
the existing generation-qualified authenticated gRPC Command execution. The
Desktop Runtime Host passes the normalized typed argument to its existing local
operator service.

Neither application parses user text after execution begins. Runtime and
endpoint validation remain authoritative.

Argument editor state is local application state. Command observations,
Property updates, inventory reprojection, and periodic refresh must not replace
partially entered argument text. Changing the selected Command creates editor
state from the newly selected authoritative descriptor.

Execution remains explicit, single-shot, and never automatically retried.

## Editor selection

The two WPF applications will expose equivalent editor kinds:

- none for parameterless or unsupported Commands;
- Boolean for Boolean arguments;
- text for Numeric, String, and ByteArray arguments.

The editor-selection logic will depend on `DataDescriptor`, not Command path,
display name, endpoint identity, instrument identity, or application-specific
knowledge.

The first implementation may retain separate presentation view models in the
two applications. Shared parsing and validation semantics must reside outside
WPF and outside transport adapters.

## Validation endpoint

The existing opt-in `simulation-byte-buffer-validation` endpoint will be
expanded with typed validation Commands whose effects are observable through
authoritative Properties. It must continue to include the existing
`Buffer.Replace` ByteArray Command.

The validation model will cover:

| Command argument | Required validation |
| --- | --- |
| parameterless | execution without an editor |
| Boolean | both values |
| Numeric | valid value, inclusive boundaries, comma-decimal rejection, range rejection, and non-finite rejection |
| String | ordinary, empty, and whitespace-only values |
| ByteArray | exact bytes, leading zero, `FF`, whitespace, and incomplete-byte rejection |

Invalid argument input must remain local. Valid local and remote executions must
reach the normal runtime-host Command path and produce authoritative observable
state.

## Consequences

- Desktop and Laptop Command argument editors use identical conversion rules.
- New supported Command argument types do not require Command-path-specific UI.
- Parameterless Commands remain compatible.
- No protocol-version or gRPC-contract change is required.
- Runtime and endpoint validation remain authoritative.
- Property input and Command input may share internal typed conversion helpers,
  but their public parsers retain operation-specific contracts.
- Enumeration editors, richer numeric controls, localization, structured binary
  schemas, and multiple arguments remain separate work.

## Implementation

1. 10A — ADR and shared Command input semantics.
2. 10B — Laptop Client descriptor-driven Command argument editors.
3. 10C — Desktop Runtime Host descriptor-driven Command argument editors.
4. 10D — typed Command validation simulation.
5. 10E — local and remote end-to-end validation.
6. 10F — documentation and closure.

Every increment must build and preserve all previously validated Property,
Command, Event, observation, reconnect, and shutdown behavior.

## Acceptance

ADR-0038 is complete when:

- parameterless Commands remain executable in both applications;
- Boolean, Numeric, String, and ByteArray argument editors are selected solely
  from authoritative descriptors;
- local and remote applications use shared input semantics;
- invalid input remains local and disables execution;
- valid typed values execute through the existing local and authenticated remote
  paths;
- ByteArray mapping remains exact end to end;
- editor state survives unrelated inventory and observation refreshes;
- execution remains generation-qualified, explicit, and never automatically
  retried;
- automated tests cover parsing, editor selection, execution requests, mapping,
  validation simulation, and regression behavior;
- controlled Desktop and Laptop validation succeeds.
