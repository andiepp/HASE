# ADR-0021 - USB Serial Endpoint Discovery and Authoritative Compact Verification

- Status: Proposed
- Date: 2026-07-22

---

# Context

HASE supports resource-constrained Arduino Uno-class endpoints through USB serial transport and Compact Serial Protocol Version 1.

ADR-0020 defines the compact endpoint architecture and keeps four identities separate:

1. the configured serial connection;
2. the USB-to-serial adapter;
3. the authoritative HASE endpoint;
4. the versioned endpoint descriptor.

Capabilities C-018 through C-022 established and physically validated production USB serial transport, compact framing and CRC validation, authoritative bootstrap, exact host-side descriptor resolution, compact commands and properties, runtime property-cache synchronization, connection supervision, automatic recovery, and explicit manual COM-port configuration.

The current physical workflow requires the application or user to provide a COM-port name before compact bootstrap can begin. This does not support automatic discovery when the assigned port is unknown, Windows assigns a different port after reconnection, several adapters are connected, or an application needs to present compatible endpoints for explicit selection.

Windows can expose a COM-port name and optional USB metadata such as vendor identifier, product identifier, product name, manufacturer name, and USB serial number. This metadata may identify an adapter model or physical USB device, but it is not authoritative HASE endpoint identity.

A CH340-class adapter may expose no unique USB serial number. An adapter may be replaced without changing the attached HASE endpoint. Identical adapters may connect different endpoints. A serial device may expose matching USB metadata without implementing HASE.

Automatic discovery must therefore use USB serial metadata only to locate and optionally filter candidates. Every accepted compact endpoint must be verified through Compact Serial Protocol bootstrap. `CompactBootstrapResponse.EndpointId` remains the authoritative HASE endpoint identity.

Discovery must isolate ports that are busy, unavailable, inaccessible, silent, non-HASE, malformed, unsupported, or incompatible. A failure involving one candidate must not terminate discovery of other candidates.

Discovery must not create, attach, replace, detach, or mutate runtime endpoints automatically. Manual COM-port configuration remains supported as an equal connection-definition source.

The first implementation and physical validation platform is Windows. Public contracts remain platform-neutral so that Linux discovery can be added later.

Compact Serial Protocol Version 1 and Protocol Version 1 remain unchanged.

---

# Decision

HASE will discover USB serial endpoints through a two-stage process:

```text
USB serial candidate enumeration
    ->
Compact Serial Protocol bootstrap verification
```

USB serial enumeration identifies connection candidates only. Compact bootstrap establishes authoritative HASE endpoint identity and descriptor reference.

The first capability using this decision is C-023:

> Discover USB serial candidates automatically, then verify compatible compact endpoints through authoritative Compact Serial Protocol bootstrap.

## Capability boundary

C-023 includes:

- platform-neutral USB serial candidate and candidate-source contracts;
- a Windows USB serial candidate-source implementation;
- COM-port and optional USB metadata acquisition;
- optional metadata filtering before active verification;
- temporary serial connection ownership for verification;
- Compact Serial Protocol bootstrap verification;
- authoritative endpoint identity and compact descriptor-reference acquisition;
- exact host-side descriptor resolution and compatibility validation;
- isolated outcomes for unsuccessful candidates;
- authoritative endpoint-ID deduplication;
- cancellation-aware sequential discovery orchestration;
- a Protocol Explorer physical validation scenario;
- physical validation with the existing Arduino Uno compact endpoint.

C-023 does not include:

- automatic runtime attachment or endpoint replacement;
- runtime attachment inventory mutation;
- live USB hot-plug presence events;
- persistent adapter-to-endpoint association;
- recovery across a COM-port name change;
- Linux USB serial enumeration;
- bounded-parallel verification;
- protocol changes;
- proprietary commercial-instrument adapters;
- BLE discovery;
- authentication, authorization, or encryption.

## Architectural layers

