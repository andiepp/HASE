# ADR-0020 - Resource-Constrained Serial Endpoint Protocol

- Status: Accepted
- Date: 2026-07-20

---

# Context

HASE supports Protocol Version 1, framed TCP transport, runtime synchronization, automatic recovery, physical ESP32 operation, explicit endpoint attachment, and a runtime-host-owned attachment inventory.

ADR-0019 defines the runtime host as the owner of the complete local communication lifecycle for every attached endpoint. It separates connection resolution, endpoint identity, descriptor resolution, runtime publication, recovery, and orderly shutdown. It also permits compact, versioned descriptor references resolved through a host-side repository.

The next physical target is an Arduino Uno-class endpoint connected to the runtime host through USB serial, typically by a CH340-class USB-to-serial adapter.

An Arduino Uno has substantially less program memory, RAM, and nonvolatile storage than the ESP32 endpoint. Requiring it to implement the complete Protocol Version 1 contract or embed and transmit a complete endpoint descriptor would undermine the resource-constrained endpoint goal.

The serial capability must therefore support an endpoint that:

- uses bounded memory;
- avoids dynamic allocation in normal protocol processing;
- does not embed a complete endpoint descriptor;
- reports a compact, versioned descriptor reference;
- exposes an authoritative endpoint identity independently of the serial port and USB adapter;
- can be attached through manually configured serial settings;
- participates in the runtime-host lifecycle established by ADR-0019.

The architecture must keep four concepts separate:

1. the configured serial connection;
2. the identity of a USB-to-serial adapter;
3. the authoritative HASE endpoint identity;
4. the identity and version of the endpoint descriptor.

A Windows COM-port name is not stable endpoint identity. A Linux device path is likewise not endpoint identity. USB vendor ID, product ID, adapter serial number, and physical USB topology describe an adapter or attachment location, not necessarily the endpoint behind it.

Protocol Version 1 is frozen for its current endpoint contract. The serial endpoint must operate below Protocol Version 1 unless a later architecture decision explicitly changes that boundary.

---

# Decision

HASE will introduce a separate compact, versioned protocol for resource-constrained serial endpoints.

The compact protocol is not Protocol Version 1 and does not change Protocol Version 1. It is an endpoint protocol profile below Protocol Version 1 that maps into the same transport-independent HASE runtime model.

The first capability using this decision is C-018:

> Attach and operate a resource-constrained endpoint over a manually configured USB serial connection, using a compact versioned descriptor reference resolved through a host-side descriptor repository.

## Capability boundary

C-018 includes:

- a manually configured serial connection definition;
- a cross-platform serial transport implementation;
- compact request and response framing;
- compact endpoint bootstrap;
- authoritative endpoint identity acquisition;
- compact descriptor-reference acquisition;
- exact host-side descriptor resolution;
- explicit attachment through the runtime-host lifecycle;
- initial synchronization and readiness-gated publication;
- recovery with identity and descriptor-reference revalidation;
- orderly detachment and serial-port closure;
- physical validation with an Arduino Uno-class endpoint on Windows.

C-018 does not include:

- automatic USB-device detection;
- automatic COM-port selection;
- USB hot-plug presence tracking;
- persistent adapter-to-endpoint matching;
- Protocol Version 1 changes;
- proprietary commercial-instrument adapters;
- BLE transport;
- a northbound runtime-host API;
- Linux physical validation.

These excluded items remain independently selectable future capabilities.

## Protocol relationship

The compact serial protocol has its own version identifier and message contract.

It may express only the operations approved for the compact profile. It is not required to reproduce every Protocol Version 1 message or serialization rule.

The host adapts compact protocol operations to the existing HASE endpoint, instrument, property, command, and event model. Runtime consumers do not treat the compact endpoint as a Protocol Version 1 endpoint.

The first compact protocol version must be extensible by explicit versioning. Unsupported protocol versions fail attachment. Implementations must not guess compatibility or silently fall back to another version.

## Serial connection definition

A serial connection definition describes how the runtime host can open a serial byte stream. It is connection information, not endpoint identity.

