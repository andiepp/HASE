# ADR-0028 - Normalized Northbound Command Execution

- Status: Accepted
- Date: 2026-07-24

---

# Context

ADR-0023 defines the northbound runtime-host API boundary.

ADR-0024 through ADR-0026 define stable runtime-host identity, identity
resolution, and file-based identity persistence.

ADR-0027 defines normalized northbound Property operations. Its implementation
provides:

- immutable attachment-generation-scoped operation targets;
- one shared attachment-generation authority for inventory snapshots and active
  operations;
- attachment-bound operation ports retained by host-owned sessions;
- normalized native and compact operation results;
- adapters that hide southbound protocol details;
- preservation of complete runtime-host lifecycle ownership.

Phase 7.4 must expose Command execution to local and future remote applications
without exposing physical endpoint connections, protocol sessions, compact wire
identifiers, correlation identifiers, mutable runtime objects, or attachment
lifecycle ownership.

Native Protocol Version 1 and Compact Serial Protocol Version 1 already support
physical Command execution, but their operational paths differ:

- native operations use Protocol Version 1 messages, correlation identifiers,
  an optional argument, an optional return value, and the active runtime protocol
  binding;
- compact operations use host-side logical-to-byte mappings, compact statuses,
  and one-reader request demultiplexing.

Compact Command arguments require a protocol-specific encoding agreement.
No such general encoding is approved for this increment. The current compact
Command path therefore accepts only a null argument.

Commands may change endpoint state. A timeout or connection loss can occur
after the endpoint executed the Command but before the runtime host received
the response. Automatic retry would risk executing the Command twice.

Some Commands can affect Properties, but successful Command execution does not
itself provide authoritative Property values. Updating Property caches based on
an assumed Command effect would violate the endpoint-authoritative cache model.

The northbound Command contract must preserve these semantics while presenting
one transport-independent application service.

---

# Decision

HASE will provide one normalized, transport-independent northbound Command
service.

The service exposes one explicit execution operation:

```text
IRuntimeHostCommandService
    ExecuteAsync(target, argument, CancellationToken)
```

The final CLR signatures are introduced through reviewed contracts in small
increments. They must preserve the semantics defined by this ADR.

The service routes each operation through an attachment-bound Command operation
port owned by the runtime host. Native and compact adapters implement that port
without exposing their wire protocols.

## Command target

Every Command request uses one immutable target containing:

```text
RuntimeHostCommandTarget
    EndpointId
    RuntimeEndpointAttachmentGeneration
    InstrumentId
    CommandPath
```

The target contains logical HASE identities only.

It does not contain:

- network addresses;
- serial port names;
- Protocol Version 1 correlation identifiers;
- compact instrument or Command byte identifiers;
- descriptor repository references;
- transport or connection objects;
- mutable runtime objects.

The attachment generation is mandatory. It prevents an application holding an
earlier inventory snapshot from silently executing a Command against a
replacement attachment that has the same authoritative `EndpointId`.

## Command service

`IRuntimeHostCommandService.ExecuteAsync`:

1. resolves the current published attachment by authoritative `EndpointId`;
2. verifies the expected attachment generation;
3. resolves the runtime instrument by `InstrumentId`;
4. resolves the Command by logical `CommandPath`;
5. validates whether the supplied argument is supported by the selected
   attachment-bound adapter;
6. captures the attachment-bound Command operation port;
7. submits the Command exactly once;
8. maps the native or compact outcome into a normalized result;
9. returns the normalized status, optional return value, and optional safe
   diagnostic.

The Command service:

- resolves targets only through the authoritative attachment inventory and
  shared attachment-generation authority;
- returns immutable application-facing results;
- never returns `RuntimeEndpoint`, `RuntimeInstrument`, attachment sessions,
  coordinators, transports, protocol messages, or compact frames;
- never attaches, detaches, replaces, supervises, recovers, or disposes an
  endpoint;
- never transfers physical connection ownership to an application.

## Normalized result

The Command result exposes:

```text
Status
ReturnValue?
Diagnostic?
```

Expected outcomes are represented by this transport-independent status set:

```text
Success
AttachmentNotCurrent
InstrumentNotFound
CommandNotFound
ArgumentNotSupported
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

A successful result may contain a return value. A Command that has no return
value succeeds with no return value.

A failed result contains no return value. It may contain bounded, safe
diagnostic text suitable for display and logging.

Applications must use the status rather than parse diagnostic text.

Cancellation continues to throw `OperationCanceledException`. Cancellation is
not converted into a normalized failure status.

Programming defects, violated internal invariants, and unexpected host failures
are not silently converted into endpoint failures.

## Argument semantics

The northbound service accepts one optional Command argument.

The meaning and supported value shape remain defined by the logical Command
descriptor and the attachment-bound adapter.

For Native Protocol Version 1:

- a null or non-null optional argument is passed through the normalized adapter;
- the adapter uses the existing Protocol Version 1 Command argument
  representation;
- endpoint validation remains authoritative.

For Compact Serial Protocol Version 1 in this increment:

- only a null argument is supported;
- a non-null argument returns `ArgumentNotSupported`;
- the operation is rejected before a compact Command request is sent;
- no general compact argument encoding is introduced.

`ArgumentNotSupported` describes a mismatch between the requested argument and
the capabilities of the current Command path. It is distinct from deliberate
endpoint rejection of an argument that was transmitted.

## Return-value semantics

Native Protocol Version 1 passes its optional Command return value through the
normalized adapter.

Compact Commands in this increment do not produce a return value. A successful
compact result therefore contains no return value.

The absence of a return value is not a failure.

No native protocol response object, variant encoding, compact frame, or wire
status becomes part of the northbound contract.

## Native and compact status mapping

Native Protocol Version 1 result codes and Compact Serial Protocol Command
statuses remain internal implementation details.

They are mapped into the normalized status model.

The mapping must preserve meaningful distinctions where the endpoint protocols
provide them, including:

- deliberate endpoint rejection;
- endpoint-side failure;
- timeout;
- endpoint unavailability.

An unsupported or unknown logical target discovered before endpoint
communication is reported as `InstrumentNotFound`, `CommandNotFound`, or
`ArgumentNotSupported` as appropriate.

No wire result code becomes part of the northbound contract.

## Shared attachment-generation authority

The shared attachment-generation authority introduced for ADR-0027 is also used
for Command execution.

The same authority serves:

- inventory list;
- inventory lookup;
- runtime-host snapshot capture;
- Property operations;
- Command operations;
- later observation services.

There must never be separate generation mappings for snapshot publication,
Property routing, and Command routing.

For one published `RuntimeEndpointAttachmentInventoryEntry` object:

- one generation is retained for its published lifetime;
- every snapshot and operation lookup observes that same generation.

When the entry ends:

- its generation is retired;
- a later entry receives a new generation;
- reusing the same authoritative `EndpointId` does not reuse the generation.

## Attachment-bound Command operation port

Each production endpoint attachment session retains one
transport-independent Command operation port.

The port:

- is created with the attachment's operational resources;
- remains owned by the attachment session;
- is permanently bound to that attachment's `RuntimeEndpoint` and coordinator;
- addresses instruments and Commands by logical HASE identity;
- becomes unusable when the attachment session ends;
- never resolves another attachment by `EndpointId`;
- never transfers connection ownership.

The Command service captures the current entry and its session-bound port after
generation validation.

It does not re-resolve the target by endpoint identity after awaiting.

If detach races an in-flight operation, that operation may:

- complete against the old attachment; or
- fail because the old attachment is shutting down.

It must never be redirected to a later attachment with the same `EndpointId`.

## Native Command adapter

The native adapter:

- resolves the target runtime instrument and Command;
- constructs the Protocol Version 1 Command request;
- passes through the optional argument;
- uses the current protocol binding owned by the native attachment coordinator;
- creates and validates host-owned correlation identifiers;
- validates response message type and correlation;
- maps the existing native result code;
- returns the optional endpoint-provided value.

The adapter does not expose:

- protocol messages;
- correlation identifiers;
- protocol connections;
- transport connections;
- connection managers;
- coordinators.

## Compact Command adapter

The compact adapter:

- reverse-resolves `InstrumentId` and `CommandPath` through the attachment's
  validated host-side compact Command mapping;
- translates those logical identities to compact byte identifiers below the
  northbound boundary;
- rejects non-null arguments before endpoint communication;
- uses the compact coordinator and its existing serialized operation gate;
- maps compact Command statuses;
- returns no Command value in this increment.

The adapter does not expose:

- compact instrument or Command byte identifiers;
- compact value encodings;
- compact frames;
- serial protocol connections;
- serial byte streams;
- compact coordinators.

## Command execution and Property caches

Successful Command execution does not speculatively update any Property cache.

This applies even when:

- the Command is documented to change a Property;
- the expected new value appears deterministic;
- the physical validation scenario knows which value should result.

Property caches change only through existing authoritative mechanisms, including
successful Property reads, endpoint-confirmed Property writes, synchronization,
or authoritative endpoint events where approved by their own contracts.

A caller that needs confirmed post-Command state must perform an authoritative
Property read through `IRuntimeHostPropertyService`.

## No automatic Command retry

The Command service submits one Command at most once.

It never retries automatically after:

- timeout;
- connection loss;
- endpoint unavailability;
- attachment shutdown;
- an ambiguous response failure.

Connection supervision may recover or replace the host-owned physical
connection, but it does not replay the Command.

After an ambiguous failure, the application may inspect authoritative endpoint
state and make a new, explicit decision. A later application request is a new
Command execution, not an automatic retry.

## Concurrency

Multiple applications may submit Command operations concurrently.

The Command service does not introduce one global endpoint-operation lock.

Existing attachment-owned components retain responsibility for:

- native request correlation;
- compact request serialization;
- one-reader compact demultiplexing;
- connection replacement;
- supervision and recovery;
- orderly shutdown.

Northbound Command concurrency must not bypass those components.

## Lifecycle ownership

The runtime host remains the sole owner of:

- discovery;
- endpoint attachment and detachment;
- transport and protocol connections;
- compact mappings;
- synchronization;
- connection supervision and recovery;
- connection replacement;
- event routing;
- Property and Command operation ports;
- attachment shutdown and disposal.

The Command service observes and routes through the current attachment. It does
not own its lifetime.

---

# Rejected alternatives

## Expose `RuntimeCommand` directly

Rejected because it is mutable in-process runtime state and would expose
implementation details across the northbound boundary.

## Expose native and compact Command APIs separately

Rejected because applications must use one logical Properties, Commands, and
Events model independently of southbound protocol.

## Omit attachment generation

Rejected because a client holding an earlier snapshot could silently execute a
Command against a replacement attachment.

## Route by `EndpointId` after every await

Rejected because a detached endpoint could be replaced during execution and
later routing could cross into the new attachment.

## Expose compact byte identifiers

Rejected because compact mappings are attachment-owned southbound details and
must remain below the northbound boundary.

## Introduce compact argument encoding now

Rejected because no general encoding agreement is required for the approved
physical validation and introducing one would expand the Compact Serial
Protocol contract prematurely.

## Discard native arguments or return values

Rejected because Protocol Version 1 already supports both and the normalized
boundary can preserve them without exposing the wire protocol.

## Automatically retry Commands

Rejected because a timeout or connection loss can leave execution outcome
ambiguous and retrying can duplicate a state-changing action.

## Update Property caches from expected Command effects

Rejected because the endpoint remains authoritative and a successful Command
result is not an authoritative Property value.

## Let applications acquire physical endpoint connections

Rejected by ADR-0019 and ADR-0023. The runtime host exclusively owns physical
endpoint communication lifecycles.

## Maintain a second generation dictionary for Commands

Rejected because inventory, Property, and Command generation authority could
diverge.

---

# Initial implementation sequence

Implementation proceeds in small, independently buildable increments:

1. add this ADR;
2. add immutable Command target, normalized status, and result contracts;
3. add the public `IRuntimeHostCommandService` contract;
4. extend attachment sessions with an attachment-bound Command operation port
   without changing lifecycle ownership;
5. implement target and attachment-generation validation;
6. implement native Command execution with optional argument and return-value
   pass-through;
7. add validated compact logical Command reverse lookup;
8. implement compact null-argument Command execution;
9. normalize native and compact result mapping, timeout, and cancellation;
10. compose the Command service beside the existing snapshot and Property
    services;
11. verify no automatic retry and no speculative Property-cache mutation;
12. validate the ESP32 and Arduino Uno LED Commands through the same in-process
    northbound Command contract;
13. confirm resulting LED state and restoration through authoritative Property
    reads;
14. update ProjectStatus and Roadmap after verification.

No increment selects a remote wire technology.

---

# Consequences

## Positive

- Applications receive one Command API for native and compact endpoints.
- Native optional arguments and return values remain available.
- Compact wire identifiers remain hidden.
- Stale operations cannot cross attachment lifetimes.
- Inventory, Property, and Command routing use one attachment-generation
  authority.
- Ambiguous failures cannot trigger automatic duplicate execution.
- Property caches retain endpoint-authoritative semantics.
- Physical endpoint connection ownership remains local to the runtime host.
- The service can later be mapped to a remote API without changing endpoint
  lifecycle ownership.

## Costs

- Attachment sessions require an attachment-bound Command operation port.
- Native and compact adapters require explicit result mapping.
- Compact attachments require validated logical-to-byte Command reverse lookup.
- Compact non-null arguments remain unavailable until separately designed.
- Applications that require confirmed post-Command state must issue an
  authoritative Property read.
- Detach races and ambiguous failures require explicit tests.

---

# Scope exclusions

This decision does not define:

- compact non-null argument encoding;
- compact Command return values;
- automatic retry or replay;
- Command queues or scheduling;
- idempotency keys;
- transactions;
- Property-cache derivation from Command effects;
- Event subscriptions;
- lifecycle or Property subscriptions;
- remote wire mapping;
- authentication, authorization, encryption, or auditing;
- Tailscale runtime-host discovery;
- remote attachment, detachment, replacement, or host shutdown.

Those remain separate Phase 7 decisions and increments.

---

# Verification requirements

Automated verification must demonstrate:

- target validation for every required identity;
- stale-generation rejection;
- no routing into a replacement attachment;
- instrument-not-found and Command-not-found reporting;
- compact non-null argument rejection before endpoint communication;
- native null and non-null argument pass-through;
- native optional return-value pass-through;
- compact success without a return value;
- normalized native and compact status mapping;
- cancellation propagation as `OperationCanceledException`;
- timeout reporting;
- endpoint-unavailable reporting;
- deliberate endpoint-rejection reporting;
- endpoint-failure reporting;
- exactly one submission per service invocation;
- no automatic retry after timeout or connection loss;
- no speculative Property-cache update after Command success;
- one shared generation across inventory snapshots, Property operations, and
  Command operations;
- concurrent clients remain behind attachment-owned correlation or
  serialization;
- no application obtains or disposes a physical endpoint connection.

In-process integration verification must execute LED Commands against Native
Protocol Version 1 and Compact Serial Protocol endpoints through the same
`IRuntimeHostCommandService` contract.

Physical validation must:

1. attach and publish the physical ESP32 and Arduino Uno through the runtime
   host;
2. construct each Command target from the authoritative endpoint identity and
   published attachment generation;
3. execute the logical LED Command through the same northbound Command service;
4. confirm the resulting state with an authoritative Property read through
   `IRuntimeHostPropertyService`;
5. restore the original LED state through the Command service;
6. confirm the restored state with another authoritative Property read;
7. detach orderly through the runtime host.