USB serial discovery is separated into candidate enumeration, candidate verification, and discovery orchestration.

### Candidate enumeration

`Hase.Transport` owns transport-level discovery contracts and platform-specific enumeration implementations.

A platform-neutral candidate can contain:

- port name or operating-system device path;
- optional USB vendor identifier;
- optional USB product identifier;
- optional product name;
- optional manufacturer name;
- optional USB serial number.

A candidate does not contain an authoritative HASE `EndpointId`.

The first Windows implementation enumerates available serial candidates, associates port names with USB metadata where possible, tolerates missing optional metadata, isolates malformed operating-system records, supports cancellation, and releases all enumeration resources. Enumeration does not open ports or perform compact operations.

Windows-only types, APIs, query syntax, registry paths, and device identifiers remain behind the platform-neutral contract.

### Candidate verification

Every selected candidate is verified by:

1. creating a serial connection definition from the candidate and configured serial settings;
2. opening a temporary serial byte stream;
3. establishing a Compact Serial Protocol connection;
4. sending the existing compact bootstrap request;
5. requiring a valid correlated bootstrap response;
6. accepting `CompactBootstrapResponse.EndpointId` as authoritative identity;
7. resolving the exact compact descriptor reference;
8. validating descriptor and compact-profile compatibility;
9. disposing the temporary compact connection and serial byte stream.

The wire contract remains unchanged. Verification uses an explicit timeout so a silent or unrelated serial device cannot block discovery indefinitely.

A successful result retains both the original candidate metadata and the authoritative bootstrap and descriptor information. Candidate metadata remains descriptive connection information.

### Discovery orchestration

The discovery service combines candidate enumeration, optional filtering, and compact verification.

The initial implementation verifies candidates sequentially. This provides deterministic ownership and ordering, bounded resource use, straightforward cancellation, and reduced risk of disturbing several unrelated serial devices simultaneously.

The service:

- continues after isolated candidate failures;
- preserves unsuccessful outcomes for diagnostics;
- propagates caller cancellation;
- deduplicates verified endpoints by authoritative `EndpointId`;
- produces a unique endpoint inventory for one discovery operation;
- never creates, attaches, replaces, or mutates a runtime endpoint.

Bounded-parallel verification remains deferred until physical evidence demonstrates a need.

## Serial settings

USB metadata does not determine Compact Serial Protocol communication settings. Discovery receives explicit serial settings using the existing production transport contracts.

The first physical C-023 scenario uses:

```text
Baud rate : 115200
Data bits : 8
Parity    : None
Stop bits : One
Handshake : None
```

Manual COM-port configuration remains supported and continues to use the existing compact connection and bootstrap path.

## Candidate metadata and filtering

Candidate filtering may use port name, VID, PID, product name, manufacturer name, and USB serial number. Filtering reduces active probes but does not verify HASE compatibility.

A filter match must not assign endpoint identity, select a descriptor, publish a runtime endpoint, or bypass bootstrap and compatibility validation.

Missing optional metadata does not invalidate a candidate. It may make the candidate ineligible for a filter that explicitly requires that metadata.

## Identity separation

### Connection identity

The port name or device path identifies a connection target for one attempt. It is not endpoint identity and may change after reconnection.

### USB adapter identity

VID, PID, product name, manufacturer name, USB serial number, device instance, and physical topology describe the adapter or attachment. They are optional discovery metadata and are never substituted for HASE endpoint identity.

### Endpoint identity

`CompactBootstrapResponse.EndpointId` is authoritative. Verified endpoints are deduplicated by this value.

If different candidates report the same endpoint identity during one discovery operation, the verified inventory contains that identity once. Discovery does not decide which candidate should replace an existing runtime attachment.

### Descriptor identity

Bootstrap reports the descriptor identifier and version. The runtime host resolves the complete descriptor through exact repository lookup. Descriptor identity remains distinct from endpoint identity.