The public connection contract uses platform-neutral concepts and can represent at least:

- a port name or operating-system device path;
- baud rate;
- data bits;
- parity;
- stop bits;
- handshake mode;
- read and write limits or timeouts where required by the transport implementation;
- an optional expected endpoint identity;
- configured connection origin.

Windows names such as `COM5` and Linux paths such as `/dev/ttyUSB0` are values supplied to the same connection contract. Platform-specific enumeration APIs do not enter the transport-independent runtime contracts.

The first physical configuration is manual. Detection and configuration may later produce the same serial connection definition, but detection never attaches an endpoint automatically.

## Identity separation

The following identities remain distinct:

### Connection identity

The configured port name and serial settings identify a connection target for one attachment attempt. They are not authoritative HASE identity.

### USB adapter identity

USB vendor ID, product ID, adapter serial number, product text, manufacturer text, device instance, and physical topology are optional detection metadata.

USB adapter identity is never substituted for endpoint identity. An adapter may be replaced while the endpoint remains logically the same, and identical adapters may expose no unique serial number.

### Endpoint identity

The endpoint reports its authoritative `EndpointId` through compact bootstrap.

If the connection definition contains an expected endpoint identity, attachment fails when the endpoint reports a different identity.

The runtime-host attachment inventory continues to use the authoritative endpoint identity. A serial port or USB adapter never becomes its key.

### Descriptor identity

The endpoint reports a compact descriptor reference containing a stable descriptor identifier and an explicit descriptor version.

Descriptor identity is not endpoint identity. Multiple physical endpoints may legitimately use the same descriptor reference.

## Compact bootstrap

Every new serial connection performs compact bootstrap before runtime construction and publication.

Bootstrap establishes at least:

- compact protocol version;
- authoritative endpoint identity;
- descriptor identifier;
- descriptor version.

Bootstrap is performed on the connection that is intended to enter the operational lifecycle. A previously observed endpoint or adapter does not eliminate bootstrap.

The host rejects bootstrap when:

- the response is malformed;
- the protocol version is unsupported;
- the endpoint identity is missing or invalid;
- an expected endpoint identity does not match;
- the descriptor reference is missing or invalid;
- the descriptor reference cannot be resolved exactly;
- the resolved descriptor is incompatible with the compact protocol profile.

No runtime endpoint is published after a rejected bootstrap.

## Descriptor repository

The complete endpoint descriptor is stored on the runtime host in a predefined descriptor repository.

Repository lookup uses the complete compact descriptor reference. Resolution is exact by descriptor identifier and descriptor version.

The repository must not:

- select the latest version implicitly;
- substitute a similarly named descriptor;
- ignore an unknown version;
- resolve an ambiguous reference;
- mutate the endpoint-supplied reference.

The endpoint is authoritative for the descriptor reference it reports. The host repository is authoritative for the complete descriptor stored under that exact reference.

The resolved descriptor contains the complete endpoint structure, including all instruments, properties, commands, events, paths, data descriptors, quantities, units, and metadata required by the runtime.

One resolved endpoint descriptor may contain multiple instruments. They share the same physical serial connection and compact protocol session.

## Framing and bounded processing

USB serial provides a byte stream and does not preserve message boundaries. The compact protocol therefore defines deterministic binary frames.

The framing contract must provide:

- an unambiguous frame boundary;
- a compact protocol version;
- a message type;
- a bounded payload length;
- request and response association sufficient for the approved operation model;
- detection of corrupted frames;
- deterministic recovery after invalid or incomplete input;
- a fixed maximum frame size.

The detailed byte layout, numeric encodings, checksum algorithm, maximum sizes, and message payloads are frozen by the first codec contract and its golden-byte tests before physical firmware implementation.

The first version favors small deterministic code over general-purpose serialization. It must not require JSON, reflection, a heap-based object graph, or storage of the complete descriptor on the endpoint.

The Arduino implementation uses fixed-size buffers selected from explicit protocol maxima. Frames exceeding those maxima are rejected.

