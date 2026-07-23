# ADR-0027 - Normalized Northbound Property Operations

- Status: Accepted
- Date: 2026-07-23

---

# Context

ADR-0023 defines the northbound runtime-host API boundary.

ADR-0024 through ADR-0026 define stable runtime-host identity, identity
resolution, and file-based identity persistence. Phase 7.1 and Phase 7.2
provide:

- stable runtime-host identity and API version;
- immutable runtime-host and endpoint attachment snapshots;
- authoritative inventory list and lookup;
- opaque attachment generations;
- file-backed snapshot composition.

Phase 7.3 must expose Property access to local and future remote applications
without exposing physical endpoint connections, protocol sessions, compact
wire identifiers, correlation identifiers, mutable runtime objects, or
attachment lifecycle ownership.

The runtime already maintains synchronized `RuntimeProperty` caches.

Native Protocol Version 1 and Compact Serial Protocol Version 1 already support
physical Property operations, but their operational paths differ:

- native operations use Protocol Version 1 messages, correlation identifiers,
  and the active runtime protocol binding;
- compact operations use host-side logical-to-byte mappings, compact statuses,
  one-reader request demultiplexing, and write confirmation reads.

The existing attachment inventory exposes the attached `RuntimeEndpoint` and
owning attachment session. It does not yet expose one transport-independent
Property operation port.

The existing northbound attachment-generation mapping is private to the
inventory snapshot provider. Active operations must use the exact same
generation authority as published snapshots. Separate generation mappings
could allow snapshots and operation routing to disagree.

Cached queries and authoritative endpoint operations also have different
semantics:

- a cached value may remain available while an endpoint is disconnected;
- a live read requires endpoint communication;
- a write must not update the cache merely because a client submitted a value.

These differences must remain explicit in the northbound application-service
contract.

---

# Decision

HASE will provide one normalized, transport-independent northbound Property
service.

The service keeps cached queries, explicit endpoint reads, and endpoint writes
as separate operations.

It routes active operations through an attachment-bound operation port owned by
the runtime host. Native and compact adapters implement that port without
exposing their wire protocols.

## Property target

Every Property request uses one immutable target containing:

```text
RuntimeHostPropertyTarget
    EndpointId
    RuntimeEndpointAttachmentGeneration
    InstrumentId
    PropertyId
```

The target contains logical HASE identities only.

It does not contain:

- network addresses;
- serial port names;
- Protocol Version 1 correlation identifiers;
- compact Property byte identifiers;
- descriptor repository references;
- transport or connection objects;
- mutable runtime objects.

The attachment generation is required for cached queries, explicit reads, and
writes.

Although cached queries do not communicate with the endpoint, generation
scoping prevents a client from silently interpreting a replacement
attachment's cache as belonging to an earlier inventory snapshot.

## Property service

The application-service boundary is conceptually:

```text
IRuntimeHostPropertyService
    GetCached(target)
    ReadAsync(target, CancellationToken)
    WriteAsync(target, requestedValue, CancellationToken)
```

The final CLR signatures are introduced through reviewed contracts in small
increments. They must preserve the semantics defined by this ADR.

The Property service:

- resolves targets only through the authoritative attachment inventory and
  shared attachment-generation authority;
- returns immutable application-facing snapshots and operation results;
- never returns `RuntimeEndpoint`, `RuntimeInstrument`, `RuntimeProperty`,
  attachment sessions, coordinators, transports, or protocol messages;
- never attaches, detaches, replaces, supervises, recovers, or disposes an
  endpoint;
- never transfers physical connection ownership to an application.

## Cached Property queries

`GetCached` never communicates with the physical endpoint.

It:

1. resolves the current published attachment by authoritative `EndpointId`;
2. verifies the expected attachment generation;
3. resolves the runtime instrument and Property by logical identity;
4. captures an immutable Property snapshot;
5. returns whether the cached value is known.

A successful cached snapshot contains:

```text
PublishedRuntimePropertySnapshot
    RuntimeHostPropertyTarget
    PropertyDescriptor
    EndpointConnectionStatus
    PropertyValue? CurrentValue
    bool IsKnown
```

`PropertyValue` already contains:

- engineering value;
- UTC timestamp;
- quality.

The snapshot does not claim that a live endpoint read succeeded.

A cached value may be returned while the endpoint is:

- disconnected;
- connecting;
- synchronizing;
- ready;
- faulted;
- recovering through the existing supervision lifecycle.

`IsKnown` is false when the runtime Property has not yet received an
authoritative value.

## Explicit Property reads

`ReadAsync`:

1. resolves and generation-validates the current attachment;
2. resolves the instrument and Property;
3. validates readable access;
4. captures the attachment-bound Property operation port;
5. requests one authoritative endpoint read;
6. maps the native or compact result into the normalized result;
7. updates the runtime cache only from a successful authoritative response;
8. returns the confirmed `PropertyValue`.

An explicit read does not return a stale cached value as though it were a live
success.

The service does not retry a failed explicit read automatically. Connection
supervision and recovery remain independent host-owned lifecycles.

## Property writes

