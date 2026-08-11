# ADR-0052 — Python Client Examples and Laboratory Automation

- Status: Accepted objective; Increment 52A
- Date: 2026-08-10

## Context

ADR-0050 established the supported asyncio-native external Python Client boundary. ADR-0051 established reproducible local distribution, private installed automation, guarded workflows, and explicit Laptop selection between the Desktop and MiniPC Runtime Hosts.

HASE now needs small repository-backed programs that show a user how to consume the installed public Python API without introducing another framework.

## Decision

Examples use only the public `hase` package API and Python standard library unless a later increment explicitly approves another dependency. Laptop target selection remains explicit through the external two-target registry and an exact target identifier. Examples do not discover Runtime Hosts, select a default target, fan out, fail over, redirect, retry, reconnect, or automatically resubscribe.

Read-only examples precede mutation examples. Later Property writes and Commands retain ADR-0050/0051 uncertain-outcome, no-retry, and no-replay semantics.

Examples remain repository source and are not added to the wheel merely by being introduced. Increment 52A therefore retains `hase-client 0.6.0` unchanged.

## Increment 52A — Explicit Runtime Host Inventory Example

52A adds `examples/inspect_runtime_host.py`. The program requires an external target-registry path and exactly one of `desktop-runtime-host` or `minipc-runtime-host`. It resolves one profile, opens one mutual-TLS channel, invokes `GetSnapshot` exactly once, closes deterministically, and presents descriptor inventory.

The presentation includes API version, endpoint identity and state, instrument identity/name/kind, non-sensitive manufacturer/model/firmware metadata, Properties, Commands, Events, and numeric descriptor metadata. It deliberately does not print Runtime Host identity, attachment generation, instrument serial number, profile path, address, credential/trust paths or contents, diagnostics, or raw transport objects.

52A performs no cached or authoritative Property read, Property write, Command, observation subscription, diagnostic subscription, authorization change, hardware mutation, discovery, retry, reconnection, failover, or fan-out.

## Increment 52B — Authoritative Property Read Example

52B adds `examples/read_property.py`. The program requires the external target registry plus exact target, endpoint, instrument, and Property identifiers. It obtains exactly one snapshot, requires the endpoint to be Ready and the Property readable, constructs the current `PropertyTarget` from the snapshot attachment generation, performs exactly one authoritative Property read, and closes deterministically.

The example prints target and Property display names, the confirmed value, descriptor-provided native unit for numeric Properties, quality, and authoritative UTC timestamp. It withholds Runtime Host identity/address, profile and credential/trust paths or contents, attachment generation, instrument serial number, raw transport objects, and remote diagnostics.

52B never reads cached data, writes, executes a Command, subscribes, retries, reconnects, obtains a second snapshot, fails over, or fans out. An attachment-not-current result is reported rather than reconciled automatically. The package remains `hase-client 0.6.0`.

Physical acceptance uses only Laptop target `minipc-runtime-host` and MiniPC Arduino A0. The result must be finite numeric, `GOOD`, within the descriptor range, and use the descriptor-provided voltage unit. No authorization, profile, credential, registry, diagnostics, or hardware-state change is required.

## Increment 52C — Bounded Repeated Measurement Example

52C adds `examples/sample_property.py`. The program requires the external target registry plus exact target, endpoint, instrument, Property, interval, and count. Interval is bounded to 0.1 through 3600 seconds and count to 1 through 1000. The Python program owns the schedule; no scheduling behavior is added to HASE.

The example opens one channel, obtains exactly one snapshot, resolves one current readable `PropertyTarget`, and reuses that attachment generation for the complete bounded run. A successful run performs exactly `count` sequential authoritative reads. Sample starts are anchored to a monotonic schedule; reads never overlap, and an already-due sample begins immediately after the preceding read finishes.

Any failed sample terminates the run. The example never retries, reconnects, obtains another snapshot, refreshes attachment generation, skips a failed sample, reads cached data, writes, executes a Command, subscribes, fans out, or fails over. Output uses authoritative HASE UTC timestamps, descriptor-provided numeric units, and Property quality for every sample. No file or plot is produced. The package remains `hase-client 0.6.0`.

Physical acceptance uses only Laptop target `minipc-runtime-host` and MiniPC Arduino A0 with `--interval 1.0 --count 5`. All five values must be finite numeric values within the descriptor range, use `V`, and have `good` quality. No authorization, profile, credential, registry, diagnostics, or hardware-state change is required.

## Increment 52D1 — Bounded Live Observation Example

52D1 adds `examples/observe_runtime_host.py`. The program requires the external target registry, one exact Runtime Host target, and a bounded live-observation count from 1 through 1000. It opens exactly one ordinary observation stream and does not invoke a separate snapshot RPC: the stream's required `ObservationInitialSnapshot` establishes the starting sequence and endpoint count, and does not consume the requested live-observation count.

