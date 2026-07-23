# ADR-0023 - Northbound Runtime-Host API Boundary

- Status: Accepted
- Date: 2026-07-23

---

# Context

HASE now owns the complete local communication lifecycle for attached physical
endpoints.

The runtime host provides:

- explicit native Protocol Version 1 and Compact Serial Protocol attachment;
- an authoritative endpoint attachment inventory;
- descriptor-driven runtime endpoints and instruments;
- synchronized Property caches;
- Property reads and writes;
- Command execution;
- Event notification;
- connection supervision and health probing;
- automatic recovery and resynchronization;
- cached-value preservation during temporary disconnection;
- stable runtime Event observers across physical connection replacement;
- orderly endpoint detachment and runtime-host shutdown.

ADR-0019 establishes that the runtime host remains the sole owner of every
physical endpoint connection, protocol or adapter session, synchronization
service, recovery supervisor, notification route, and attachment lifetime.

Applications currently interact with the runtime through in-process runtime
objects and capability-specific composition. This is insufficient for
applications running in another process or on another computer.

The intended deployment model includes remote applications that reach a HASE
runtime host through a LAN or a private network such as Tailscale. Those
applications must use the runtime model hosted on the controlling computer
without opening, sharing, replacing, or supervising the physical endpoint
connections themselves.

Tailscale node discovery is a separate future concern. It can make runtime hosts
reachable or discoverable, but it does not define the runtime-host application
contract and does not replace API authentication or authorization.

The northbound boundary must also hide differences between native Protocol
Version 1 endpoints and Compact Serial Protocol endpoints. Applications operate
on HASE endpoint, instrument, Property, Command, and Event semantics regardless
of the southbound transport or protocol.

The existing runtime object graph is not itself a remote contract:

- runtime objects contain process-local references and observer registrations;
- public mutation methods exist for trusted internal runtime integration;
- native and compact operational paths use different internal collaborators;
- attachment sessions expose lifecycle-owning resources that remote
  applications must not control directly;
- CLR types and constructor signatures are not a stable interoperable wire
  format.

A dedicated application-service boundary is therefore required before choosing
a remote API technology or encoding.

---

# Decision

The northbound runtime-host API is a Phase 7 application and service boundary.

Phase 6 ends with the completed local transport, protocol, physical endpoint,
attachment, supervision, recovery, and shutdown capabilities through C-025.
Optional additional transports, discovery mechanisms, compact profiles, and
platform integrations do not prevent Phase 7 from beginning.

The northbound API exposes immutable representations of runtime-host state and
explicit application operations. It does not expose the mutable in-process
runtime graph, physical endpoint transports, southbound protocol objects, or
attachment-session ownership.

The conceptual dependency direction is:

```text
Local or remote application
    -> northbound runtime-host API
        -> runtime-host application services
            -> authoritative attachment inventory
            -> runtime model
            -> host-owned operation routing
                -> native or compact endpoint integration
```

The runtime host remains the sole owner of every physical endpoint lifecycle.

## Transport-independent application services

The first northbound implementation defines transport-independent application
service contracts.

These contracts:

- represent host, endpoint, instrument, Property, Command, Event, and connection
  state using immutable snapshots or operation results;
- normalize native and compact endpoint behavior;
- route every active operation through the runtime host;
- preserve device-authoritative Property semantics;
- support cancellation;
- express expected operational failures without exposing transport-specific
  exceptions as the public contract;
- remain independent of ASP.NET, gRPC, HTTP, JSON, WebSocket, Tailscale, or
  another remote API technology.

A later adapter maps these services to the approved remote wire technology.

## Authoritative publication source

The authoritative runtime-host attachment inventory is the source of endpoints
published northbound.

An endpoint is visible through the operational northbound API only after:

- authoritative identity verification;
- descriptor resolution;
- runtime construction;
- initial synchronization;
- successful attachment publication in the host inventory.

Unpublished staged runtime endpoints, failed attachment attempts, discovery
candidates, and manually configured connection definitions are not operational
inventory entries.

## Host identity and API compatibility

The northbound API exposes:

- a stable runtime-host identity;
- a northbound API contract version;
- the capabilities supported by that host API instance.

Runtime-host identity identifies the controlling host. It does not identify a
physical endpoint and must not replace `EndpointId`.

Wire-version negotiation and compatibility rules are defined with the selected
remote API technology in a later decision.

## Endpoint and attachment identity

Every published endpoint representation contains:

- authoritative `EndpointId`;
- an opaque attachment generation identifier;
- endpoint descriptor information;
- current connection status.

`EndpointId` identifies the physical endpoint.

The attachment generation identifier identifies one published attachment
lifetime on one runtime host. It changes when an attachment ends and another
attachment is later published, even when the authoritative `EndpointId` is
unchanged.

All state-changing operations and explicit live reads identify both the
authoritative endpoint and the expected attachment generation.