## Operation model

The compact protocol is request/response-oriented for its first capability.

Only operations required by the first approved Arduino endpoint scenario are introduced. Additional property, command, event, streaming, or notification operations require later incremental approval but may extend the same versioned compact protocol when backward compatibility is preserved.

The host maps compact operation identifiers to paths and semantics from the resolved descriptor. The mapping is deterministic and part of descriptor/profile compatibility. The endpoint is not required to transmit full HASE paths on every operation.

Unsolicited event delivery is not required by the first C-018 increment. If added later, the serial session must still have exactly one owner of the receive path.

## Runtime-host lifecycle

Compact serial attachment follows ADR-0019:

```text
Manual configuration
    -> serial connection-target resolution
    -> open serial connection
    -> compact bootstrap
    -> endpoint identity validation
    -> exact descriptor resolution
    -> runtime construction
    -> initial synchronization
    -> readiness-gated publication
    -> operation and health monitoring
    -> recovery or orderly shutdown
```

The attachment session owns:

- the serial transport connection;
- the compact protocol session;
- the resolved descriptor association;
- the runtime endpoint;
- synchronization and operation mapping;
- recovery supervision;
- diagnostics;
- orderly shutdown.

The runtime endpoint must not outlive the attachment session that owns its serial connection and compact protocol session.

## Recovery and revalidation

Transport loss retains the established runtime endpoint and cached values while recovery is attempted according to the runtime-host policy.

Recovery reopens the configured serial connection and repeats compact bootstrap.

The replacement connection is accepted only when:

- the compact protocol version remains supported;
- the authoritative endpoint identity matches the attached runtime endpoint;
- the descriptor identifier and descriptor version match the attached descriptor reference;
- required compatibility checks succeed;
- initial resynchronization succeeds.

A different endpoint appearing on the same COM port is not an automatic replacement. Identity or descriptor-reference mismatch faults the recovery attempt and never mutates the existing attachment into a different endpoint.

## Concurrency and ownership

The runtime host is the sole owner of an attached serial port.

Independent applications must not open or share that physical port directly. They access instruments through the runtime host and its future northbound API.

Operations sharing one serial endpoint are serialized or correlated according to the compact session contract. Multiple instruments do not create multiple physical serial connections.

## Cross-platform requirement

Windows is the first physical validation platform. Linux compatibility is an architectural requirement from the start.

Public contracts must not expose Windows Management Instrumentation, Windows device-instance identifiers, registry paths, or other Windows-only types.

Platform-specific port enumeration and USB metadata acquisition belong behind optional detection adapters. The serial transport implementation uses a .NET serial abstraction available on supported Windows and Linux runtimes.

Linux physical validation remains a separate backlog item and is not required to complete the first Windows C-018 validation.

## Cancellation, failure, and shutdown

Connection opening, bootstrap, repository resolution, synchronization, operation, recovery, and shutdown are cancellation-aware.

After cancellation or failure, all resources created by the attachment attempt are closed or disposed before the error is returned.

Orderly shutdown:

- prevents new operations;
- stops recovery and health monitoring;
- completes or cancels active compact exchanges according to policy;
- closes the compact protocol session;
- closes and disposes the serial port;
- removes the endpoint from the runtime and attachment inventories;
- leaves the endpoint in the `Disconnected` state;
- is safe when repeated.

---

# Consequences

## Positive consequences

- Arduino Uno-class endpoints do not store or transmit complete descriptors.
- Protocol Version 1 remains unchanged and transport-independent.
- The compact endpoint protocol can be implemented with bounded buffers and a small firmware footprint.
- Manual Windows COM-port configuration and Linux device paths use one architectural contract.
- Serial connection, USB adapter, endpoint, and descriptor identities remain separate.
- Exact descriptor resolution prevents accidental or silent descriptor substitution.
- Compact endpoints enter the existing runtime model and attachment inventory.
- Multiple instruments can share one serial connection.
- Recovery cannot silently replace an endpoint when a different device appears on the same port.
- Future automatic USB detection can converge on the same explicit attachment path.