## Verification outcomes

Each candidate produces either a verified result or an isolated unsuccessful result. The result model distinguishes at least:

- port busy;
- port unavailable;
- access denied;
- connection failed;
- verification timed out;
- non-HASE endpoint;
- invalid compact response;
- unsupported compact protocol version;
- invalid endpoint identity;
- unknown descriptor reference;
- incompatible descriptor or compact profile.

The exact public outcome names are frozen with their contract tests. Platform-specific exceptions do not become the public contract, although diagnostic detail may retain an original exception or platform message.

A candidate failure is not caller cancellation. Caller cancellation propagates and stops enumeration and verification. Temporary resources are disposed before cancellation or failure is returned.

## Deduplication

Candidates are deduplicated by normalized port name or operating-system device path. Verified endpoints are deduplicated by `CompactBootstrapResponse.EndpointId`.

VID, PID, descriptive names, and USB serial number are not authoritative endpoint-deduplication keys.

The unique inventory belongs to one discovery operation. C-023 does not define live Added, Updated, or Removed events.

## Runtime attachment boundary

Discovery ends after verification and descriptor resolution.

A verified result may later produce an existing serial connection definition for an explicit attachment request. The operational connection repeats the identity and descriptor-reference validation required by ADR-0020 because the physical candidate may have changed after discovery.

Discovery never creates a `RuntimeEndpoint`, publishes into `RuntimeContext`, adds an attachment inventory entry, starts supervision, or performs runtime property synchronization.

## Manual configuration

Automatic discovery supplements manual configuration; it does not replace it.

Applications may construct a serial connection definition from manual configuration, stored configuration, a verified discovery result, or a future platform-specific discovery source. All sources converge on the same compact connection and bootstrap architecture.

## Windows-first implementation

Windows is the first implementation and validation platform. The Windows adapter may use Windows device-management facilities internally, but public contracts do not expose Windows Management Instrumentation types, Windows Runtime device types, registry types, device-notification types, or Windows-only identifier formats.

The Windows implementation tolerates devices that expose only a COM-port name and no usable USB metadata.

## Linux compatibility

Linux USB serial discovery remains explicit backlog. Platform-neutral contracts permit a future implementation to report paths such as `/dev/ttyUSB0` and `/dev/ttyACM0`. Linux-specific udev or operating-system details remain internal.

## Connection ownership and safety

Every verification attempt exclusively owns its temporary serial stream and compact connection and disposes them after success, rejection, timeout, or cancellation. A verification connection is never transferred into runtime attachment.

A busy port is an isolated outcome. Discovery does not take ownership from another process or attached runtime endpoint.

Opening an unrelated serial device may cause device-specific behavior. Configurable filtering and sequential verification reduce this risk.

## Protocol stability

C-023 changes neither Compact Serial Protocol Version 1 nor Protocol Version 1. USB metadata belongs to the host-side connection environment and is not added to either protocol.

---

# Consequences

## Positive consequences

- Compatible endpoints can be found without a manually entered COM port.
- COM-port assignment remains connection information rather than identity.
- USB metadata can reduce unnecessary probing.
- Compact bootstrap remains the single authoritative identity source.
- Both protocol versions remain unchanged.
- Manual configuration remains available.
- Candidate failures remain isolated.
- Temporary connections have explicit ownership.
- Discovery cannot mutate runtime attachment state.
- Windows-specific enumeration remains isolated.
- Future Linux discovery can reuse the contracts.
- Exact descriptor resolution prevents metadata-based guessing.

## Negative consequences

- Windows metadata acquisition needs a platform-specific implementation.
- Metadata availability varies between adapters and drivers.
- Some adapters have no unique serial number.
- Sequential verification can be slow when silent ports time out.
- Opening unrelated serial devices may have side effects.
- Discovery and attachment perform separate bootstrap validation.
- Operating-system errors require translation to platform-neutral outcomes.