The runtime host rejects an operation when the supplied attachment generation
is no longer current. An operation from an ended attachment must never be
silently applied to a later attachment with the same `EndpointId`.

The attachment generation identifier is opaque to clients. It is not a
transport address, correlation identifier, descriptor version, or endpoint
identity.

## Inventory and descriptor queries

The operational API exposes:

- a snapshot of currently published endpoint attachments;
- lookup by authoritative `EndpointId`;
- attachment generation;
- endpoint connection state and its UTC change timestamp;
- immutable endpoint and instrument descriptor representations;
- the Properties, Commands, and Events exposed by each instrument;
- current cached Property values when known.

Descriptor and runtime representations are API data contracts. They are not
remote references to mutable CLR runtime objects.

Connection detail may be exposed only as safe diagnostic text. Applications
must not use diagnostic text for program logic.

## Property cache semantics

For every Property, the API may expose:

- descriptor identity and metadata;
- the current cached `PropertyValue`, when known;
- value;
- UTC value timestamp;
- quality;
- current endpoint connection state;
- whether the value is known.

The device remains authoritative.

A cached value can remain available while an endpoint is disconnected or
recovering. Returning that cached value does not assert that a live endpoint
read succeeded.

The API must keep these operations distinct:

- querying the host's current cached representation;
- requesting an explicit endpoint read.

## Explicit Property reads

An explicit Property read:

- identifies endpoint, attachment generation, instrument, and Property;
- is accepted only for the current attachment generation;
- is routed through the host-owned endpoint operation path;
- returns the authoritative endpoint result;
- updates the runtime cache only through the existing synchronization and
  authority rules;
- fails explicitly when the endpoint is unavailable, the request is invalid,
  the operation is unsupported, or the attachment is no longer current.

## Property writes

A Property write:

- identifies endpoint, attachment generation, instrument, and Property;
- is validated against the descriptor and Property access rules;
- is routed through the host-owned endpoint operation path;
- does not expose native or compact wire identifiers;
- does not update the runtime cache merely because the application submitted a
  value;
- updates the runtime cache only after endpoint confirmation or another
  protocol-defined authoritative result;
- reports validation, availability, cancellation, timeout, endpoint, and
  stale-attachment failures explicitly.

## Command execution

A Command invocation:

- identifies endpoint, attachment generation, instrument, and Command path;
- carries the argument defined by the Command descriptor;
- belongs to exactly one attachment generation;
- is routed and correlated by the runtime host;
- returns success, optional result, or an explicit failure;
- must not be completed by a response belonging to another attachment
  generation.

The northbound API does not expose the endpoint protocol correlation
identifier.

## Property and lifecycle subscriptions

The API supports live observation of:

- endpoint attachment and detachment;
- endpoint connection-status changes;
- Property-value changes;
- replacement of a published attachment generation.

A client first obtains an inventory snapshot and then establishes live
subscriptions according to the consistency rules of the selected remote API
mapping.

After a northbound connection is lost, the client obtains a new snapshot before
resuming live observation. Initial Phase 7 semantics do not require durable
notification replay.

## Event subscriptions

The API supports live Event subscriptions addressed by:

- authoritative `EndpointId`;
- attachment generation;
- `InstrumentId`;
- Event path.

Each delivered occurrence contains:

- source identity;
- UTC timestamp;
- optional Event value.

The northbound API preserves the existing runtime Event semantics:

- Event occurrences are transient;
- there is no offline Event queue;
- there is no reconnect replay;
- a client disconnected from the northbound API may miss Event occurrences even
  while the physical endpoint remains attached;
- persistent Event history is a separate future service.

The host continues to own the single physical endpoint receive path and
multiplexes Event delivery to local and remote observers.

## Multi-client operation

Multiple applications may concurrently:

- inspect the published inventory;
- query descriptors and cached Property values;
- observe lifecycle and Property changes;
- subscribe to Events;
- submit authorized endpoint operations.

Applications never acquire or share the physical endpoint connection.

The runtime host owns operation routing and any endpoint-specific
serialization. The initial API provides:

- no implicit client-exclusive lock;
- no distributed transaction across endpoints;
- no automatic retry of a state-changing client operation after an ambiguous
  result;
- no guarantee that concurrent writes or Commands from different clients form
  an atomic sequence.

Leases, exclusive access, operation priorities, idempotency keys, and
cross-operation transactions require separate future decisions.

## Operational and management APIs

The northbound boundary distinguishes two service groups.

The operational API covers:

- published inventory;
- descriptors;
- connection status;
- cached Property values;
- explicit Property reads;
- Property writes;
- Command execution;
- lifecycle and Property subscriptions;
- Event subscriptions.

The management API may later cover:

- discovery;
- connection definitions;
- endpoint attachment and detachment;
- persistent host configuration;
- descriptor-repository administration;
- endpoint replacement policy;
- host shutdown.

The initial Phase 7 capability implements the operational API only.

Remote attachment, detachment, replacement, discovery, and configuration are
not part of the first northbound capability. They require separately approved
authorization and lifecycle-management semantics.