`WriteAsync`:

1. resolves and generation-validates the current attachment;
2. resolves the instrument and Property;
3. validates writable access;
4. validates the requested value against the Property descriptor where the
   existing descriptor model supports validation;
5. captures the attachment-bound Property operation port;
6. submits the operation through the current attachment;
7. maps the native or compact result;
8. updates the cache only after endpoint confirmation or another approved
   authoritative protocol result;
9. returns the confirmed `PropertyValue`.

Submitting a requested value never updates the runtime cache by itself.

The service does not retry a state-changing write automatically after timeout,
connection loss, or another ambiguous outcome.

Compact writes retain the existing behavior:

```text
write request
    -> endpoint write status
        -> successful confirmation read
            -> runtime cache update
            -> confirmed PropertyValue
```

An unsuccessful compact write or confirmation read does not update the cache.

## Normalized result status

Expected failures are represented by a transport-independent status:

```text
Success
AttachmentNotCurrent
InstrumentNotFound
PropertyNotFound
ReadNotSupported
WriteNotSupported
InvalidValue
EndpointUnavailable
EndpointRejected
EndpointFailure
TimedOut
```

`AttachmentNotCurrent` covers:

- no currently published attachment with the supplied `EndpointId`;
- a current attachment whose generation differs from the supplied generation;
- an attachment that ended before operation routing completed.

This is the explicit stale-operation result required by ADR-0023.

A successful cached query contains a Property snapshot.

A successful explicit read or write contains a confirmed `PropertyValue`.

A failed result contains no Property value. It may contain bounded, safe
diagnostic text suitable for display and logging.

Applications must use the status rather than parse diagnostic text.

Cancellation continues to throw `OperationCanceledException`.

Programming defects, violated internal invariants, and unexpected host failures
are not silently converted into endpoint failures.

## Native and compact status mapping

Native Protocol Version 1 result codes and Compact Serial Protocol read/write
statuses remain internal implementation details.

They are mapped into the normalized status model.

The mapping must preserve meaningful distinctions where the endpoint protocols
provide them, including:

- unsupported operations;
- invalid values;
- deliberate endpoint rejection;
- endpoint-side failure;
- timeout;
- endpoint unavailability.

No wire result code becomes part of the northbound contract.

## Shared attachment-generation authority

The attachment-generation mapping will be extracted from the private
implementation of `RuntimeHostInventorySnapshotProvider` into one internal
shared projection or registry.

That shared authority is used by:

- inventory list;
- inventory lookup;
- runtime-host snapshot capture;
- cached Property queries;
- explicit Property reads;
- Property writes;
- later Command and observation services.

There must never be separate generation mappings for snapshot publication and
active operation routing.

For one published `RuntimeEndpointAttachmentInventoryEntry` object:

- one generation is retained for its published lifetime;
- every snapshot and operation lookup observes that same generation.

When the entry ends:

- its generation is retired;
- a later entry receives a new generation;
- reusing the same authoritative `EndpointId` does not reuse the generation.

## Attachment-bound Property operation port

Each production endpoint attachment session exposes one
transport-independent Property operation port.

The port:

- is created with the attachment's operational resources;
- remains owned by the attachment session;
- is permanently bound to that attachment's `RuntimeEndpoint` and coordinator;
- addresses instruments and Properties by logical HASE identity;
- becomes unusable when the attachment session ends;
- never resolves another attachment by `EndpointId`;
- never transfers connection ownership.

The Property service captures the current entry and its session-bound port
after generation validation.

It does not re-resolve the target by endpoint identity after awaiting.

If detach races an in-flight operation, that operation may:

- complete against the old attachment; or
- fail because the old attachment is shutting down.

It must never be redirected to a later attachment with the same `EndpointId`.

## Native Property adapter

The native adapter:

- resolves the target runtime instrument and Property;
- constructs Protocol Version 1 Property requests;
- uses the current protocol binding owned by the native attachment
  coordinator;
- creates and validates host-owned correlation identifiers;
- validates response message type and correlation;
- maps `ProtocolResultCode`;
- updates the runtime cache only after a successful authoritative response;
- returns the confirmed `PropertyValue`.

The adapter does not expose:

- `ProtocolMessage`;
- `CorrelationId`;
- the protocol connection;
- the transport connection;
- the connection manager;
- the coordinator.

## Compact Property adapter

The compact adapter:

- reverse-resolves `InstrumentId` and `PropertyId` through the attachment's
  validated host-side compact Property mapping;
- uses the compact coordinator and its existing serialized operation gate;
- maps compact Property-read and Property-write statuses;
- updates the runtime cache only after successful authoritative reads;
- preserves the existing write and confirmation-read behavior;
- returns the confirmed `PropertyValue`.

The adapter does not expose:

- compact Property byte identifiers;
- compact value encodings;
- compact frames;
- serial protocol connections;
- serial byte streams;
- compact coordinators.

## Descriptor and access validation

The Property service validates the target against the current attachment's
runtime model and immutable descriptor before active routing.

It distinguishes:

- unknown instrument;
- unknown Property;
- non-readable Property;
- non-writable Property;
- invalid requested value where descriptor validation can determine that
  safely before endpoint communication.

The physical endpoint remains authoritative. Host-side validation does not
permit speculative cache mutation and does not replace endpoint validation.

## Concurrency

Multiple applications may query caches and submit Property operations
concurrently.

The Property service does not introduce one global endpoint-operation lock.

Existing attachment-owned components retain responsibility for:

- native request correlation;
- compact request serialization;
- one-reader compact demultiplexing;
- connection replacement;
- supervision and recovery;
- orderly shutdown.

Northbound operation concurrency must not bypass those components.

## Lifecycle ownership

The runtime host remains the sole owner of:

- endpoint attachment and detachment;
- transport and protocol connections;
- compact mappings;
- synchronization;
- connection supervision and recovery;
- event routing;
- operation ports;
- attachment shutdown.

The Property service observes and routes through the current attachment. It
does not own its lifetime.

---

# Rejected alternatives

## Expose `RuntimeProperty` directly

Rejected because it is mutable in-process state and would expose cache mutation
and observer mechanisms across the northbound boundary.

## Use one operation for cached and live reads

Rejected because a cached value can be available while endpoint communication
is impossible. Applications must choose explicitly between cache inspection and
authoritative endpoint access.

## Omit attachment generation from cached queries

Rejected because a client could silently query a replacement attachment after
holding an earlier snapshot.

## Route by `EndpointId` after every await

Rejected because a detached endpoint could be replaced during an operation and
later routing could cross into the new attachment.

## Expose native and compact operation APIs separately

Rejected because applications must use one logical Properties, Commands, and
Events model independently of southbound protocol.

## Expose protocol or compact result codes

Rejected because the northbound contract must remain independent of native and
compact wire protocols.

## Update cache when a write is submitted

Rejected because the endpoint remains authoritative.

## Automatically retry writes

Rejected because timeout or connection loss can leave a state-changing
operation's endpoint outcome ambiguous.

## Let applications acquire physical endpoint connections

Rejected by ADR-0019 and ADR-0023. The runtime host exclusively owns physical
endpoint communication lifecycles.

## Maintain a second generation dictionary for operations

Rejected because snapshot and operation generation authority could diverge.

---

# Initial implementation sequence

Implementation proceeds in small, independently buildable increments:

1. add this ADR;
2. add immutable Property target, cached snapshot, normalized status, and result
   contracts;
3. extract the shared attachment-generation authority without changing
   published snapshot behavior;
4. implement cached Property query resolution;
5. add the attachment-bound Property operation port;
6. implement native authoritative Property reads;
7. implement compact authoritative Property reads;
8. implement native Property writes and confirmation mapping;
9. route compact Property writes through the existing confirmation behavior;
10. compose the Property service beside the existing snapshot providers;
11. validate native and compact endpoint families through the same in-process
    northbound Property contract;
12. update ProjectStatus and Roadmap after verification.

No increment selects a remote wire technology.

---

# Consequences

## Positive

- Applications receive one Property API for native and compact endpoints.
- Cached queries remain usable during temporary endpoint disconnection.
- Live reads remain explicitly authoritative.
- Writes preserve endpoint-confirmed cache semantics.
- Stale operations cannot cross attachment lifetimes.
- Snapshot and operation routing use one attachment-generation authority.
- Southbound protocols and wire identifiers remain hidden.
- Physical endpoint connection ownership remains local to the runtime host.
- The service can later be mapped to a remote API without changing endpoint
  lifecycle ownership.

## Costs

- Attachment sessions require a new transport-independent operation port.
- Native and compact adapters require explicit result mapping.
- The private generation projection must become a shared internal component.
- More immutable contracts and operation tests are required.
- Detach races must be tested to prove that operations do not cross into
  replacement attachments.

---

# Scope exclusions

This decision does not define:

- normalized Command execution;
- Event subscriptions;
- lifecycle or Property subscriptions;
- remote wire mapping;
- authentication, authorization, encryption, or auditing;
- Tailscale runtime-host discovery;
- persistent Property history;
- transactions;
- leases;
- priorities;
- idempotency keys;
- remote attachment, detachment, replacement, or host shutdown.

Those remain separate Phase 7 decisions and increments.

---

# Verification requirements

Automated verification must demonstrate:

- target validation for every required identity;
- cached known and unknown values;
- cached values while the endpoint is not Ready;
- no endpoint communication during cached queries;
- authoritative reads update the cache only after success;
- writes update the cache only after endpoint confirmation;
- explicit readable and writable access validation;
- invalid-value handling;
- normalized native and compact status mapping;
- cancellation propagation;
- timeout reporting;
- endpoint-unavailable reporting;
- stale-generation rejection;
- no routing into a replacement attachment;
- one shared generation across inventory snapshots and Property operations;
- concurrent clients remain behind attachment-owned correlation or
  serialization;
- no application obtains or disposes a physical endpoint connection.

In-process integration verification must exercise native Protocol Version 1 and
Compact Serial Protocol endpoints through the same
`IRuntimeHostPropertyService` contract before remote API technology is selected.
