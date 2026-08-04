# ADR-0046 — Controlled KEL-103 Operating State and Setpoints

- Status: Accepted — Increment 46F versioned definitions complete
- Date: 2026-08-04

## Context

ADR-0045 publishes the explicitly configured KEL-103 through the production
Runtime Host as one normalized electronic-load instrument with five read-only
Properties. Identity, voltage, current, and power access; readiness-gated
publication; supervised USB and instrument power-cycle recovery; diagnostics;
and simultaneous multi-host operation are physically validated.

The next objective adds controlled access to steady-state mode, input state,
and setpoints. These operations change physical load behavior and therefore
require stronger characterization, readback, uncertainty, interlock, recovery,
and restoration rules than the completed read-only adapter.

The supplied KEL-103 command reference documents five steady-state functions:
constant voltage, constant current, constant resistance, constant power, and
short circuit. It also documents voltage, current, resistance, and power
setpoint query/set families and input ON/OFF commands. A query form for input
state is described but not demonstrated with a response example. Vendor
documentation is candidate evidence, not sufficient authority for publication.

## Decision

### Scope

The completed definition will retain the five ADR-0045 Properties and add these
authoritative state Properties:

- `Operating.Mode`, read-only string;
- `Input.Enabled`, read-only Boolean;
- `Target.Voltage`, read/write numeric value in volts;
- `Target.Current`, read/write numeric value in amperes;
- `Target.Resistance`, read/write numeric value in ohms; and
- `Target.Power`, read/write numeric value in watts.

`Operating.Mode` recognizes all five physically characterized steady-state
modes:

- CC — constant current, setter and readback token `CC`;
- CV — constant voltage, setter and readback token `CV`;
- CR — constant resistance, setter and readback token `CR`;
- CW — constant power, setter and readback token `CW`; and
- SHORT — short circuit, case-sensitive setter and readback token `SHORt`.

Mode selection is behavior rather than direct Property assignment. The
definition exposes these parameterless Commands:

- `Mode.SelectConstantCurrent`;
- `Mode.SelectConstantVoltage`;
- `Mode.SelectConstantResistance`;
- `Mode.SelectConstantPower`; and
- `Mode.SelectShortCircuit`.

Input control exposes:

- `Input.Activate`, parameterless and valid only for CC, CV, CR, or CW;
- `Input.Deactivate`, parameterless and valid in every mode; and
- `ShortCircuit.Activate`, requiring one Boolean confirmation argument whose
  value must be `true`.

No dynamic, pulse, flip, LIST, battery, OCP, OPP, trigger, save, recall, buzzer,
baud-rate, arbitrary SCPI, or automatic-discovery capability enters this
definition.

### Characterization gate

Every query and state-changing command is introduced first through a narrowly
bounded Protocol Explorer scenario. Read-only characterization establishes:

- exact request text and termination;
- whether input-state query is supported and its exact Boolean syntax;
- exact mode tokens and handling of every documented steady-state mode;
- exact setpoint response syntax and units;
- upper and lower limits reported by the instrument;
- invariant numeric parsing, precision, sentinel behavior, latency, maximum
  response size, and error behavior; and
- absence of state mutation from every query.

No input-state Property or input Command may be published unless an
authoritative input-state query is physically verified. No writable setpoint or
mode Command may be published before its exact command, readback, and
restoration behavior is physically characterized.

State-changing characterization proceeds only with the load input confirmed
OFF. Each probe records the original authoritative mode and setpoints, changes
one item, reads it back, and restores and reverifies the original value before
the next probe. `MAX` and `MIN` setter tokens are excluded from the public
surface; HASE sends only validated invariant numeric values.

### Definition versioning and deployment

The existing version-2 read-only definition remains immutable. A version-3
definition may add only successfully characterized read-only state and setpoint
Properties. The final controlled definition receives another exact version only
after all writable mappings and Commands pass their characterization gates.

An installed Runtime Host never migrates its definition reference
automatically. Moving an installation to a newer definition is an explicit
offline profile operation performed while the Runtime Host is stopped, with
strict validation, atomic replacement, and a retained backup. Recovery never
changes definition version.

### State-changing operation semantics

All SCPI exchanges continue through the one serialized session owned by the
Runtime Host. No state-changing operation is retried automatically.

For a setpoint write the adapter:

1. authoritatively queries input state;
2. rejects the write unless input is OFF;
3. validates type, finiteness, range, precision, and unit against the exact
   descriptor;
