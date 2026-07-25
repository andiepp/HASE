# ADR-0030 - Northbound Remote API Mapping

- Status: Accepted
- Date: 2026-07-25

---

# Context

ADR-0019 assigns complete physical endpoint communication lifecycle ownership
to the runtime host.

ADR-0023 defines the transport-independent northbound runtime-host application
boundary.

ADR-0024 through ADR-0026 define:

- immutable runtime-host and published endpoint snapshots;
- stable runtime-host identity;
- opaque attachment generations;
- authoritative inventory projection;
- identity resolution and file-based identity persistence.

ADR-0027 defines normalized northbound Property operations.

ADR-0028 defines normalized northbound Command execution.

ADR-0029 defines normalized northbound live observation with:

- an authoritative initial snapshot;
- an exact subscription-local sequence boundary;
- immutable generation-bound observations;
- bounded independent subscriptions;
- explicit observation-gap termination;
- transient Event occurrences with no offline queue or replay.

Those application services are implemented and physically verified for Native
Protocol Version 1 and Compact Serial Protocol Version 1 endpoints. The
verified baseline before this ADR is:

```text
Commit 4b91dbccb8a8bfa18010e1c233406a4aa3bcb7fe
2,212 automated tests pass
.NET solution builds
Phase 7.5 northbound live observation is complete
Physical C-028 validation succeeds for ESP32 and Arduino Uno
```

Phase 7.7 must make the completed application-service boundary available to a
separate application process without changing the southbound endpoint model or
transferring endpoint lifecycle ownership.

The remote mapping must support:

- unary runtime-host snapshot retrieval;
- unary cached and authoritative Property reads;
- unary Property writes;
- unary Command execution;
- server-streaming live observation;
- one authoritative initial snapshot and sequence boundary as the first
  observation-stream message.

The remote contract must remain independent of:

- mutable runtime objects;
- endpoint transports and protocol sessions;
- native and compact southbound wire formats;
- discovery candidates and attachment definitions;
- physical endpoint lifecycle administration;
- authentication-provider policy;
- Tailscale node discovery.

The transport-independent application services remain authoritative. The
remote API is an adapter over those services, not a second runtime model.

Remote exposure introduces a security boundary. Authentication, authorization,
encryption, credential lifecycle, audit behavior, and production non-local
binding have not yet been decided. Until a separate security ADR is accepted,
the implementation must be reachable only through loopback interfaces.

---

# Decision

HASE will map its completed northbound runtime-host application services to
ASP.NET Core gRPC over HTTP/2.

The mapping will use versioned protobuf contracts and generated gRPC service
types.

The first public protobuf package is version 1. Versioning is explicit in the
protobuf package, generated CLR namespace, and service surface. Contract
evolution must follow protobuf compatibility rules.

The remote API provides:

```text
RuntimeHostRemoteApi V1
    GetSnapshot
    ReadCachedProperty
    ReadAuthoritativeProperty
    WriteProperty
    ExecuteCommand
    Observe
```

The final protobuf messages and CLR adapter signatures will be introduced in
small reviewed increments. They must preserve the semantics in this ADR and in
ADR-0023 through ADR-0029.

## Technology boundary

ASP.NET Core owns:

- HTTP/2 request handling;
- gRPC service activation;
- request cancellation propagation;
- server-stream writing;
- endpoint binding;
- process-level hosting and shutdown integration.

Generated protobuf and gRPC types own the remote wire contract only.

The remote adapter owns:

- validation of remote request structure;
- conversion from protobuf messages to immutable application-service targets;
- invocation of the existing northbound application services;
- conversion from normalized application results to protobuf responses;
- conversion from normalized observations to server-stream messages;
- safe mapping of expected service outcomes to stable remote result values.

The existing northbound application services remain authoritative for:

- inventory and runtime-host snapshots;
- attachment-generation validation;
- Property reads and writes;
- Command execution;
- live observation sequencing;
- initial snapshot coordination;
- bounded observation buffering;
- observation-gap detection;
- subscription disposal.