## Negative consequences

- HASE will maintain a second endpoint protocol in addition to Protocol Version 1.
- The host requires a descriptor repository before compact endpoints can attach.
- Compact protocol codecs and firmware require separate compatibility tests.
- Compact protocol capabilities may initially be narrower than Protocol Version 1.
- Serial connections generally allow only one local process owner.
- Endpoint and descriptor identity must be revalidated after every reconnection.

## Risks

- An underspecified compact protocol could become difficult to extend.
- Excessive framing or identifier overhead could defeat the small-footprint goal.
- Descriptor/profile mismatch could map compact operation identifiers incorrectly.
- Some CH340-class adapters do not expose a unique USB serial number.
- Operating systems may assign a different port name after reconnection.
- Serial noise, reset behavior, and bootloader traffic may produce incomplete or invalid input.

These risks are controlled through explicit versioning, golden-byte codec tests, bounded frame sizes, exact descriptor lookup, compatibility validation, resynchronization, and strict identity separation.

---

# Implementation sequence

Implementation proceeds in small, buildable increments:

1. manual serial connection contracts and validation tests;
2. serial transport framing and transport-level tests without runtime integration;
3. compact protocol primitives, bootstrap messages, codecs, and golden-byte tests;
4. host descriptor-repository contract and exact-resolution tests;
5. compact bootstrap and descriptor-resolution integration;
6. runtime attachment, synchronization, recovery, and inventory integration;
7. Protocol Explorer C-018 scenario;
8. minimal Arduino Uno firmware;
9. physical Windows validation through a CH340-class adapter;
10. documentation of the completed capability.

The first code increment introduces manual serial connection contracts only. It performs no serial I/O and introduces no compact wire encoding.

---

# Alternatives considered

## Use Protocol Version 1 unchanged on Arduino Uno

Rejected because the complete Protocol Version 1 implementation, general serialization, full paths, and descriptor handling impose unnecessary program-memory and RAM cost on the resource-constrained target.

## Embed the complete descriptor in the endpoint

Rejected because it consumes scarce nonvolatile storage and requires the endpoint to transmit and serialize metadata already maintained by the host repository.

## Treat the COM port as endpoint identity

Rejected because port assignment is operating-system connection information and can change independently of the endpoint.

## Treat the CH340 adapter as endpoint identity

Rejected because the adapter is not the endpoint, may be replaced, and may not expose a unique serial number.

## Select a descriptor manually without endpoint declaration

Rejected for the native compact endpoint path because it can silently associate the wrong descriptor with the connected firmware. Manually configured descriptors remain valid only for separately approved adapter scenarios.

## Extend Protocol Version 1 with a compact mode

Rejected for C-018 because Protocol Version 1 is frozen and the constrained profile has different memory, framing, and operation requirements. A later ADR may define negotiation or convergence if experience justifies it.

## Include automatic USB detection in C-018

Rejected because detection, USB metadata, matching policy, and hot-plug presence are independent of serial transport and compact endpoint operation. Manual configuration establishes the smallest complete physical capability.

---

# Deferred decisions

The following require later increments or architecture decisions:

- automatic USB and CH340 detection;
- adapter-to-endpoint matching and persistence;
- hot-plug presence tracking;
- Linux physical validation;
- compact unsolicited events;
- compact streaming;
- compact bulk operations;
- firmware update transport;
- authentication and authorization;
- encryption;
- proprietary serial-instrument adapters;
- gateway routing;
- northbound multi-application access.

---

# Final decision

HASE will support Arduino Uno-class USB serial endpoints through a separate compact, versioned resource-constrained protocol below Protocol Version 1.

The endpoint reports authoritative endpoint identity and an exact versioned descriptor reference. The runtime host resolves the complete descriptor from a predefined repository and owns the complete attachment, synchronization, operation, recovery, and shutdown lifecycle.

Serial connection identity, USB adapter identity, endpoint identity, and descriptor identity remain separate. Manual serial configuration is included in C-018; automatic USB-device detection is deferred.