4. sends one invariant setter command;
5. queries the same authoritative setpoint;
6. succeeds only when readback confirms the requested normalized value; and
7. updates the runtime cache only from the authoritative readback.

Each setpoint setter also selects its associated steady-state mode: voltage
selects CV, current selects CC, resistance selects CR, and power selects CW.
The adapter must treat that mode change as authoritative operation behavior,
read it back, and update mode state rather than assuming a mode-neutral write.

For a mode-selection Command the adapter authoritatively confirms input OFF,
sends exactly one fixed mode command, queries current mode, and succeeds only
when readback matches the requested mode.

`Input.Activate` authoritatively queries input state and mode immediately before
transmission. It rejects SHORT mode. After one activation command it queries
input state and succeeds only when ON is confirmed.

`ShortCircuit.Activate` requires the normalized Boolean argument `true`, then
authoritatively verifies input OFF and mode SHORT immediately before sending
the activation command. It succeeds only after input-state readback confirms
ON. A missing, false, malformed, or unsupported argument is rejected without
SCPI transmission.

`Input.Deactivate` has no mode or cached-state precondition. It sends one OFF
command and succeeds only after authoritative input-state readback confirms
OFF. It is not retried automatically even though deactivation is the safe
operator intent. If remote deactivation cannot be confirmed, the operator must
use the instrument's physical OFF control.

If transmission may have started and completion or readback cannot be
established, the physical outcome is uncertain. The session is faulted when
required, the normalized operation fails with sanitized uncertainty detail, no
speculative cache update occurs, and supervised recovery creates a new session.
Existing northbound contracts are reused; raw SCPI exceptions do not cross the
adapter boundary.

### Runtime lifecycle and external changes

Initial publication and every recovery verify identity and synchronize all
descriptor-backed Properties, including actual mode, input state, and all four
setpoints. Recovery never replays a mode selection, setpoint write, activation,
or deactivation. The instrument remains authoritative if its front panel is
used while the Runtime Host is connected; the next authoritative read reflects
the physical state.

Runtime Host or Client shutdown does not silently send OFF. An implicit command
could have an uncertain outcome and would make application shutdown a hidden
physical operation. Operators must explicitly deactivate and confirm OFF before
ending a session when the load must be safe.

### SHORT safety boundary

Selecting SHORT is permitted only while authoritative input state is OFF and
does not itself activate the input. Generic activation is rejected in SHORT
mode. The separately named `ShortCircuit.Activate` command and required true
confirmation make short-circuit intent explicit at the normalized boundary.

Physical SHORT validation is a late, separately approved step. It requires a
current-limited source, reviewed voltage and current limits, a brief activation
interval, continuous operator presence, an accessible physical OFF control,
authoritative ON and OFF confirmation, and restoration of the original mode and
setpoints. No unattended SHORT validation is permitted.

### Diagnostics

Operational diagnostics may identify sanitized operation category, requested
capability, direction, bounded outcome, duration, endpoint identity, and
attachment generation. They must not contain SCPI command text, requested or
returned values, setpoints, serial targets, raw responses, instrument serial
identity, credentials, configuration paths, or raw exception text. SCPI
Protocol and Bytes diagnostics remain deferred.

## Consequences

- HASE can display complete steady-state operating state and controlled targets
  without exposing SCPI northbound.
- Setpoints use authoritative read/write Properties; behavior uses explicit
  Commands.
- CR is a first-class controlled mode with an authoritative resistance target.
- SHORT is supported through a distinct confirmation and activation boundary.
- Input-state query support is a hard prerequisite for every input-control
  capability.
- State changes remain explicit, serialized, read back, non-retried, and honest
  about uncertain outcomes.
- Recovery preserves attachment identity but never replays physical intent.
- Operators retain responsibility for external-source limits and physical safe
  shutdown.

## Approved increment sequence

1. 46A — Decision, safety model, and characterization plan.
2. 46B — Read-only mode, input-state, and setpoint characterization — complete
   at 4,854 tests.
3. 46C — Read-only upper/lower-limit characterization — complete at 4,905
   tests.
4. 46D — Input-OFF mode-selection characterization and restoration — complete
   at 4,937 tests.
5. 46E — Input-OFF setpoint-write characterization and restoration — complete
   at 5,000 tests.
6. 46F — Versioned state and controlled-capability definitions — complete at
   5,018 tests.