The remote adapter must not duplicate those responsibilities.

## Service organization

Version 1 uses one cohesive runtime-host gRPC service.

The service contains unary operations for snapshot, Property, and Command
access, plus one server-streaming observation operation.

This grouping reflects one runtime-host application boundary. It does not
combine endpoint lifecycle administration with operational access because
lifecycle administration is excluded.

The protobuf service name and package are versioned. Later incompatible APIs
must use a new protobuf package and service version rather than silently
changing version 1 semantics.

## Runtime-host snapshot

`GetSnapshot` is unary.

Its response maps the complete immutable `RuntimeHostSnapshot`, including:

- API contract version;
- stable runtime-host identity;
- runtime-host display name when present;
- capture timestamp;
- every immutable published endpoint snapshot;
- authoritative `EndpointId`;
- opaque attachment generation;
- immutable endpoint descriptor;
- captured normalized connection status.

The remote response must not expose mutable runtime objects, attachment
sessions, operation ports, observer registrations, transports, or protocol
connections.

Repeated fields preserve the order supplied by the authoritative application
snapshot. Clients must not infer lifecycle ownership from that order.

## Property operations

Property operations are unary and map one-for-one to the existing normalized
Property service:

```text
ReadCachedProperty
    -> IRuntimeHostPropertyService cached read

ReadAuthoritativeProperty
    -> IRuntimeHostPropertyService authoritative read

WriteProperty
    -> IRuntimeHostPropertyService endpoint-confirmed write
```

Every Property target carries:

- authoritative `EndpointId`;
- opaque attachment generation;
- `InstrumentId`;
- logical Property path.

The adapter must preserve all normalized Property result statuses. It must not
replace application outcomes with diagnostic-text parsing.

Expected normalized outcomes are returned in the protobuf response. They are
not represented as transport failures.

Cancellation is propagated to the application service. Cancellation does not
create a Property result.

The adapter:

- does not locate endpoints independently;
- does not maintain another attachment-generation mapping;
- does not read or write a southbound protocol directly;
- does not update the Property cache itself;
- does not retry an ambiguous operation;
- does not infer a Property value from a Command result.

## Command operations

`ExecuteCommand` is unary and maps one-for-one to the existing normalized
Command service.

Every Command target carries:

- authoritative `EndpointId`;
- opaque attachment generation;
- `InstrumentId`;
- logical Command path.

The optional Command argument and optional return value use the common
versioned remote value representation.

The adapter preserves all normalized Command result statuses.

Expected endpoint rejection, attachment-generation mismatch, unsupported
argument, unavailable endpoint, endpoint failure, and timeout outcomes are
returned as stable protobuf response values. They are not inferred from gRPC
status text.

Cancellation is propagated to the application service and remains distinct
from the normalized timed-out result.

Command execution remains exactly once from the remote adapter's perspective.
The adapter never automatically retries a submitted Command after timeout,
connection loss, cancellation, or an ambiguous response.

The adapter never speculatively updates Property caches.

## Remote value representation

Version 1 defines one explicit protobuf value union for values crossing the
remote boundary.

The union is closed for version 1 and uses protobuf `oneof`.

Every supported normalized application value must map deterministically to one
union member. Unsupported CLR values fail safely at the adapter boundary and
must never be serialized through CLR type names, JSON fallbacks, reflection
metadata, or culture-dependent text.

Absence and an explicit supported value are distinct.

Numbers, timestamps, identifiers, descriptors, and paths use stable,
culture-independent protobuf representations.

UTC timestamps use protobuf timestamp semantics and are validated during
mapping. Local or unspecified `DateTime` interpretation must not be invented by
the adapter.

Attachment generations remain opaque. A client may return a received
generation in a later target, but must not construct meaning from its encoding.

The exact set of version 1 value members is introduced only after inspecting
the values already accepted and returned by the existing Property, Command,
snapshot, and observation contracts.

## Observation streaming

