# ADR-0019 - Local Endpoint Communication Lifecycle Ownership

- Status: Accepted
- Date: 2026-07-19

---

# Context

HASE now supports Protocol Version 1, framed TCP transport, duplex protocol sessions, runtime synchronization, automatic recovery, protocol health probing, event routing, physical ESP32 operation, and IPv4 mDNS/DNS-SD endpoint discovery.

Network discovery intentionally produces a verified endpoint inventory without creating, attaching, replacing, or operating runtime endpoints. A verified network endpoint contains a reachable candidate and the authoritative endpoint identity returned by `DiscoverResponse`.

The next capability must connect detection to operation and orderly shutdown. This requires an explicit communication lifecycle owned by the HASE runtime host.

The lifecycle must support more than one detection mechanism:

- IPv4 mDNS/DNS-SD discovery;
- manually configured TCP host names or IP addresses and ports;
- future serial-port detection and configuration;
- predefined endpoint configuration;
- vendor-specific detection through an adapter;
- future discovery of remote HASE runtime hosts.

Discovery is optional. Commercial instruments may provide a LAN or serial interface without mDNS/DNS-SD or HASE Protocol Version 1. Such instruments require manual configuration and, when they use a proprietary protocol, a HASE adapter or gateway.

Resource-constrained endpoints may be unable to store or transmit complete descriptors. HASE already supports compact descriptor references. A small endpoint must be able to declare a predefined descriptor reference whose complete descriptor is resolved by the runtime host from a repository.

One physical endpoint can contain multiple instruments. One endpoint transport connection and duplex protocol session can therefore carry operations and notifications for multiple instruments. The runtime host must remain the single owner of the receive path and distribute operations and notifications to local or future remote applications.

The intended deployment model also includes remote applications that access the API of a controlling HASE runtime host, potentially through a Tailscale network. Tailscale-based host detection and the future remote API sit above the local endpoint lifecycle. They do not replace local endpoint detection, transport ownership, synchronization, recovery, or shutdown.

The architecture must therefore distinguish:

1. detection of a HASE runtime host;
2. detection or configuration of endpoints reachable by that host;
3. the local communication lifecycle owned by that host;
4. future application access to the host's runtime model.

---

# Decision

A HASE runtime host owns the complete local communication lifecycle for every attached endpoint.

Detection mechanisms provide connection candidates or configured connection definitions. They never attach, replace, operate, or detach runtime endpoints automatically.

The local endpoint lifecycle is:

```text
Detection or configuration
    -> connection-target resolution
    -> endpoint verification or adapter probing
    -> descriptor resolution
    -> explicit attachment
    -> synchronization
    -> operation
    -> health monitoring and recovery
    -> orderly shutdown
```

## Runtime-host ownership

The runtime host owns all resources created for an attached endpoint, including:

- the transport connection and transport connection manager;
- the protocol connection or adapter session;
- the runtime endpoint;
- synchronization services;
- the connection coordinator;
- automatic recovery supervision;
- health probing;
- notification routing;
- lifecycle diagnostics.

An attachment session represents this ownership boundary. It exposes the attached runtime endpoint while retaining ownership of the resources required to operate it. The attachment session supports asynchronous orderly shutdown and disposal.

The runtime endpoint must not outlive the attachment session that owns its active communication lifecycle.

## Explicit attachment

Attachment requires an explicit application or user request.

The following actions are prohibited:

- attaching the first discovered endpoint automatically;
- replacing an existing runtime endpoint automatically;
- treating a network address as authoritative endpoint identity;
- treating a discovery service instance name as authoritative endpoint identity;
- retaining partially constructed lifecycle resources after cancellation or failure.

The initial attachment capability does not replace an existing attached endpoint. If the target runtime host already contains an attached endpoint with the same authoritative endpoint identity, the attachment request is rejected.

Endpoint replacement requires a separate future policy and explicit approval.

## Connection definitions

A connection definition describes how the runtime host can reach an endpoint. It is not endpoint identity.

Connection definitions may originate from:

- a verified discovery result;
- manually configured network settings;
- future serial-port detection;
- manually configured serial settings;
- persistent host configuration;
- an adapter-specific source.

A network connection definition may contain a host name or IP address, TCP port, transport options, and an optional expected endpoint identity.

A future serial connection definition may contain a port identity, serial settings, transport options, and an optional expected endpoint identity.

Discovered and manually configured connection definitions converge into the same explicit attachment lifecycle.

## Verification and identity