The example presents all five public observation kinds: attachment publication, attachment ending, connection-status change, Property-value change, and Event occurrence. It displays observation sequence and relevant typed payload fields while withholding Runtime Host identity/address, attachment generation, profile and credential/trust paths or contents, instrument serial numbers, raw protobuf/gRPC objects, and diagnostics.

52D1 never replays, reconnects, resubscribes, opens diagnostics, reads or writes a Property, executes a Command, fans out, or fails over. The existing public client's stream ordering and gap detection remain authoritative. Stream failure, early ending, or cancellation terminates the run; bounded completion closes the channel deterministically. The package remains `hase-client 0.6.0`.

Physical validation is deliberately deferred to Increment 52D2 because the accepted Laptop MiniPC Python principal does not currently possess the distinct `observation.subscribe` permission. 52D1 makes no authorization, profile, credential, registry, diagnostics, or hardware-state change.

## Increment 52D2A — Laptop MiniPC Narrow Observation Authorization Tooling

52D2A introduces a dedicated transactional authorizer for the existing `hase-laptop-python-minipc` principal. Its required pre-state is exactly `runtime-host.snapshot.read` plus `property.authoritative.read`; its only permitted post-state change is adding `observation.subscribe`. The existing `hase-python-automation` observation authorizer remains unchanged.

The dedicated authorizer preserves exact policy SHA-256 checking, atomic staged publication, rollback retention, access-control preservation, post-publication hash validation, and automatic rollback on publication failure. It rejects an already-authorized principal and any unexpected Laptop MiniPC permission set. The operator exposes a distinct `authorize-laptop-minipc-observation` verb and the PowerShell wrapper `Enable-HaseLaptopMiniPcPythonObservation.ps1`.

52D2A changes repository tooling only. It does not alter external authorization, credentials, profiles, target registries, diagnostics, or hardware. Physical 52D2 validation remains deferred until the new tooling is tested, committed, pushed, and synchronized to LABC.

## Increment 52D2 — Narrow Observation Authorization and Physical MiniPC Event Validation

52D2 persistently extends only the existing `hase-laptop-python-minipc` principal from its exact two-grant state (`runtime-host.snapshot.read`, `property.authoritative.read`) by adding `observation.subscribe`. The accepted resulting grant set contains exactly those three permissions. `property.cached.read`, `property.write`, `command.execute`, and `diagnostics.subscribe` remain absent.

The authorization transaction used the dedicated 52D2A authorizer while the MiniPC Runtime Host was stopped. The active policy changed from SHA-256 `1b048431568e1cd20c88f96184bcae328fee26890bba9611620aa7c8e07e59d1` to `2a450a398994eb58fb1e34e1abc0f9c0867add96c5d8e36f9f926b1987d33a10`; the retained external rollback file remains the exact pre-change policy with the original SHA-256. No credential, private key, profile, target registry, server trust, diagnostics authorization, or repository state was changed by the transaction.

Physical validation used the installed `hase-client 0.6.0` on Laptop target `minipc-runtime-host` with one bounded ordinary observation stream. The stream's initial snapshot reported one endpoint and live sequence started at 1. Periodic same-value `built-in-led-state` and `analog-input-voltage` Property observations were present, so physical validation used a bounded count of 30 rather than assuming the first live observation would be the operator event. The contiguous sequence 1 through 30 included `Controller/ButtonPressed` Event observations at sequences 9 and 22 for `arduino-uno-01` / `arduino-uno-controller-01`; the operator confirmed two physical button presses. The example did not filter, replay, reconnect, resubscribe, open diagnostics, read or write a Property, or execute a Command.

After validation the MiniPC Runtime Host was stopped. The active policy SHA-256, exact three-grant principal state, rollback SHA-256, and clean synchronized repository were reverified. The three-grant authorization is retained as the accepted operational state.

## Increment 52E — Guarded Same-Value Property Write Example

52E adds `examples/write_same_value_property.py` as the first user-oriented mutation example. The program requires an external target registry, exact target, endpoint, instrument and Property identifiers, plus the explicit `--confirm-same-value-write` switch. It has no value argument: confirmation authorizes only one write of the current authoritative value back to the same Property.

A successful run opens one channel, obtains exactly one snapshot, requires one Ready `READ_WRITE` Property, performs one initial authoritative read, writes that exact returned value exactly once, requires the returned confirmed value to match exactly, performs one authoritative reconciliation read, and requires that value to match exactly. The one snapshot attachment generation is retained throughout.