`Observe` is a server-streaming RPC.

The first response message always contains:

- the authoritative initial `RuntimeHostSnapshot`;
- the exact subscription-local snapshot sequence boundary.

Later response messages contain exactly one normalized observation each.

The stream shape is:

```text
Observe request
    -> InitialSnapshot message
    -> Observation message
    -> Observation message
    -> ...
```

The first message is not optional. An observation must never be written before
the initial snapshot message.

The remote adapter opens exactly one
`IRuntimeHostObservationService` subscription for one `Observe` call. It uses
the returned initial snapshot and snapshot sequence from that subscription. It
must not obtain a separate snapshot before or after opening the subscription.

The adapter preserves:

- subscription-local monotonically increasing sequence;
- snapshot sequence boundary;
- observation kind;
- authoritative `EndpointId`;
- attachment generation;
- immutable normalized payload;
- host observation order.

Sequence values remain meaningful only within one stream subscription. They
are not durable cursors, host-wide positions, replay tokens, or values that may
be compared across subscriptions.

The client cannot request replay from an earlier sequence.

## Observation payloads

Version 1 maps every ADR-0029 observation kind explicitly:

```text
AttachmentPublished
AttachmentEnded
ConnectionStatusChanged
PropertyValueChanged
EventOccurred
```

The protobuf observation uses an explicit kind plus a payload union whose
member must agree with that kind.

Malformed or internally inconsistent combinations are programming or mapping
failures. They must not be forwarded as valid observations.

Payloads preserve the normalized application contract:

- `AttachmentPublished` carries the complete immutable published endpoint
  snapshot;
- `AttachmentEnded` carries the ended attachment identity and UTC end time;
- `ConnectionStatusChanged` carries previous and current normalized statuses;
- `PropertyValueChanged` carries instrument identity, logical Property
  identity, previous known value when available, and current authoritative
  runtime-cache value;
- `EventOccurred` carries instrument identity, logical Event path, UTC
  occurrence timestamp, and optional Event value.

No payload exposes southbound protocol messages, correlation identifiers,
compact byte identifiers, runtime observer objects, or endpoint connections.

## Backpressure and observation gaps

ADR-0029 bounded buffering remains authoritative.

The gRPC adapter does not introduce an unbounded intermediate observation
queue.

The adapter reads from the existing bounded application subscription and
awaits the HTTP/2 stream writer. A slow remote client therefore consumes its
subscription slowly and may cause that application subscription to enter its
defined terminal observation-gap state.

When the application subscription reports an observation gap:

- no later observation is written;
- the gRPC stream terminates explicitly;
- the client must open a new `Observe` call;
- the new stream begins with a fresh authoritative initial snapshot;
- lifecycle and Property state are recovered from that snapshot;
- Event occurrences lost during the gap are not replayed.

The remote mapping must distinguish an observation gap from orderly
cancellation and ordinary server shutdown.

No gRPC write may block runtime observer callbacks, endpoint routing,
supervision, inventory mutation, or another subscription. Runtime callbacks
enqueue only through the existing ADR-0029 service.

## Cancellation and disposal

The gRPC request cancellation token is propagated to every application-service
operation.

For unary RPCs, cancellation ends that call and does not synthesize a
normalized success or failure result.

For `Observe`, client cancellation:

- cancels stream enumeration;
- disposes the one application observation subscription owned by that RPC;
- releases RPC-owned resources;
- does not detach, replace, stop, or dispose any endpoint;
- does not affect another observation subscription.

Server shutdown cancels active RPCs and disposes their subscriptions before
the gRPC host completes shutdown.

Disposal must be deterministic and idempotent.

## Error mapping

Version 1 distinguishes:

- normalized application outcomes;
- invalid remote requests;
- cancellation;
- observation-gap termination;
- unavailable or shutting-down remote host;
- unexpected server defects.

Normalized Property and Command outcomes are response data.

Invalid protobuf request structure is reported as a stable gRPC invalid-request
failure without invoking the application operation.