7. 46G — Runtime reads, writes, Commands, readback, and uncertain outcomes.
8. 46H — Hosting, recovery, diagnostics, Host, and Client integration.
9. 46I — Controlled activation, deactivation, SHORT, recovery, and multi-host
   physical validation.
10. 46J — Documentation and closure.

Every increment requires explicit approval and remains independently buildable
and testable.

## Validation strategy

Automated validation covers strict parsing, exact request text, culture
invariance, unit and range enforcement, input-OFF interlocks, Boolean SHORT
confirmation, no-send rejection paths, serialized operations, authoritative
readback, cache authority, uncertain outcomes, session faulting, no retry,
recovery without replay, definition compatibility, diagnostics sanitization,
and unchanged normalized remote contracts.

Physical validation proceeds from read-only queries to input-OFF mode and
setpoint changes with exact restoration, then conservative CC, CV, CR, and CW
activation under a current-limited source. SHORT activation is last and
separately gated. Every energized run ends with confirmed OFF, restored state,
orderly shutdown, and independent port reopening.

## Baseline

ADR-0046 starts from the clean pushed ADR-0045 closure baseline of 4,772
automated tests passing in Visual Studio 2026 Release configuration on .NET 10.
The Client and both Runtime Hosts are stopped. Machine-specific reachability,
instrument serial identity, and deployment security values remain external.

## Increment 46B characterized result

The bounded Protocol Explorer path verifies identity and sends exactly one
selected fixed read-only query. Physical validation established case-sensitive
mode responses `CC`, `CV`, `CR`, `CW`, and `SHORt`; input responses `OFF` and
`ON`; and invariant voltage, current, resistance, and power target responses
with exact suffixes `V`, `A`, `OHM`, and `W`. Four fractional digits were
observed for each characterized target response.

Mode selection and the brief input activation used only attended front-panel
actions while the external supply output remained off. No state-changing SCPI
command was transmitted. Final authoritative queries confirmed CC mode, input
off, and unchanged original setpoints. Every session closed normally, and an
independent redacted identity-only query reopened the port successfully.

No actual target value, serial target, instrument serial identity, or deployment
security value is retained in the characterization record. Increment 46B closes
at 4,854 automated tests passing in Visual Studio 2026 Release configuration on
.NET 10.

## Increment 46C characterized result

The first read-only limit candidate, `:VOLTage? MIN`, received no framed
response and reached the bounded exchange timeout. It was not retried, no
alternative was transmitted in that run, the session was disposed, and
authoritative follow-up found no state mutation. `MIN` and `MAX` remain excluded
setter tokens rather than supported limit-query parameters.

Physical validation established separate case-sensitive `LOW?` and `UPP?`
paths. The instrument reported ranges of 0.1000–120.00 V,
0.0000–30.000 A, 0.0500–7500.0 OHM, and 0.0000–300.00 W. Responses used
invariant numeric text and exact units `V`, `A`, `OHM`, and `W`, with differing
lexical precision across targets and bounds.

Every supported query reverified identity, sent exactly one fixed read-only
limit request, completed inside the bound, and closed normally. Final ordinary
queries confirmed CC mode, input off, and unchanged original targets. The port
then reopened independently for a redacted identity-only query. Increment 46C
closes at 4,905 automated tests passing in Visual Studio 2026 Release
configuration on .NET 10.

## Increment 46D characterized result

The bounded Protocol Explorer path first verified KEL-103 identity,
authoritative input OFF, initial CC mode, and all four original setpoints. It
then transmitted one fixed destination command, required input to remain OFF,
and required exact destination readback. Only after that confirmation did it
transmit one CC restoration command, reverify OFF and CC, and compare all four
setpoints with the original normalized snapshot. No command was retried and no
recovery session replayed an operation.

Physical validation established these exact case-sensitive mappings on firmware
V3.30:

| Selection | Setter command | Authoritative readback |
| --- | --- | --- |
| CC | `:FUNCtion CC` | `CC` |
| CV | `:FUNCtion CV` | `CV` |
| CR | `:FUNCtion CR` | `CR` |
| CW | `:FUNCtion CW` | `CW` |
| SHORT | `:FUNCtion SHORt` | `SHORt` |

The legacy-shaped `:FUNCtion VOLT` candidate was transmitted once but left the
authoritative mode at CC. The all-uppercase `:FUNCtion SHORT` candidate likewise
left the mode at CC. Neither failed candidate was retried, and the restoration
path was correctly suppressed because the requested destination had not been
confirmed. Physical inspection found CC and OFF after both failures.

