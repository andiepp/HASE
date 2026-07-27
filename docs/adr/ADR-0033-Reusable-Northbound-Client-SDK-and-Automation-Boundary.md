# ADR-0033 - Reusable Northbound Client SDK and Automation Boundary

- Status: Accepted
- Date: 2026-07-27

---

# Context

ADR-0023 through ADR-0029 define the transport-independent northbound
runtime-host application boundary, including:

- stable runtime-host identity;
- immutable published endpoint snapshots;
- authoritative endpoint identity;
- opaque attachment generations;
- normalized cached and authoritative Property reads;
- endpoint-confirmed Property writes;
- exactly-once Command execution at the application-service boundary;
- live observation with one coherent initial snapshot;
- strictly increasing subscription-local sequences;
- explicit observation-gap termination;
- transient Events with no offline queue or replay.

ADR-0030 maps that boundary to the versioned
`RuntimeHostRemoteApi` protobuf and gRPC service.

ADR-0031 defines the northbound remote API security boundary.

ADR-0032 implements and physically validates a controlled private-network
deployment in which:

- a Windows 11 desktop owns the runtime host;
- the desktop remains the sole owner of the physical ESP32 and Arduino
  lifecycles;
- a separate Windows 11 laptop is an authenticated operational client;
- mutual TLS uses operating-system certificate-store private-key custody;
- the laptop pins the runtime-host server certificate exactly;
- all deployment-specific addresses, credentials, identifiers, and paths remain
  outside source control and ordinary console output.

The verified baseline before this ADR is:

```text
Commit 0ad8bcbd3b1e1539796f578d3e5498274984ab71
3,029 automated tests pass
.NET solution builds
ADR-0032 controlled private-network validation is complete
Authenticated laptop snapshot, Property, Command, and observation access succeeds
```

The validated laptop client is currently a Protocol Explorer scenario. It
proves the remote contract and deployment but is not a reusable application
client.

The next product-facing capability is a real laptop UI. HASE also needs to
preserve a practical path for:

- command-line tools;
- interactive automation;
- Python scripts;
- Jupyter notebooks;
- future presentation technologies;
- future operating systems.

Implementing connection, mapping, sequencing, recovery, and mutation semantics
directly inside WPF ViewModels would make the first client presentation-specific
and would duplicate those responsibilities in every later client.

The generated protobuf client is intentionally transport-shaped. It exposes:

- protobuf messages;
- gRPC call objects;
- transport exceptions;
- server-stream mechanics;
- protobuf `oneof` cases;
- certificate-backed channel composition.

Those details are required by the adapter but are not the appropriate
application model for ViewModels or interactive automation.

Python can generate a native gRPC client from the version 1 protobuf contract.
However, the standard Python gRPC TLS API ordinarily consumes certificate and
private-key bytes. ADR-0032 deliberately installs the client private key as
non-exportable in the Windows Current User certificate store. Exporting or
duplicating that key for Python would weaken the accepted deployment boundary.

HASE therefore needs one reusable client architecture that:

- preserves the existing version 1 API;
- preserves ADR-0032 credential custody;
- centralizes remote client semantics;
- remains independent of WPF;
- can later support a separately secured Python automation boundary;
- does not transfer physical endpoint lifecycle ownership to any client.

---

# Decision

HASE will introduce a reusable, UI-independent northbound client SDK before
implementing the WPF laptop application.

The client architecture has three primary layers:

```text
Hase.Client
    transport-independent client contracts, models, state, and behavior

Hase.Client.Grpc
    version 1 protobuf/gRPC and ADR-0032 private-network adapter

Hase.Client.Wpf
    WPF and Prism presentation over Hase.Client
```

Future Python, command-line, and other presentation integrations will consume
the same normalized client boundary. They will not reimplement the remote
runtime-host semantics independently.

The exact public CLR types and constructors will be introduced only in small
reviewed implementation increments. This ADR fixes their responsibilities and
dependencies, not their final spelling.

## Dependency direction

The dependency direction is:

```text
Hase.Client.Wpf
    -> Hase.Client

Hase.Client.Grpc
    -> Hase.Client
    -> Hase.Runtime.Remote.Grpc.Contracts
    -> Hase.Runtime.Remote.Grpc.Hosting

Hase.Client
    -> no WPF dependency
    -> no Prism dependency
    -> no gRPC dependency
    -> no protobuf dependency
    -> no certificate-store dependency
```

`Hase.Client` is the application-facing boundary.

`Hase.Client.Grpc` is a replaceable remote transport adapter for the accepted
version 1 gRPC contract.

`Hase.Client.Wpf` is the first presentation layer and must not become the owner
of remote transport composition or observation sequencing.

Dependencies must not point from `Hase.Client` to WPF, Prism, generated
protobuf types, `Grpc.Core`, `Grpc.Net.Client`, ASP.NET Core, or X.509
certificate-store infrastructure.

## Client lifecycle ownership

One connected client session owns:

- one remote client deployment;
- one gRPC channel;
- one generated version 1 client;
- one observation subscription;
- one connected-session cancellation boundary;
- one current observation sequence;
- one normalized current runtime-host model.

The session is the sole owner of those resources.

ViewModels, views, scripts, and individual operations must not create or dispose
channels independently.

The session is explicitly connected and disconnected. Disposal is idempotent
from the consumer's perspective and releases every resource owned by the
connected session.

The client session never owns:

- runtime-host process lifetime;
- endpoint discovery;
- endpoint attachment or detachment;
- physical endpoint connections;
- runtime endpoint supervision;
- reconnect policy for physical endpoints;
- descriptor synchronization with physical endpoints.

Those remain runtime-host responsibilities under ADR-0019 and ADR-0023.

## Client session states

The normalized client session distinguishes at least:

```text
Disconnected
Connecting
Connected
Reconnecting
Disconnecting
Faulted
```

These describe the application's relationship with the remote runtime host.
They are separate from each published physical endpoint's connection state.

The first implementation may introduce the states incrementally, but must not
collapse:

- remote client connection state;
- physical endpoint connection state;
- attachment lifetime.

While the remote client is reconnecting, the last normalized model may remain
available for presentation as stale. New Property writes and Command executions
must not be queued or submitted until a new authoritative session baseline is
established.

## Normalized client model

`Hase.Client` defines an immutable or externally immutable representation of:

- runtime-host identity;
- remote API version;
- published endpoint attachments;
- endpoint identity;
- attachment generation;
- endpoint descriptor;
- endpoint connection status;
- instruments;
- Properties;
- Commands;
- Events;
- normalized Property values;
- normalized operation results;
- normalized live observations;
- client failures and state transitions.

The client model is descriptor-driven.

Presentation code must obtain operational identity from normalized model
identifiers and paths. Display names are never operational identity.

The model preserves endpoint identity and attachment generation as separate
values.

Attachment identity is:

```text
EndpointId + AttachmentGeneration
```

Property target identity is:

```text
EndpointId
    + AttachmentGeneration
    + InstrumentId
    + PropertyId
```

Command target identity is:

```text
EndpointId
    + AttachmentGeneration
    + InstrumentId
    + ordered Command path
```

Event identity is:

```text
EndpointId
    + AttachmentGeneration
    + InstrumentId
    + ordered Event path
```

The client does not infer identity from display name, collection position,
endpoint transport, network address, COM port, or physical connection detail.

## Remote API version

The gRPC adapter validates the runtime-host API version before publishing a
connected client model.

An unsupported major version fails connection.

Backward-compatible minor-version evolution within major version 1 remains
subject to protobuf compatibility rules.

The client SDK does not silently reinterpret an incompatible contract.

## Initial state and observation

The long-running client session opens `Observe` as its authoritative
initialization path.

The first stream message must be the observation initial snapshot.

The adapter maps that snapshot into the normalized client model and records its
snapshot sequence before declaring the client session connected.

Every later stream message must contain one observation.

A second initial snapshot in the same subscription is invalid.

The adapter enforces strictly increasing subscription-local sequences.

The normalized observation reducer supports all version 1 observation kinds:

```text
AttachmentPublished
AttachmentEnded
ConnectionStatusChanged
PropertyValueChanged
EventOccurred
```

The reducer applies an observation only to its exact endpoint identity and
attachment generation.