## Risks

- Broad probing could disturb unrelated serial equipment.
- Windows may report incomplete or inconsistent port associations.
- A port may disappear between enumeration and opening.
- A device may reset when opened.
- Bootloader output may precede compact responses.
- Timeouts may be too long or too short.
- Duplicate USB metadata may mislead presentation or filtering.
- An endpoint may move between discovery and attachment.

These risks are controlled through filtering, explicit timeouts, isolated results, sequential verification, strict framing, authoritative bootstrap, repeat validation during attachment, and separation of discovery from runtime mutation.

---

# Implementation sequence

1. add platform-neutral USB serial candidate and candidate-source contracts with tests;
2. add filter contracts and metadata-filter tests;
3. add the Windows candidate-source adapter with isolated provider tests;
4. add compact candidate-verification result contracts;
5. add temporary bootstrap verification with lifecycle and timeout tests;
6. add sequential orchestration and authoritative deduplication tests;
7. add Protocol Explorer C-023;
8. physically validate with the Arduino Uno endpoint;
9. update Project Status and Roadmap after physical validation.

The first code increment introduces candidate contracts only. It performs no Windows enumeration, serial I/O, compact bootstrap, descriptor resolution, runtime attachment, or protocol changes.

---

# Alternatives considered

## Treat the COM-port name as endpoint identity

Rejected because port assignment may change independently of the endpoint.

## Treat VID and PID as endpoint identity

Rejected because they identify a vendor and product class shared by many devices.

## Treat the USB serial number as endpoint identity

Rejected because it describes the USB device or adapter, may be missing or duplicated, and may change when an adapter is replaced.

## Select the descriptor from USB metadata

Rejected for native compact endpoints. Only bootstrap supplies the authoritative descriptor reference.

## Attach every verified endpoint automatically

Rejected because discovery must not mutate runtime state.

## Reuse the verification connection for attachment

Rejected because discovery and attachment have different ownership boundaries and the candidate may change between them.

## Probe all ports concurrently

Rejected initially because concurrent exclusive opens complicate cancellation and may disturb several unrelated devices.

## Require USB metadata for every candidate

Rejected because valid adapters may expose only a port name.

## Replace manual configuration with discovery

Rejected because deterministic deployments and instruments with incomplete metadata still require manual configuration.

## Add USB fields to Compact Serial Protocol Version 1

Rejected because USB metadata belongs to the host connection environment.

## Implement Windows and Linux together

Deferred. Contracts remain platform-neutral, but Windows is the first target.

---

# Deferred decisions

- Linux USB serial enumeration and physical validation;
- live USB hot-plug presence tracking;
- Added, Updated, and Removed events;
- bounded-parallel verification;
- recovery when an endpoint returns on a different port;
- persistent preferred-adapter matching;
- user-defined matching rules;
- proprietary non-HASE serial instruments;
- BLE discovery;
- authentication, authorization, and encryption;
- firmware update transport;
- endpoint-browser UI integration;
- automatic attachment policies;
- northbound runtime-host access.

---

# Final decision

HASE will discover USB serial endpoints by enumerating platform-reported serial candidates and verifying selected candidates through the existing Compact Serial Protocol bootstrap.

COM-port names, VID, PID, product names, manufacturer names, and USB serial numbers are candidate metadata only.

`CompactBootstrapResponse.EndpointId` remains authoritative HASE endpoint identity. The descriptor identifier and version returned by bootstrap remain authoritative for exact host-side descriptor resolution.

Candidate failures are isolated. Caller cancellation stops discovery. Temporary verification connections are always disposed.

Discovery does not attach, replace, publish, or mutate runtime endpoints automatically. Manual COM-port configuration remains fully supported.

The first implementation is Windows-specific behind platform-neutral contracts. Linux USB serial discovery remains explicit backlog.

Compact Serial Protocol Version 1 and Protocol Version 1 remain unchanged.
