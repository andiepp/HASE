# ADR-0029 - Northbound Live Observation

- Status: Accepted
- Date: 2026-07-24

---

# Context

ADR-0023 defines the northbound runtime-host API boundary.

ADR-0024 through ADR-0026 define stable runtime-host identity, identity
resolution, and file-based identity persistence.

ADR-0027 defines normalized northbound Property operations.

ADR-0028 defines normalized northbound Command execution.

Those implementations provide:

- immutable runtime-host and published endpoint snapshots;
- one shared attachment-generation authority for snapshots and active
  operations;
- attachment-bound Property and Command operation ports;
- normalized native and compact operation results;
- adapters that hide southbound protocol details;
- preservation of complete runtime-host lifecycle ownership.

Phase 7.5 must expose live runtime-host changes to local and future remote
applications without exposing mutable runtime objects, physical endpoint
connections, protocol sessions, transport notifications, compact wire
identifiers, or attachment lifecycle ownership.

The runtime already has internal synchronous observer mechanisms for:

- endpoint connection-status changes;
- runtime Property-value changes;
- runtime Event occurrences.

The authoritative attachment inventory currently exposes attach, detach, find,
and list operations. It does not expose publication or removal observation.

Northbound applications need to observe:

- publication of an attached endpoint;
- ending of a published attachment;
- endpoint connection-state changes;
- authoritative runtime Property-cache changes;
- transient Event occurrences.

A snapshot followed by an independently opened live stream has a race: a change
can occur after the snapshot is captured but before observation becomes active.
The northbound contract must not silently lose such changes.

Applications can also consume observations more slowly than the runtime
produces them. Unbounded buffering would allow one application to consume
unbounded host memory. Silent dropping would leave application state
untrustworthy.

Events remain transient under the existing runtime semantics. They have no
offline queue and no replay after reconnect. Property and lifecycle state can
be recovered from a fresh authoritative snapshot; lost Event occurrences
cannot be reconstructed.

The northbound observation contract must preserve these semantics while
presenting one transport-independent application service.

---

# Decision

HASE will provide one normalized, transport-independent northbound observation
service.

The service opens one observation subscription that provides:

```text
IRuntimeHostObservationService
    OpenSubscriptionAsync(options, CancellationToken)

RuntimeHostObservationSubscription
    InitialSnapshot
    SnapshotSequence
    ReadAllAsync(CancellationToken)
    DisposeAsync()
```

The final CLR signatures are introduced through reviewed contracts in small
increments. They must preserve the semantics defined by this ADR.

Opening a subscription coordinates initial snapshot capture and live
observation activation. The returned stream contains only observations whose
sequence is later than the returned snapshot sequence.

No attachment, connection, Property, or Event change may disappear between
the returned initial snapshot and the live stream.

## Observation kinds

The normalized observation model contains these kinds:

```text
AttachmentPublished
AttachmentEnded
ConnectionStatusChanged
PropertyValueChanged
EventOccurred
```

Each observation contains:

```text
Sequence
EndpointId
RuntimeEndpointAttachmentGeneration
Kind
Payload
```

The payload is an immutable normalized value appropriate to the observation
kind.

No observation exposes:

- `RuntimeEndpoint`;
- `RuntimeInstrument`;
- `RuntimeProperty`;
- `RuntimeEvent`;
- attachment inventory entries or sessions;
- transports or protocol connections;
- protocol messages or correlation identifiers;
- compact byte identifiers;
- mutable runtime collections.

## Sequence semantics

Every subscription observes a monotonically increasing sequence.

The sequence:

- establishes the order in which that subscription receives observations;
- identifies the boundary represented by its initial snapshot;
- allows an application to detect a discontinuity;
- is opaque outside the observation contract;
- is not a persistent host-wide event-log position;
- is not reusable after the subscription ends.

Applications must not compare sequence values from different subscriptions.

The sequence does not create Event persistence or replay.

## Race-free initial snapshot

Opening a subscription must coordinate these actions as one observation
boundary:

1. activate capture of later observations;
2. capture the authoritative runtime-host snapshot;
3. establish the snapshot sequence;
4. return the snapshot, sequence, and active subscription.

An implementation may order its internal locks and buffering differently, but
the externally visible result must be equivalent.

An observation representing state already included in the initial snapshot may
be omitted. An observation representing a later state must appear after the
snapshot sequence.

The initial snapshot contains current state. It does not synthesize Event
occurrences.

## Attachment lifecycle observations