Rejected and uncertain mutation outcomes are surfaced through the existing public mutation classification and terminate immediately. No mutation failure path retries, replays, reconnects, refreshes the snapshot, writes again, or automatically reconciles an uncertain outcome. The example never reads cached data, executes a Command, subscribes to observations or diagnostics, fans out, or fails over. Numeric presentation uses the descriptor-provided native unit; byte-array values are not printed. The package remains `hase-client 0.6.0`.

52E changes repository example/test/documentation source only. It does not change external authorization, credentials, profiles, target registries, Runtime Host state, or hardware. Physical validation is deferred because the accepted Laptop MiniPC Python principal does not possess `property.write`; a separately approved increment must address that authorization boundary before physical use.

## Increment 52F1 — Dedicated Laptop MiniPC Property-Write Authorization Tooling

52F1 adds repository tooling for one narrow policy-only authorization transition for principal `hase-laptop-python-minipc`. The accepted pre-state is exactly `runtime-host.snapshot.read`, `property.authoritative.read`, and `observation.subscribe`; the only added permission is `property.write`. Any missing, reordered, duplicate, already-authorized, or unexpected target-principal grant state is rejected.

The dedicated authorizer requires the exact current authorization-policy SHA-256, writes a staged candidate, rechecks the active revision, publishes atomically with `File.Replace`, retains an exact rollback copy, and verifies policy hash plus rollback hash and security metadata. It changes only the Runtime Host authorization policy; it does not modify an application profile, credentials, certificates, private keys, target registries, trust configuration, diagnostics configuration, or hardware state.

The operator adds only `authorize-laptop-minipc-property-write`; existing generic Property-write, Command, and observation verbs retain their existing behavior. `tools/Enable-HaseLaptopMiniPcPythonPropertyWrite.ps1` computes the active policy SHA-256 and invokes the already-built Release operator with `--no-build`. 52F1 changes repository source/tooling only and performs no external authorization transaction. Physical same-value validation remains deferred to 52F2 after this tooling is tested, committed, pushed, and synchronized to LABC and LTAEP.

## Increment 52F2 — Narrow Property-Write Authorization and Physical Same-Value Validation

52F2 persistently extends only the existing `hase-laptop-python-minipc` principal from its exact three-grant state (`runtime-host.snapshot.read`, `property.authoritative.read`, `observation.subscribe`) by adding `property.write`. The accepted resulting grant set contains exactly those four permissions. `property.cached.read`, `command.execute`, and `diagnostics.subscribe` remain absent.

The authorization transaction used the dedicated 52F1 authorizer while the MiniPC Runtime Host was stopped. The active policy changed from SHA-256 `2a450a398994eb58fb1e34e1abc0f9c0867add96c5d8e36f9f926b1987d33a10` to `74d1ff1173960f7e39792ce187ef9c9a1a92df5d73094fcea66bf006a3d996b5`; the retained external rollback file remains the exact pre-change policy with SHA-256 `2a450a398994eb58fb1e34e1abc0f9c0867add96c5d8e36f9f926b1987d33a10`. No credential, private key, profile, target registry, trust, diagnostics authorization, or repository state was changed by the transaction.

Physical validation used the installed `hase-client 0.6.0` on Laptop target `minipc-runtime-host` against `arduino-uno-01` / `arduino-uno-controller-01` / `built-in-led-state`. The committed guarded example required `--confirm-same-value-write`, read the authoritative value as `False`, wrote exactly that same `False` value once, received a confirmed write result, performed one authoritative reconciliation read, and observed `False` again. The example exited with code 0 and reported `Reconciliation: matched`. No arbitrary new value was supplied, and no retry, replay, reconnect, cached read, Command, observation, or diagnostics operation was performed.

After validation the MiniPC Runtime Host was stopped. The active policy SHA-256 remained `74d1ff1173960f7e39792ce187ef9c9a1a92df5d73094fcea66bf006a3d996b5`, the rollback SHA-256 remained the exact pre-52F2 value, the four-grant principal state was reverified, prohibited permissions remained absent, and the repository remained clean. The four-grant authorization is retained as the accepted operational state.

## Validation

Automated coverage proves mandatory explicit target selection, deterministic sanitized presentation, one selected profile, one snapshot, deterministic channel closure on success and snapshot failure, and absence of non-snapshot client operations from the example source.

Physical validation is read-only and separately targets the Desktop and MiniPC Runtime Hosts from the Laptop. No authorization change is required. The accepted ADR-0051 inventory counts are the comparison baseline; no Property value is read during 52A validation.

## Consequences

- The first user-oriented Python program demonstrates the already accepted `0.6.0` public API without changing it.
- Example code remains visibly separate from package, provisioning, and deployment tooling.
- A later increment may add one authoritative Property-read example without changing 52A's inventory-only semantics.