Client cancellation uses gRPC cancellation semantics.

An observation gap uses one documented terminal gRPC failure mapping and never
appears as orderly end-of-stream.

Unexpected server defects are not converted into successful responses or
fabricated endpoint results. Remote diagnostics must remain safe and must not
expose stack traces, file paths, credentials, transport internals, raw protocol
frames, or sensitive host configuration.

The exact gRPC status mapping is defined and tested with the first service
adapter implementation.

## Contract versioning

Version 1 protobuf contracts follow these compatibility rules:

- package and generated CLR namespace include version 1;
- existing field numbers are never reused;
- removed fields are reserved by name and number;
- new fields use new numbers;
- existing field meaning is not changed incompatibly;
- enum zero values represent an explicit unspecified value where required by
  protobuf compatibility;
- existing enum numeric values are never reassigned;
- `oneof` membership changes are reviewed for compatibility;
- RPC request and response types remain explicit;
- incompatible behavior requires a new API version.

The existing runtime-host API contract version remains application data and is
included in snapshot mapping. It is not replaced by the protobuf package
version.

Generated protobuf types do not become the internal northbound application
model. Mapping remains explicit in both directions.

## Hosting and dependency direction

The approved dependency direction is:

```text
Remote application
    -> HTTP/2
    -> versioned gRPC contract
    -> ASP.NET Core gRPC adapter
    -> Hase.Runtime.Northbound application services
    -> authoritative attachment projection
    -> runtime model and host-owned operation routing
    -> native or compact endpoint integration
```

The transport-independent `Hase.Runtime.Northbound` project must not depend on
ASP.NET Core, gRPC, generated protobuf types, or the remote host project.

Remote contract and hosting projects may depend inward on the northbound
application boundary as required. Dependencies must not point from runtime,
transport, protocol, or endpoint projects toward the gRPC host.

The ASP.NET Core host is a composition edge. It does not become an endpoint
runtime, discovery service, or attachment inventory.

## Loopback-only binding

Until a separate security ADR is accepted, every executable gRPC host
configuration must bind only to loopback addresses.

Permitted bindings are:

```text
127.0.0.1
::1
localhost when resolved and constrained to loopback
```

Binding to wildcard, LAN, VPN, Tailscale, public, container-external, or other
non-loopback interfaces is forbidden in this phase.

Examples of forbidden bindings include:

```text
0.0.0.0
::
machine LAN addresses
Tailscale addresses
public host names or addresses
```

The implementation must enforce the loopback restriction in code and verify it
with automated tests. Configuration alone is not trusted to preserve this
boundary.

HTTP/2 may initially use cleartext loopback transport where required for local
development and integration verification. This is not approval for cleartext
non-loopback transport.

No documentation or sample may instruct users to bypass the loopback
restriction.

## Lifecycle ownership

The remote API exposes operational access only.

It does not expose RPCs to:

- discover endpoints;
- attach endpoints;
- detach endpoints;
- replace attachments;
- create or dispose endpoint connections;
- change supervision or retry policy;
- start or stop endpoint protocols;
- shut down the runtime host.

The runtime host remains the sole owner of:

- discovery;
- attachment selection;
- endpoint attachment and detachment;
- transport and protocol connections;
- compact mappings;
- synchronization;
- connection supervision and recovery;
- connection replacement;
- Property and Command operation ports;
- Event routing;
- attachment shutdown and disposal.

Closing a channel, cancelling an RPC, or disposing a remote client never
changes physical endpoint lifecycle ownership.

## Security boundary

This ADR does not approve production remote exposure.

Before any non-loopback binding, a separate accepted security ADR must define:

- runtime-host and client authentication;
- authorization model and operation permissions;
- transport encryption and certificate or key trust;
- credential enrollment, storage, rotation, revocation, and recovery;
- audit events, contents, retention, and privacy;
- safe diagnostic policy;
- denial-of-service and resource-limit policy;
- deployment and update assumptions;
- interaction with Tailscale or another network boundary.