Offline inspection of the manufacturer-supplied RND testing package found the
mixed-case literal `SHORt` combined with the utility's `:FUNC %s` command
format. A separately approved physical probe then confirmed `:FUNCtion SHORt`
exactly once and restored CC exactly once. CV, CR, and CW probes also completed
with one destination and one restoration transmission each. Input remained OFF,
all four setpoints remained unchanged, and every successful session closed in
authoritative CC and OFF state while the external supply output remained off.

SHORT selection did not activate the load. Input activation remains a separate
operation, generic activation must reject SHORT, and `ShortCircuit.Activate`
still requires explicit Boolean confirmation and its own later physical gate.
Increment 46D closes at 4,937 automated tests passing in Visual Studio 2026
Release configuration on .NET 10.

## Increment 46E characterized result

Physical validation established these exact invariant setter forms on firmware
V3.30:

| Target | Setter form | Mode side effect |
| --- | --- | --- |
| Voltage | `:VOLTage <value>V` | CV |
| Current | `:CURRent <value>A` | CC |
| Resistance | `:RESistance <value>OHM` | CR |
| Power | `:POWer <value>W` | CW |

The first same-value voltage probe established that a voltage setter is not
mode-neutral: it selected CV while input remained OFF. The initial scenario
reported that mode mismatch without sending another command, and attended
physical inspection confirmed CV and OFF before manual CC restoration. The
corrected path then required initial CC, confirmed each setter-associated mode,
verified all four targets unchanged, and restored CC exactly once for voltage,
resistance, and power. Current remained in CC and sent no redundant mode
restoration.

Changed-value characterization queried the selected target's established lower
and upper bounds and derived one different interior candidate at exactly one
response-scale decimal quantum. No value or bound was displayed or retained in
documentation. Each changed setter was transmitted once and authoritatively
confirmed with input OFF, the expected mode, and all unrelated targets
unchanged. The original selected value was then transmitted once and all four
original targets were confirmed. Voltage, resistance, and power completed one
additional CC restoration; current required none.

Every successful same-value and changed-value session closed in authoritative
CC and OFF state with all original targets restored while the external supply
output remained off. No command was retried and no recovery session replayed a
mutation. Incomplete or uncertain outcomes stop before speculative restoration;
a valid but differently quantized changed readback permits the already-planned
original-value and CC restoration before reporting failure.

Increment 46E closes at 5,000 automated tests passing in Visual Studio 2026
Release configuration on .NET 10. Production Properties and Commands remained
unchanged through Increment 46E.

## Increment 46F versioned definition result

The definition repository now preserves four exact versions under the existing
KEL-103 descriptor identity:

| Version | Capability |
| --- | --- |
| 1 | Immutable identity definition |
| 2 | Immutable production read-only identity and measurement definition |
| 3 | Read-only identity, measurements, operating mode, input state, and four targets |
| 4 | Version-3 state plus four writable targets and five mode-selection Commands |

Version 3 contains eleven read-only Properties. It retains product identity,
firmware, measured voltage, measured current, and measured power, then adds
`Operating.Mode`, `Input.Enabled`, `Target.Voltage`, `Target.Current`,
`Target.Resistance`, and `Target.Power`. The four targets carry their physically
characterized native units and ranges. No invariant numeric resolution is
claimed from response formatting alone.

Version 4 retains the same eleven Properties. Only the four target Properties
are read/write. It adds the parameterless Commands
`Mode.SelectConstantCurrent`, `Mode.SelectConstantVoltage`,
`Mode.SelectConstantResistance`, `Mode.SelectConstantPower`, and
`Mode.SelectShortCircuit`. SHORT selection is mode selection only and does not
activate the input.

`Input.Activate`, `Input.Deactivate`, and `ShortCircuit.Activate` remain absent.
They require their later characterization and physical-validation gates before
publication. No runtime mapping, execution path, profile migration, or
deployment change entered Increment 46F. Production remains on immutable
definition version 2 until an explicitly approved offline migration validates
and atomically replaces the installed profile while retaining a backup.

Increment 46F closes at 5,018 automated tests passing in Visual Studio 2026
Release configuration on .NET 10. No additional physical operation was required
because the definitions encode the accepted Increment 46B through 46E evidence
without yet executing those capabilities in production.