`AttachmentPublished` is emitted only after an attachment becomes part of the
authoritative published attachment inventory.

Its payload contains the complete immutable
`PublishedRuntimeEndpointSnapshot`, including:

- authoritative `EndpointId`;
- attachment generation;
- immutable endpoint descriptor;
- captured connection status.

Discovery candidates, connection definitions, staged endpoints, bootstrap
attempts, failed unpublished attachments, and temporary verification
connections are not published observations.

`AttachmentEnded` identifies the authoritative `EndpointId`, ended attachment
generation, and host-observed UTC end time.

It does not imply why the attachment ended and does not expose transport or
administrative details.

Reattachment with the same authoritative `EndpointId` is represented by:

1. `AttachmentEnded` for the old generation;
2. `AttachmentPublished` for a new generation.

No generation is reused.

No automatic replacement policy is introduced.

## Connection-status observations

`ConnectionStatusChanged` contains:

- authoritative `EndpointId`;
- attachment generation;
- previous normalized connection status;
- current normalized connection status.

Each status retains:

- normalized connection state;
- UTC change time when known;
- optional safe diagnostic.

Applications must use normalized state rather than parse diagnostic text.

Connection supervision, retry, recovery, and replacement remain owned by the
runtime host.

## Property-value observations

`PropertyValueChanged` contains:

- authoritative `EndpointId`;
- attachment generation;
- `InstrumentId`;
- `PropertyId`;
- previous known `PropertyValue` when available;
- current authoritative runtime-cache `PropertyValue`.

The observation reports changes that have already entered the authoritative
runtime cache through approved runtime mechanisms.

The observation service:

- does not read the endpoint to manufacture a change;
- does not write a Property;
- does not update the cache;
- does not infer Property values from successful Commands;
- does not expose mutable `RuntimeProperty` objects.

Applications recover current Property state from the initial snapshot after
opening a new subscription.

## Event observations

`EventOccurred` contains:

- authoritative `EndpointId`;
- attachment generation;
- `InstrumentId`;
- logical `EventPath`;
- UTC occurrence timestamp;
- optional Event value.

Native Protocol Version 1 and Compact Serial Protocol Event routing remain
southbound implementation details.

Event semantics remain:

- transient;
- delivered only to subscriptions active for the occurrence;
- no offline queue;
- no replay;
- no synthetic occurrence from snapshots;
- no recovery of an occurrence lost because a subscription ended or overflowed.

The observation service does not change the existing stable runtime Event
observer continuity across physical connection replacement within one
attachment generation.

## Shared attachment-generation authority

The same attachment-generation authority used by inventory snapshots, Property
operations, and Command operations is also used by observation.

The same authority serves:

- inventory list;
- inventory lookup;
- runtime-host snapshot capture;
- Property operations;
- Command operations;
- attachment lifecycle observations;
- connection-status observations;
- Property-value observations;
- Event observations.

There must never be a separate generation mapping for observation.

Every observation is bound permanently to the attachment generation from which
it originated.

When an attachment ends:

- its generation is retired;
- observation adapters for that generation are deactivated;
- later callbacks from that generation are ignored;
- a later attachment with the same `EndpointId` receives a new generation.

An observation from an ended generation must never be attributed to a later
attachment.

## Attachment inventory observation

The authoritative attachment inventory will expose internal publication and
ending notifications required by the northbound projection.

Those notifications:

- describe committed inventory changes only;
- are emitted within a deterministic ordering boundary relative to inventory
  mutation;
- never expose failed unpublished attachment attempts;
- do not allow observers to attach, detach, replace, or dispose endpoints;
- do not transfer attachment session ownership.

The northbound observation composition subscribes to these notifications and
creates or retires generation-bound adapters.

Public attachment administration is not introduced.

## Generation-bound adapters

For every published attachment generation, the observation composition binds
adapters to the attachment's:

- runtime endpoint connection-status observer;
- runtime endpoint Property-value observer;
- runtime Event observers for the published descriptor.

The adapters:

- translate mutable runtime callbacks into immutable normalized observations;
- enqueue observations without awaiting application code;
- remain bound to one attachment generation;
- detach when that attachment ends;
- suppress stale callbacks after retirement;
- never resolve a different attachment by `EndpointId`;
- never own the endpoint lifecycle.

## Subscription isolation

Each application subscription has independent delivery state and buffering.

A slow, cancelled, failed, or disposed subscription must not:

- block runtime callback threads;
- block endpoint supervision;
- block inventory operations;
- block another subscription;
- affect physical endpoint routing;
- detach or dispose an endpoint.