Tailscale connectivity alone must not be treated as the complete
authentication and authorization policy without that decision.

---

# Rejected alternatives

## Expose internal CLR contracts through JSON or reflection-based RPC

Rejected because internal types would become accidental wire contracts and
could expose mutable runtime or implementation details.

## Use REST polling for live observation

Rejected because polling does not preserve the ADR-0029 snapshot-plus-sequence
boundary or transient Event delivery semantics.

## Use WebSockets with an ad hoc message format

Rejected because HASE requires an explicit versioned schema, generated
contracts, stable unary operations, and server-streaming semantics.

## Use bidirectional streaming for all operations

Rejected because snapshot, Property, and Command operations are naturally
bounded request-response operations. Bidirectional streaming would add session
state without a current requirement.

## Send the initial snapshot in a separate unary call

Rejected because independently opening a snapshot call and observation stream
would reintroduce the race eliminated by ADR-0029.

## Permit clients to resume from a sequence

Rejected because sequences are subscription-local and no persistent
observation history or replay contract exists.

## Add an unbounded gRPC delivery queue

Rejected because one slow or disconnected client could consume unbounded
runtime-host memory and bypass ADR-0029 gap semantics.

## Map normalized operation failures only to gRPC status codes

Rejected because Property and Command statuses are stable application outcomes,
not transport failures, and clients must not infer them from diagnostic text.

## Return CLR type names or JSON for unsupported values

Rejected because such representations are unstable, culture-sensitive, and can
leak implementation details.

## Generate internal application models from protobuf

Rejected because the remote schema would replace the transport-independent
northbound boundary and force gRPC dependencies inward.

## Add endpoint lifecycle administration

Rejected because ADR-0019 and ADR-0023 retain complete lifecycle ownership in
the runtime host.

## Bind to all interfaces during development

Rejected because the security architecture is not yet accepted and an
accidental non-local service would create an unauthenticated control surface.

## Treat Tailscale reachability as authorization

Rejected because discovery, network reachability, authentication,
authorization, encryption policy, credential lifecycle, and auditing are
separate concerns.

---

# Initial implementation sequence

Implementation proceeds in small, independently buildable increments:

1. add this ADR;
2. add a dedicated versioned protobuf contract project and the smallest
   snapshot contract skeleton;
3. map runtime-host identity, API contract version, capture time, and an empty
   published-endpoint collection;
4. map complete published endpoint snapshots and descriptors;
5. add the unary `GetSnapshot` gRPC adapter;
6. add enforced loopback-only ASP.NET Core HTTP/2 hosting;
7. add the common version 1 remote value union from the values actually used by
   existing northbound contracts;
8. add Property target and normalized result messages;
9. map cached Property reads;
10. map authoritative Property reads;
11. map endpoint-confirmed Property writes;
12. add Command target and normalized result messages;
13. map Command execution without automatic retry or speculative cache update;
14. add observation request, initial snapshot, and observation stream messages;
15. map `Observe` so its first message uses the initial snapshot and sequence
    from one application observation subscription;
16. map every normalized observation payload;
17. preserve bounded buffering and explicit observation-gap termination without
    adding an unbounded adapter queue;
18. verify cancellation, deterministic subscription disposal, and host
    shutdown;
19. verify protobuf compatibility rules and stable error mapping;
20. verify that no RPC exposes or changes endpoint lifecycle ownership;
21. perform loopback-only process integration validation;
22. update ProjectStatus and Roadmap after verification.

No increment permits non-loopback binding.

---

# Consequences

## Positive

- Remote applications receive one strongly typed, versioned API.
- Unary operations map directly to the completed northbound application
  services.
- Observation streaming preserves the race-free initial snapshot and sequence
  boundary.
- Native and compact endpoint differences remain below the application
  boundary.
- Generated contracts support independent client implementations.
- Explicit protobuf compatibility rules allow controlled API evolution.
- Bounded observation behavior remains authoritative across the remote mapping.
- The runtime host retains complete physical endpoint lifecycle ownership.
- Loopback enforcement prevents accidental production exposure before security
  architecture is accepted.