Property changes additionally match instrument and Property identity.

Events additionally preserve instrument identity, ordered Event path, UTC
occurrence time, and optional normalized value.

## Physical disconnect and recovery propagation

The client SDK exposes runtime-host physical connection-state changes through
the normalized model and observation stream.

Physical connection loss and recovery inside one continuing attachment use
`ConnectionStatusChanged`.

Examples include:

- ESP32 network or Wi-Fi loss detected by the runtime connection supervisor;
- ESP32 reset and reconnect;
- Arduino USB unplug detected by the serial runtime connection;
- Arduino re-plug and compact protocol resynchronization;
- health-probe failure;
- reconnect and descriptor/state synchronization.

The expected physical endpoint progression may include:

```text
Ready
    -> Faulted
    -> Reconnecting
    -> Connecting
    -> Synchronizing
    -> Ready
```

The exact progression is runtime-host authoritative. Clients must not invent
missing intermediate states or implement physical reconnect logic.

When an attachment actually ends, the client receives `AttachmentEnded`.

A later `AttachmentPublished` with the same endpoint identity but a different
attachment generation is a new attachment. The client must not automatically:

- reuse the old selection;
- reuse an editor;
- submit a pending operation;
- retarget a Command;
- transfer mutation state.

Cached values may remain visible during physical disconnection with their
original timestamps, quality, and current endpoint connection status.

## Observation gaps and reconnect

Version 1 observation has no replay.

If the remote host reports an observation gap with gRPC `DataLoss`, the client:

1. stops applying the failed subscription;
2. discards its subscription-local sequence;
3. opens a new observation subscription;
4. requires a new initial snapshot;
5. reconciles or replaces normalized state from that snapshot.

The client does not request missing observations because no replay API exists.

If the laptop itself disconnects from the runtime host, transitions missed
while offline are not reconstructed. A new initial snapshot establishes current
state.

Any automatic remote-session reconnect behavior must be bounded, cancellable,
observable to the consumer, and tested. It must never replay or automatically
repeat Property writes or Commands.

## Property operations

The normalized client SDK exposes:

- cached Property read;
- authoritative Property read;
- Property write with endpoint confirmation.

The gRPC adapter maps those operations one-for-one to remote API version 1.

The client SDK preserves every stable Property operation outcome. Consumer
behavior must depend on the normalized status, not diagnostic-text parsing.

Cached reads perform no physical endpoint I/O.

Authoritative reads query the current physical attachment.

Successful writes return the endpoint-confirmed value.

The client does not permanently publish an optimistic Property value as
authoritative.

If a Property write times out, is cancelled after possible transmission, or
loses its transport response, the result may be uncertain. The client:

- does not retry automatically;
- does not claim that the endpoint did not act;
- supports a later authoritative read to resolve state.

## Command operations

The normalized client SDK exposes explicit Command execution.

Command execution remains exactly once from the client adapter's perspective.

The client adapter never automatically retries a Command after:

- timeout;
- cancellation;
- transport failure;
- remote-session loss;
- an ambiguous response.

If a Command affects a known Property, confirmation is obtained through an
explicit authoritative Property read. The client does not infer authoritative
Property state from Command submission alone.

Scripts and UI commands use the same mutation rules.

Interactive convenience must not weaken exactly-once and uncertain-outcome
behavior.

## Deadlines and cancellation

The gRPC adapter applies explicit bounded deadlines to unary remote calls.

The exact default deadline values remain implementation configuration and will
be introduced in a reviewed increment.

Consumers can cancel:

- connection establishment;
- active reads;
- active writes;
- active Command calls;
- the observation subscription;
- remote-session recovery;
- orderly disconnect.

Cancellation propagates to the remote call.

Cancellation is not proof that a submitted mutation had no effect.

One connected-session cancellation boundary coordinates observation shutdown
and disposal. Per-operation cancellation is linked without transferring session
ownership to the operation.

## Error model

The client SDK distinguishes:

1. normalized application-operation outcomes;
2. authentication and authorization failures;
3. remote API compatibility failures;
4. transport and availability failures;
5. deadline and cancellation;
6. observation-gap failure;
7. invalid remote-contract data;
8. local configuration and credential-loading failure.