## Security boundary

Network reachability does not grant HASE authority.

Tailscale, a LAN, localhost, or another network mechanism determines how a
client reaches the runtime host. The northbound API independently authenticates
the client and authorizes requested operations before non-local deployment.

The authorization model must be able to distinguish at least:

- host access;
- endpoint visibility;
- Property observation;
- explicit Property reads;
- Property writes;
- Command execution;
- Event subscription;
- future lifecycle administration.

The precise authentication mechanism, credential lifecycle, encryption
requirements, authorization policy representation, and audit model are decided
with the remote API technology before exposing the service beyond a trusted
local development environment.

Tailscale node discovery remains separate from both the physical endpoint
discovery model and the northbound API contract.

## Explicitly excluded surface

The northbound API does not expose:

- physical TCP connections;
- serial ports or serial byte streams;
- raw endpoint transport frames;
- Protocol Version 1 messages;
- Compact Serial Protocol messages;
- native or compact correlation identifiers;
- compact Property, Command, or Event identifiers;
- protocol sessions;
- connection coordinators;
- recovery supervisors;
- health probes;
- notification routers;
- `IEndpointAttachmentSession`;
- mutable `RuntimeEndpoint`, `RuntimeInstrument`, `RuntimeProperty`,
  `RuntimeCommand`, or `RuntimeEvent` references;
- direct calls to runtime mutation methods such as Property cache updates or
  Event publication;
- direct access to `IInstrumentExecutor`;
- descriptor-repository implementation details;
- automatic endpoint attachment or replacement;
- direct physical connection sharing;
- persistent Event history;
- distributed endpoint transactions;
- client-controlled physical recovery.

Safe, structured diagnostics may be added through a separately defined
diagnostic surface without exposing lifecycle-owning components.

## Southbound protocol independence

The northbound API is not Protocol Version 1 and is not Compact Serial Protocol.

It may reuse HASE identities, descriptor semantics, paths, values, qualities,
timestamps, operation meanings, and result concepts. It does not expose the
southbound wire contracts or require remote applications to emulate physical
endpoints.

Protocol Version 1 and Compact Serial Protocol remain unchanged by this
decision.

## Remote API technology

This decision freezes the semantic northbound boundary but does not select its
wire technology.

The remote mapping must support:

- typed request and response operations;
- cancellation and timeouts;
- server-to-client streaming or an equivalent subscription mechanism;
- explicit API compatibility;
- cross-platform clients;
- authentication and authorization;
- operation and lifecycle error mapping;
- bounded resource use for slow or disconnected clients.

Candidate technologies may include gRPC over HTTP/2, HTTP/JSON with an approved
streaming mechanism, or another explicitly evaluated mapping.

The chosen technology must adapt to the transport-independent application
services rather than becoming a dependency of the runtime core.

---

# Consequences

## Positive consequences

- Local and remote applications use one normalized runtime-host contract.
- The runtime host remains the sole owner of physical endpoint lifecycles.
- Native and compact southbound differences remain hidden from applications.
- Stale operations cannot silently cross attachment lifetimes.
- Cached Property access remains distinct from authoritative live reads.
- Existing device-authoritative cache semantics are preserved.
- Existing no-queue and no-replay Event semantics remain explicit.
- Multiple applications can share one host-owned endpoint connection safely.
- Wire technology can be selected without coupling it to the runtime core.
- Operational access can be implemented before sensitive remote lifecycle
  administration.
- Tailscale remains an optional reachability and discovery mechanism.

## Negative consequences

- A new application-service layer is required before hosting a remote API.
- Native and compact operations must converge behind normalized host services.
- Attachment generation becomes part of every active operation contract.
- Remote subscriptions require explicit snapshot, reconnect, and resource
  management rules.
- Authentication, authorization, compatibility, and audit decisions are
  required before production remote exposure.
- Applications cannot manipulate transport or attachment internals directly.

## Neutral consequences

- Existing in-process runtime APIs remain valid internal and local composition
  tools.
- Existing Protocol Explorer scenarios remain diagnostic applications rather
  than the remote API contract.
- Phase 6 optional transport and discovery backlog can continue independently.
- Protocol Version 1 remains feature complete for the current endpoint
  contract.
- Compact Serial Protocol Version 1 remains separate from Protocol Version 1.
- Tailscale runtime-host discovery remains future work.

---

# Initial Phase 7 implementation sequence

The approved incremental sequence is:

1. define transport-independent northbound snapshot and identity contracts;
2. define the host inventory query service;
3. define normalized Property read and write services;
4. define normalized Command execution;
5. define lifecycle, Property, and Event observation services;
6. validate native and compact endpoints through the same in-process
   application-service contract;
7. select and document the remote wire technology;
8. implement the remote host and client mapping;
9. add authentication, authorization, and audit behavior required for remote
   exposure;
10. address Tailscale runtime-host discovery separately.

Each increment remains independently buildable and testable.