Application code is not invoked directly from runtime observer callbacks.

Observer translation and enqueueing must be bounded and non-blocking with
respect to endpoint lifecycle and southbound routing.

## Bounded buffering and observation gaps

Every subscription uses a bounded observation buffer.

The buffer capacity is explicit and validated when the subscription is opened.
A safe implementation default may be provided.

When a subscription cannot accept the next observation because its buffer is
full:

- the observation is not silently dropped;
- the subscription enters a terminal observation-gap state;
- no later observations are delivered through that subscription;
- the asynchronous stream terminates with an explicit
  observation-gap failure;
- the application must dispose the subscription and open a new one;
- the new subscription returns a fresh authoritative initial snapshot.

Lifecycle and Property state can be recovered from that snapshot.

Event occurrences lost at the gap remain lost and are not replayed.

The service does not use an unbounded queue.

## Cancellation and disposal

Opening cancellation throws `OperationCanceledException`.

Enumeration cancellation throws `OperationCanceledException` for that
enumeration and does not become an observation.

Disposing a subscription:

- stops later delivery to that application;
- detaches only subscription-owned adapters or registrations;
- releases its bounded buffer;
- is idempotent;
- does not detach, replace, shut down, or dispose an endpoint;
- does not affect other subscriptions.

Disposing the runtime-host northbound composition ends all observation
subscriptions owned by that composition before releasing its observer
registrations. It still does not dispose the attachment inventory or endpoint
attachments.

## Callback failures

Unexpected application-consumer failure ends only that subscription.

One subscription must never prevent delivery to another subscription.

Existing runtime observer isolation remains in force. The observation adapter
must not throw application exceptions into runtime Event, Property, connection,
inventory, supervision, or transport code.

Programming defects and violated host invariants are not silently converted
into ordinary observations.

## Ordering

Each subscription receives one total sequence order across all normalized
observation kinds.

The order represents host observation order, not distributed physical time.

For one callback, its translated observation is sequenced once.

When attachment ending is sequenced:

- later callbacks from that generation are suppressed;
- `AttachmentEnded` is the final observable lifecycle boundary for that
  generation.

No ordering is promised between physical actions on independent endpoints
beyond the sequence in which the runtime host observes them.

Endpoint-provided Event and Property timestamps remain data inside their
payloads and do not replace subscription sequence ordering.

## Lifecycle ownership

The runtime host remains the sole owner of:

- discovery;
- endpoint attachment and detachment;
- transport and protocol connections;
- compact mappings;
- synchronization;
- connection supervision and recovery;
- connection replacement;
- Event routing;
- Property and Command operation ports;
- attachment shutdown and disposal.

The observation service observes published runtime state. It does not own or
administer that state.

---

# Rejected alternatives

## Return mutable runtime objects

Rejected because mutable runtime state would cross the northbound boundary and
could expose lifecycle implementation details.

## Open a stream after independently capturing a snapshot

Rejected because a change could occur between snapshot capture and stream
activation.

## Stream without an initial snapshot

Rejected because a new or reconnecting application needs authoritative current
state before applying later changes.

## Replay Event history

Rejected because current Event semantics are transient and provide no offline
queue or replay.

## Silently drop observations for slow consumers

Rejected because applications could continue with an undetectably inconsistent
view.

## Use an unbounded observation queue

Rejected because one slow application could consume unbounded runtime-host
memory.

## Block runtime callbacks until every application consumes

Rejected because a slow or failed application could block endpoint routing,
supervision, recovery, or other subscribers.

## Maintain a persistent host-wide observation log

Rejected because persistence, retention, replay, storage limits, and audit
semantics require a separate architecture decision.

## Publish discovery candidates or failed attachment attempts

Rejected because the operational northbound API exposes authoritative published
attachments, not discovery or lifecycle administration.

## Treat reattachment as the same generation

Rejected because stale clients could attribute observations to a replacement
attachment.

## Maintain a second generation dictionary for observations

Rejected because snapshot, Property, Command, and observation generation
authority could diverge.

## Let applications subscribe directly to runtime observers

Rejected because internal synchronous observers expose mutable runtime objects,
have no race-free initial snapshot, and are not a stable northbound contract.

## Let applications acquire physical endpoint connections

Rejected by ADR-0019 and ADR-0023. The runtime host exclusively owns physical
endpoint communication lifecycles.

---

# Initial implementation sequence

Implementation proceeds in small, independently buildable increments:

1. add this ADR;
2. add immutable observation-kind, sequence, payload, and subscription-option
   contracts;
3. add the public `IRuntimeHostObservationService` and subscription contracts;
4. add internal authoritative attachment publication and ending observation;
5. extend the shared attachment projection with observation sequencing;
6. implement race-free initial snapshot and stream activation;
7. bind connection-status observation to one attachment generation;
8. bind Property-value observation to one attachment generation;
9. bind Event occurrence observation to one attachment generation;
10. implement bounded per-subscription buffering and explicit observation-gap
    termination;
11. implement cancellation, subscription disposal, and composition shutdown;
12. verify stale-generation suppression and reattachment with the same
    `EndpointId`;
13. verify independent subscribers and callback-failure isolation;
14. validate native Protocol Version 1 and Compact Serial Protocol observations
    through one northbound service;
15. update ProjectStatus and Roadmap after verification.

No increment selects a remote wire technology.

---

# Consequences

## Positive

- Applications receive one observation model for native and compact endpoints.
- Every subscription begins from an authoritative, race-free snapshot boundary.
- Attachment generation prevents observations crossing attachment lifetimes.
- Connection, Property, and Event details remain transport-independent.
- Slow consumers cannot consume unbounded host memory.
- Observation loss is explicit rather than silent.
- Application failures do not affect endpoint routing or other subscriptions.
- Existing transient Event semantics remain unchanged.
- Physical endpoint connection ownership remains local to the runtime host.
- The service can later be mapped to a remote streaming API without changing
  endpoint lifecycle ownership.

## Costs

- The attachment inventory requires internal committed-change notifications.
- The shared attachment projection requires coordinated sequencing and snapshot
  activation.
- Every published attachment needs generation-bound observer adapters.
- Every subscription requires a bounded buffer and terminal-gap handling.
- Initial snapshot and stream activation require careful concurrency tests.
- Applications must reopen after cancellation, disposal, or an observation gap.
- Events lost during a gap cannot be recovered.

---

# Scope exclusions

This decision does not define:

- remote wire mapping;
- authentication, authorization, encryption, or auditing;
- persistent observation history;
- Event replay;
- offline Event queues;
- durable sequence cursors;
- cross-subscription sequence comparison;
- discovery candidate observation;
- failed unpublished attachment observation;
- transport-health or protocol-frame observation;
- remote attachment, detachment, replacement, or host shutdown;
- automatic endpoint attachment or replacement;
- Property reads or writes;
- Command execution;
- Property-cache derivation from Command effects;
- Tailscale runtime-host discovery.

Those remain separate Phase 7 decisions and increments.

---

# Verification requirements

Automated verification must demonstrate:

- immutable normalized observation contracts;
- validated bounded subscription capacity;
- one monotonically increasing sequence per subscription;
- snapshot sequence followed only by later observations;
- no change lost between initial snapshot capture and stream activation;
- attachment publication only after authoritative inventory publication;
- no observation for discovery candidates, staged endpoints, or failed
  unpublished attachments;
- attachment ending retires its generation;
- reattachment with the same `EndpointId` receives a new generation;
- stale callbacks from an ended generation are suppressed;
- normalized connection-status changes;
- normalized Property-value changes with previous and current values;
- normalized native Event occurrences;
- normalized compact Event occurrences;
- no Event replay;
- no synthetic Event occurrence from the initial snapshot;
- independent subscriber delivery;
- one slow or failed subscriber does not block another;
- bounded buffering;
- explicit terminal observation-gap behavior;
- lifecycle and Property recovery through a fresh snapshot after a gap;
- no Event recovery or replay after a gap;
- opening and enumeration cancellation propagation;
- idempotent subscription disposal;
- composition shutdown ends subscriptions without disposing attachments;
- no application callback executes on runtime observer callback paths;
- one shared generation across inventory snapshots, Property operations,
  Command operations, and observations;
- no application obtains or disposes a physical endpoint connection.

In-process integration verification must observe native Protocol Version 1 and
Compact Serial Protocol endpoints through the same
`IRuntimeHostObservationService` contract.

Physical validation must:

1. open a subscription and obtain its authoritative initial snapshot;
2. identify each published endpoint and attachment generation from that
   snapshot;
3. observe connection-state changes during host-owned recovery where practical;
4. observe authoritative Property-value changes;
5. observe one physical Event occurrence from each supported endpoint family;
6. confirm no Event replay after reconnect;
7. detach orderly through the runtime host;
8. observe the attachment ending without transferring lifecycle ownership.
