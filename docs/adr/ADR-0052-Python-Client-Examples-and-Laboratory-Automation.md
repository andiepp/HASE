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

## Validation

Automated coverage proves mandatory explicit target selection, deterministic sanitized presentation, one selected profile, one snapshot, deterministic channel closure on success and snapshot failure, and absence of non-snapshot client operations from the example source.

Physical validation is read-only and separately targets the Desktop and MiniPC Runtime Hosts from the Laptop. No authorization change is required. The accepted ADR-0051 inventory counts are the comparison baseline; no Property value is read during 52A validation.

## Consequences

- The first user-oriented Python program demonstrates the already accepted `0.6.0` public API without changing it.
- Example code remains visibly separate from package, provisioning, and deployment tooling.
- A later increment may add one authoritative Property-read example without changing 52A's inventory-only semantics.
