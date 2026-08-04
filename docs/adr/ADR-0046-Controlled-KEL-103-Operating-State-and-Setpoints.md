# ADR-0046 — Controlled KEL-103 Operating State and Setpoints

- Status: Accepted — Increment 46A
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

`Operating.Mode` recognizes all five documented steady-state modes:

- CC — constant current, wire token `CURR`;
- CV — constant voltage, wire token `VOLT`;
- CR — constant resistance, wire token `RES`;
- CW — constant power, wire token `POW`; and
- SHORT — short circuit, wire token `SHORT`.

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
2. 46B — Read-only mode, input-state, and setpoint characterization.
3. 46C — Read-only upper/lower-limit characterization.
4. 46D — Input-OFF mode-selection characterization and restoration.
5. 46E — Input-OFF setpoint-write characterization and restoration.
6. 46F — Versioned state and controlled-capability definitions.
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