## Costs

- Protobuf messages require explicit mapping to and from application contracts.
- Descriptor and value mapping require complete compatibility tests.
- ASP.NET Core and gRPC add hosting and package dependencies at the composition
  edge.
- Streaming cancellation and shutdown require deterministic disposal tests.
- Observation gaps require a stable terminal remote error mapping.
- Versioned contracts require long-term field-number and enum-value discipline.
- A separate security architecture is mandatory before useful non-local
  deployment.

---

# Scope exclusions

This decision does not define or introduce:

- endpoint discovery through the remote API;
- endpoint attachment, detachment, replacement, or shutdown RPCs;
- runtime-host shutdown administration;
- persistent Property or Event history;
- Event replay;
- offline Event queues;
- durable observation cursors;
- cross-subscription sequence comparison;
- client-to-server or bidirectional observation streaming;
- authentication or authorization policy;
- TLS certificate or credential lifecycle;
- audit policy or retention;
- production non-loopback exposure;
- wildcard, LAN, VPN, Tailscale, or public binding;
- Tailscale runtime-host discovery;
- public Internet exposure;
- browser compatibility or gRPC-Web;
- REST or JSON compatibility endpoints;
- service reflection in production;
- health-check publication outside loopback;
- automatic endpoint attachment or replacement;
- changes to native or compact southbound protocols.

Those require separate reviewed decisions.

---

# Verification requirements

Automated verification must demonstrate:

- versioned protobuf package, CLR namespace, service, messages, enums, and field
  numbers;
- protobuf round-trip behavior for every supported contract value;
- rejection of unsupported or malformed remote values;
- UTC timestamp validation;
- opaque attachment-generation round trips;
- complete immutable runtime-host snapshot mapping;
- complete published endpoint and descriptor mapping;
- one unary snapshot call mapped to the existing snapshot provider;
- Property targets mapped without a second generation authority;
- every normalized Property result status preserved;
- Command targets mapped without a second generation authority;
- every normalized Command result status preserved;
- no automatic Command retry;
- no speculative Property-cache update;
- one `Observe` call opens exactly one application observation subscription;
- the first stream message always contains that subscription's authoritative
  initial snapshot and snapshot sequence;
- no observation is written before the initial snapshot;
- later observation sequences are greater than the snapshot sequence;
- every ADR-0029 observation kind and payload maps explicitly;
- no unbounded intermediate stream buffer;
- explicit observation-gap termination;
- no Event replay after a gap or reconnect;
- unary and streaming cancellation propagation;
- deterministic and idempotent observation-subscription disposal;
- orderly gRPC host shutdown;
- one slow or cancelled client does not affect another subscription;
- normalized outcomes remain response data rather than diagnostic parsing;
- safe unexpected-error mapping;
- protobuf compatibility protections for version 1;
- enforced rejection of wildcard and non-loopback bindings;
- successful IPv4 and IPv6 loopback binding where supported;
- no remote operation exposes, transfers, or changes endpoint lifecycle
  ownership;
- `Hase.Runtime.Northbound` has no ASP.NET Core, gRPC, or protobuf dependency.

Process integration verification must:

1. start the ASP.NET Core gRPC host on an automatically selected loopback port;
2. connect through an HTTP/2 gRPC client;
3. obtain an authoritative runtime-host snapshot;
4. perform supported Property and Command operations through the same existing
   application services;
5. open observation and receive the initial snapshot as the first message;
6. receive later generation-bound observations in sequence;
7. cancel and reconnect without Event replay;
8. stop the host without changing endpoint lifecycle ownership;
9. confirm that non-loopback configuration is rejected.

Physical validation may begin only after the complete loopback mapping is
automated. It must use the existing runtime-host lifecycle and the same remote
gRPC contract for supported ESP32 Native Protocol Version 1 and Arduino Uno
Compact Serial Protocol operations.