Expected Property and Command statuses remain result values.

They are not converted into generic exceptions merely for presentation
convenience.

Transport exceptions and generated gRPC status types are mapped at the
`Hase.Client.Grpc` boundary. `Hase.Client` consumers do not need to reference
`Grpc.Core`.

Diagnostics are explanatory only. Consumer decisions use stable status and
failure categories.

The client does not expose secrets through exception decoration, activity
entries, ordinary UI output, or script-friendly result text.

## Threading and presentation independence

`Hase.Client` does not depend on a UI dispatcher or synchronization context.

It publishes normalized state and observations through asynchronous contracts
that can be consumed by:

- WPF;
- command-line applications;
- service processes;
- test harnesses;
- a future automation bridge.

The WPF layer owns marshaling presentation changes to the WPF dispatcher.

The client SDK does not mutate WPF `ObservableCollection<T>` instances.

Python integration does not become dependent on WPF threading behavior.

## WPF client boundary

`Hase.Client.Wpf` is a presentation layer over `Hase.Client`.

WPF ViewModels may own:

- presentation selection;
- formatting;
- input editing;
- `CanExecute` policy derived from client state and descriptors;
- UI activity history;
- UI-dispatcher marshaling.

WPF ViewModels must not own:

- certificate selection;
- server-certificate validation;
- gRPC channel construction;
- protobuf mapping;
- observation sequence enforcement;
- remote reconnect mechanics;
- mutation retry;
- physical endpoint supervision.

Prism remains a presentation-composition concern and is not introduced into
`Hase.Client` or `Hase.Client.Grpc`.

## ADR-0032 security preservation

The gRPC adapter reuses the existing ADR-0032 client deployment composition.

The client private key remains:

- installed outside the repository;
- held in the Windows Current User certificate store;
- non-exportable after laptop installation;
- accessed through the existing .NET TLS composition;
- absent from client configuration and normalized client models.

The client continues to:

- require HTTPS;
- use TLS 1.2 or TLS 1.3;
- present the explicitly selected client certificate;
- validate server IP identity;
- pin the provisioned server certificate exactly;
- reject insecure fallback.

Neither the WPF layer nor the normalized client SDK receives certificate or
private-key bytes.

The client SDK must not log or ordinarily display:

- private-network address;
- certificate thumbprint;
- certificate contents;
- credential identifier;
- private key;
- password;
- machine-specific configuration path.

## Python and automation boundary

Python support is an intentional future consumer of the normalized client
architecture.

Direct native Python gRPC remains technically possible because the protobuf
contract is language-neutral.

Direct Python access is not the default ADR-0032 deployment path because
standard Python gRPC TLS composition ordinarily requires private-key bytes and
would not reuse the accepted non-exportable Windows certificate-store key
without additional platform integration.

HASE will not export, duplicate, or weaken the ADR-0032 client credential merely
to simplify Python connectivity.

The preferred future Python architecture is:

```text
Python script or notebook
    -> separately secured local automation boundary
    -> reusable .NET HASE client SDK/session
    -> ADR-0032 mutual-TLS gRPC connection
    -> desktop runtime host
```

The local automation boundary is not implemented by this ADR.

It requires a separate decision covering:

- local transport;
- local caller identity;
- authorization;
- process and user isolation;
- operation exposure;
- observation delivery;
- cancellation;
- lifecycle;
- error mapping;
- resource governance;
- secret redaction.

The future boundary must not allow an arbitrary local process to borrow the
enrolled HASE client identity without an explicit authorization decision.

The Python-facing API should expose normalized HASE concepts rather than
protobuf transport details where practical.

Python Property writes and Commands remain subject to the same:

- explicit targeting;
- attachment-generation validation;
- no-automatic-retry rule;
- uncertain-outcome handling;
- authoritative confirmation behavior.

## Testability

`Hase.Client` behavior is testable without:

- a physical endpoint;
- a network listener;
- a certificate;
- gRPC;
- WPF.

Tests cover at least:

- normalized identity and target construction;
- descriptor-to-client-model projection;
- initial snapshot application;
- all five observation kinds;
- strictly increasing sequence enforcement;
- rejection of a second initial snapshot;
- stale attachment-generation handling;
- attachment end and replacement behavior;
- physical connection-state propagation;
- Property and Command status preservation;
- mutation no-retry behavior;
- cancellation and orderly disposal;
- secret-safe failures.

`Hase.Client.Grpc` tests cover:

- protobuf-to-client mapping;
- client-to-protobuf target and value mapping;
- API major-version rejection;
- initial observation ordering;
- gRPC status mapping;
- deadline and cancellation propagation;
- observation-gap handling;
- ADR-0032 deployment reuse;
- channel and stream disposal;
- absence of automatic mutation retry.

WPF tests cover presentation behavior without duplicating client adapter tests.

Physical validation remains a separate acceptance increment after the UI
supports the required operations.

## Implementation sequence

Implementation proceeds in small buildable increments:

1. introduce client core contracts and normalized immutable state;
2. implement snapshot projection and identity preservation;
3. implement the observation reducer and sequence validation;
4. introduce the version 1 gRPC mapping boundary;
5. implement connected client-session lifecycle over ADR-0032 deployment;
6. implement bounded cancellation and remote-session recovery behavior;
7. add the WPF and Prism application shell;
8. add descriptor-driven read-only inventory and Property access;
9. add live observation presentation;
10. add descriptor-valid Property writes;
11. add explicit Command execution and authoritative confirmation;
12. physically validate the complete desktop-to-laptop UI scenario;
13. decide the separate local automation and Python boundary.

Each increment must:

- build;
- keep all existing tests passing;
- add focused tests for its new behavior;
- preserve the previously validated Protocol Explorer scenarios;
- avoid embedding deployment-specific data.

---

# Consequences

## Positive

- WPF does not become the owner of remote client semantics.
- Snapshot, targeting, observation, recovery, and mutation behavior are
  implemented once.
- The first real client establishes a reusable application-facing SDK.
- Generated protobuf and gRPC types remain contained in the adapter.
- Physical disconnect and recovery state can be presented consistently.
- UI, CLI, and future automation can share the same status and error model.
- Python interoperability has an intentional path that preserves ADR-0032
  credential custody.
- Client core behavior can be tested without hardware, certificates, network,
  or WPF.
- Attachment-generation safety remains visible and enforceable at the client
  boundary.
- Future remote contract versions can be introduced through additional
  adapters without rewriting presentation behavior.

## Negative

- The WPF client begins later than a direct generated-client implementation.
- Two reusable client projects add initial solution structure and mapping code.
- Normalized client models deliberately duplicate the shape of some protobuf
  data to preserve transport independence.
- Session lifecycle and observation reduction require explicit design and
  testing before visible UI functionality appears.
- Python still requires a later local-boundary decision and implementation.

## Risks

- Client models may accidentally become a second authoritative runtime model.
- Adapter mapping may drift from the version 1 protobuf contract.
- Overly broad session recovery could hide outages or repeat unsafe mutations.
- Presentation convenience may encourage identity by display name.
- A future Python bridge could unintentionally delegate the enrolled HASE
  identity to unauthorized local processes.
- Cached state may be presented without sufficient stale-state indication.

Mitigations:

- runtime-host snapshots and observations remain authoritative;
- the client model is a projection, not a lifecycle owner;
- exhaustive mapping and sequence tests are required;
- attachment generation remains part of every active target;
- mutation retries remain prohibited;
- remote-session state and endpoint state remain separate;
- cached values retain timestamp, quality, and connection context;
- the Python boundary requires a separate security ADR.

---

# Alternatives Considered

## Put the generated gRPC client directly in WPF ViewModels

Rejected because it couples presentation code to:

- protobuf;
- gRPC call objects;
- TLS deployment;
- observation sequencing;
- transport failure mapping;
- disposal.

It would also force later clients to duplicate those behaviors.

## Build only one WPF-specific client service

Rejected because command-line, Python, test, and future presentation consumers
would not have a stable UI-independent client boundary.

## Expose generated protobuf types as the reusable SDK model

Rejected because protobuf types are transport contracts, not the
application-facing client model.

This would leak:

- generated naming;
- `oneof` mechanics;
- protobuf collection behavior;
- transport-version concerns;
- gRPC dependencies.

## Reference WPF or Prism from the client SDK

Rejected because the reusable SDK must remain usable from non-UI processes,
tests, command-line tools, and future automation.

## Implement a direct Python gRPC client first

Rejected as the primary path because it would bypass the reusable .NET client
foundation and would not naturally preserve ADR-0032 non-exportable Windows
private-key custody.

## Export the ADR-0032 client private key for Python

Rejected because it weakens the accepted credential-provisioning and custody
model.

## Make the client own endpoint reconnect

Rejected because physical endpoint connection supervision belongs exclusively
to the runtime host.

The northbound client observes connection-state changes; it does not reconnect
the ESP32 or Arduino itself.

## Automatically retry Property writes and Commands after reconnect

Rejected because the endpoint may already have acted. Automatic retry could
duplicate a Command or repeat a mutation.

## Reconstruct missed observations after laptop reconnect

Rejected because version 1 provides no replay or persistent Event history.

A new subscription begins with a new authoritative initial snapshot.

## Treat endpoint identity alone as the client attachment key

Rejected because the same endpoint can be published again under a new
attachment generation. Generation-scoped identity prevents stale operations
from targeting a replacement attachment.

## Add the Python bridge in the same increment

Rejected because the local bridge introduces a separate authorization and
identity-delegation boundary that requires its own decision.

---

# Scope

This ADR approves:

- a reusable UI-independent HASE client SDK;
- a version 1 gRPC adapter;
- reuse of the ADR-0032 private-network client deployment;
- normalized client session and state semantics;
- propagation of physical endpoint connection-state observations;
- a WPF and Prism presentation over the reusable SDK;
- an intentional future Python automation path;
- a separate later security decision for the local Python boundary.

This ADR does not approve:

- changes to the version 1 protobuf contract;
- a remote lifecycle-management API;
- remote endpoint discovery, attachment, or detachment;
- transfer of physical endpoint ownership to clients;
- observation replay or persistent Event history;
- export of ADR-0032 private keys;
- PEM credential files as the default laptop profile;
- an unrestricted local Python bridge;
- credential rotation or revocation;
- production promotion of the ADR-0032 deployment;
- Internet exposure;
- Tailscale runtime-host discovery;
- automatic Property-write or Command retry.

---

# Acceptance Criteria

ADR-0033 is satisfied when:

1. the solution contains a UI-independent client core with no gRPC, protobuf,
   WPF, Prism, or certificate dependency;
2. a version 1 gRPC adapter maps the complete operational remote API to that
   client core;
3. the adapter reuses the ADR-0032 certificate-store-backed client deployment;
4. a connected session is initialized from the observation initial snapshot;
5. all five observation kinds are normalized and applied;
6. physical endpoint connection loss and recovery are visible to consumers;
7. subscription sequences are enforced strictly;
8. observation gaps require a new subscription and initial snapshot;
9. endpoint identity and attachment generation remain separate;
10. stale-generation operations are not retargeted automatically;
11. Property writes and Commands are never retried automatically;
12. uncertain mutation outcomes remain explicit;
13. WPF ViewModels consume the client SDK rather than generated gRPC types;
14. deployment values and credentials remain outside source and ordinary
    output;
15. focused automated tests cover client state, mapping, observation,
    cancellation, failure, and disposal;
16. all pre-existing tests continue to pass;
17. the existing Protocol Explorer private-network validation continues to
    succeed;
18. the first WPF client is physically validated against the desktop-owned
    ESP32 and Arduino endpoints;
19. Python automation remains blocked until its local authorization boundary is
    separately approved.

---

# Result

HASE gains one reusable client architecture between the remote API and every
presentation or automation technology.

The first real laptop UI is built on that architecture rather than becoming
the architecture.

Physical disconnect, reconnect, USB unplug/re-plug, endpoint resynchronization,
and attachment replacement remain runtime-host responsibilities and are
propagated northbound as normalized client state.

Python interoperability remains feasible without weakening the accepted
ADR-0032 credential model. Its local authorization and identity-delegation
boundary will be decided separately after the reusable .NET client session is
implemented and validated.