For native HASE Protocol Version 1 endpoints, the authoritative endpoint identity is obtained through:

```text
DiscoverRequest
    ->
DiscoverResponse
```

A previously verified discovery result does not eliminate verification during attachment. Attachment verifies the endpoint identity again on the connection that will enter the operational lifecycle.

If an expected endpoint identity was configured or selected, attachment fails when the connected endpoint reports a different identity.

Commercial instruments that do not implement HASE Protocol Version 1 require an adapter. The adapter defines its probe, identity, descriptor mapping, operation mapping, and compatibility checks. Manually providing a descriptor does not make a proprietary instrument understand HASE messages.

## Descriptor sources

Descriptor resolution is independent of connection-target resolution.

An endpoint attachment may use one of these descriptor sources:

- a complete descriptor returned by the endpoint;
- a compact descriptor reference returned by the endpoint and resolved from a host repository;
- a manually configured descriptor used by an approved adapter;
- a descriptor selected by persistent host configuration.

Repository descriptors must have stable identifiers and explicit versions or content identities. Attachment fails safely when a required descriptor reference is unknown, incompatible, or ambiguous.

For a compact descriptor reference:

- the endpoint is authoritative for its endpoint identity and declared descriptor reference;
- the host repository is authoritative for the complete descriptor associated with that reference;
- synchronization validates that the resolved descriptor is compatible with the endpoint and adapter profile.

Protocol Version 1 remains unchanged by this decision.

## Multiple instruments and applications

One attached endpoint may contain multiple instruments. Instrument paths and correlation identifiers allow their operations to share one endpoint protocol session.

Within one runtime host, the host owns the single endpoint receive path and routes responses and notifications to the appropriate runtime instruments and observers.

Two independent application processes must not directly share one serial port or one established TCP connection. Multiple applications instead communicate with the runtime host, which multiplexes access to its attached endpoints.

An endpoint that accepts multiple independent TCP clients may support multiple protocol sessions, but this is separate from sharing one physical connection and is not required by the initial attachment capability.

Instrument concurrency, exclusive access, operation serialization, and authorization policies remain separate concerns.

## Remote applications and Tailscale

The local endpoint lifecycle does not depend on Tailscale or another remote-network technology.

A future remote application may:

1. detect or configure a reachable HASE runtime host;
2. connect to the host's northbound API;
3. inspect the host's runtime endpoint and instrument inventory;
4. perform authorized operations through the host.

The runtime host continues to own every physical TCP or serial endpoint connection.

Tailscale-based host detection identifies reachable runtime hosts, not physical endpoint identity. Physical endpoint discovery remains local to the runtime host unless an explicit gateway architecture is introduced later.

The future northbound API may reuse HASE identities, paths, descriptors, values, and operation semantics, but it is not required to expose raw Protocol Version 1. API technology, authentication, authorization, auditing, and multi-client policy require separate decisions.

## Cancellation and failure

Cancellation applies to connection, verification, descriptor resolution, synchronization, recovery startup, and shutdown.

When attachment is cancelled or fails, every resource created by that attachment attempt is closed or disposed before the failure is returned to the caller.

An attachment is not published in the runtime host's active inventory until its required identity verification, descriptor resolution, runtime construction, and initial synchronization have completed successfully.

An operational transport failure does not immediately remove the attachment. The established recovery lifecycle may retain the runtime endpoint, preserve cached values, replace the transport, resynchronize, and return the endpoint to operation.

## Orderly shutdown

Orderly shutdown is a lifecycle operation, not merely object destruction.

Shutdown:

- prevents new operations from entering the attachment;
- cancels or completes active operations according to the applicable operation policy;
- stops health probing and automatic recovery;
- detaches notification routing;
- closes the protocol or adapter session;
- closes the transport connection;
- releases all lifecycle resources;
- removes the attachment from the active runtime inventory;
- leaves persistent connection configuration available when persistence is enabled.

Repeated shutdown or disposal must be safe.

---

# Consequences

## Positive consequences

- Discovery and manual configuration share one attachment lifecycle.
- Network addresses remain separate from authoritative endpoint identity.
- Runtime endpoint ownership and resource disposal are explicit.
- The architecture supports TCP, future serial transport, and vendor adapters.
- Compact descriptor references support resource-constrained endpoints.
- One endpoint connection can serve multiple instruments.
- A runtime host can safely multiplex endpoint access for future remote applications.
- Automatic recovery remains part of the attachment instead of creating a new endpoint identity.
- Cancellation and partial-failure cleanup have a defined ownership boundary.
- Tailscale and future remote APIs remain above the transport-independent runtime core.

## Negative consequences

- Attachment requires more than creating a `RuntimeEndpoint` object.
- The runtime host must manage attachment inventory and asynchronous lifetimes.
- Adapter-based commercial instruments require device-specific implementation and validation.
- Repository-backed descriptors require versioning, distribution, and compatibility rules.
- Multi-application operation requires a host API instead of direct shared serial-port access.
- Orderly shutdown and concurrent operations require explicit coordination.

## Neutral consequences

- Existing explicitly addressed TCP scenarios remain valid diagnostic tools.
- IPv4 mDNS/DNS-SD remains a reachability mechanism only.
- Protocol Version 1 remains unchanged.
- Existing coordinator-owned duplex session and recovery behavior remains valid.
- Live discovery presence tracking remains separate from attachment lifecycle tracking.
- Authentication and authorization remain outside local Protocol Version 1 discovery but become required concerns for a future remote API.

---

# Alternatives Considered

## Let each application own physical endpoint connections

Rejected as the general architecture.

Independent applications cannot safely share one serial port or one established duplex receive path. It would also duplicate synchronization, recovery, caching, and event routing. The runtime host owns physical connections and future applications use its northbound API.

## Attach every discovered endpoint automatically

Rejected.

Discovery advertisements provide reachability only. Automatic attachment would create resource use and runtime mutation without an explicit application decision and could conflict with an existing endpoint.

## Treat discovered and configured endpoints as separate runtime models

Rejected.

Discovery and configuration are different ways to obtain connection definitions. Once explicitly selected, both should use the same verification, descriptor resolution, attachment, operation, recovery, and shutdown lifecycle.

## Return a bare RuntimeEndpoint from attachment

Rejected.

A runtime endpoint alone does not communicate ownership of its transport, protocol session, coordinator, supervisor, health probe, notification routing, or shutdown responsibilities. An attachment session provides the required lifecycle boundary.

## Require every endpoint to provide a complete descriptor

Rejected.

This would exclude resource-constrained microcontrollers and duplicate static descriptors. Compact versioned descriptor references resolved by the host repository remain supported.

## Use Protocol Version 1 directly as the remote application API

Deferred as the default approach.

The future northbound API must support host inventory, multiple clients, cached state, authorization, diagnostics, and API evolution. It may reuse HASE semantics without being identical to the constrained endpoint protocol.

## Make Tailscale part of the local endpoint lifecycle

Rejected.

Tailscale may provide remote host reachability and host detection, but local endpoint attachment must remain usable without it and independent of a specific remote-network product.

---

# Validation

This decision is documentation-only. Implementation will proceed in small contract and behavior increments.

The first contract increment must verify:

- validation of discovered and manually configured connection definitions;
- explicit attachment-request validation;
- separation of connection origin from endpoint identity;
- descriptor-source validation;
- attachment-session ownership and asynchronous shutdown contracts;
- rejection of implicit replacement intent;
- cancellation-token propagation in lifecycle contracts.

Later implementation increments must verify:

- rediscovery followed by explicit attachment without a command-line IP address;
- manual TCP attachment without mDNS/DNS-SD;
- authoritative identity revalidation during attachment;
- rejection of an unexpected endpoint identity;
- complete cleanup after cancellation and partial failure;
- initial synchronization before inventory publication;
- recovery without creating a duplicate runtime endpoint;
- one endpoint connection operating multiple instruments;
- orderly idempotent shutdown;
- repository-backed descriptor resolution;
- future serial and adapter implementations against the same lifecycle boundary.

---

# Follow-up Work

- introduce the initial lifecycle contracts in `Hase.Runtime.Transport`;
- define the runtime host attachment inventory;
- implement attachment from a verified IPv4 discovery result;
- implement manual TCP connection definitions;
- reuse the existing coordinator, supervisor, synchronizer, and health-probe infrastructure;
- add a Protocol Explorer scenario that discovers, explicitly selects, attaches, operates, and shuts down an endpoint;
- define the descriptor repository contract before implementing constrained serial endpoints;
- define adapter contracts before integrating proprietary commercial instruments;
- implement serial transport in a later capability;
- select and design the northbound runtime API separately;
- evaluate Tailscale-based runtime-host detection as an integration above the local lifecycle;
- define authentication, authorization, auditing, and multi-client operation policy before enabling remote control.

