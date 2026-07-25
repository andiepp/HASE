# ADR-0031 - Northbound Remote API Security Boundary

- Status: Accepted
- Date: 2026-07-25

---

# Context

ADR-0019 assigns complete physical endpoint communication lifecycle ownership
to the runtime host.

ADR-0023 defines the transport-independent northbound runtime-host application
boundary.

ADR-0024 through ADR-0026 define immutable runtime-host snapshots, stable
runtime-host identity, opaque attachment generations, authoritative inventory
projection, identity resolution, and file-based runtime-host identity
persistence.

ADR-0027 defines normalized northbound Property operations.

ADR-0028 defines normalized northbound Command execution.

ADR-0029 defines normalized northbound live observation with an authoritative
initial snapshot, subscription-local sequence boundaries, bounded independent
subscriptions, explicit observation-gap termination, and no offline Event queue
or replay.

ADR-0030 maps those completed application services to a versioned ASP.NET Core
gRPC API over HTTP/2. The mapping provides unary snapshot, Property, and Command
operations and server-streaming observation. It preserves runtime-host
ownership, uses explicit protobuf contracts, and remains restricted to loopback
interfaces.

The verified baseline before this ADR is:

```text
Commit 8daa6f3e03e859e2241a6d1b3dc045220e4f36c9
2,520 automated tests pass
.NET solution builds
ADR-0030 remote gRPC mapping is complete
IPv4 loopback HTTP/2 gRPC integration passes
IPv6 loopback HTTP/2 gRPC integration passes where supported
Wildcard and non-loopback bindings are rejected
```

ADR-0030 deliberately does not approve production non-loopback exposure. It
requires a separate security decision to define:

- runtime-host and client authentication;
- authorization and operation permissions;
- transport encryption and trust;
- credential enrollment, storage, rotation, revocation, and recovery;
- audit behavior, retention, and privacy;
- safe diagnostics;
- denial-of-service and resource-limit policy;
- deployment and update assumptions;
- interaction with Tailscale or another network boundary.

The northbound API can read authoritative physical state, write physical
Properties, execute Commands, and observe transient Events. A remotely exposed
runtime host is therefore a security boundary around physical capabilities, not
only a data service.

The security architecture must preserve these existing invariants:

- physical endpoints do not authenticate northbound applications;
- endpoint transports and protocols remain private to the runtime host;
- the runtime host remains the sole owner of endpoint lifecycle;
- remote clients receive operational access only;
- attachment generation remains part of every generation-scoped target;
- normalized application outcomes remain distinct from transport failures;
- Command execution is never retried automatically after an ambiguous outcome;
- observation remains bounded and has explicit gap semantics;
- native and compact endpoints remain indistinguishable above the normalized
  northbound boundary.

The architecture must also support HASE's intended deployment model:

```text
Remote application
    -> private or routed network reachability
    -> authenticated and encrypted gRPC connection
    -> runtime-host security boundary
    -> authorized northbound operation
    -> transport-independent northbound application service
    -> host-owned endpoint lifecycle and operation routing
    -> native or compact physical endpoint
```

Tailscale may provide private reachability and an encrypted network overlay, but
network membership alone does not define the complete HASE application identity,
authorization, audit, credential, or resource-governance policy.

---

# Decision

The HASE runtime host is the complete northbound remote API security boundary.

Every non-loopback northbound connection must be:

1. encrypted in transit;
2. authenticated before an application RPC is executed;
3. associated with one stable HASE client principal;
4. authorized for the requested operation;
5. subject to bounded resource policy;
6. represented in security audit records.

Loopback development remains a separate trusted-local profile. Cleartext HTTP/2
may remain available only on enforced loopback interfaces for local development,
automated tests, and process integration verification.

No cleartext non-loopback northbound connection is permitted.

## Security responsibilities

The security boundary owns:

- server identity presentation;
- remote client authentication;
- authenticated-principal construction;
- authorization policy evaluation;
- transport-security enforcement;
- credential trust and revocation checks;
- security audit generation;
- safe remote denial behavior;
- per-principal and host-wide resource limits;
- security-sensitive configuration validation;
- rejection of insecure non-loopback startup;
- graceful cancellation and shutdown of authenticated calls.

The security boundary does not own:

- physical endpoint discovery;
- endpoint selection, attachment, replacement, or detachment;
- transport or protocol connection ownership;
- endpoint synchronization or supervision;
- Property-cache authority;
- Command-result interpretation;
- observation sequencing or gap detection;
- native or compact southbound message handling;
- endpoint firmware authentication of remote applications.

Those responsibilities remain below the northbound application boundary.

## Deployment profiles

HASE defines two northbound hosting profiles.

### Trusted local development profile

The trusted local development profile:

- binds only to enforced loopback addresses;
- may use cleartext HTTP/2;
- does not require a remote client credential;
- exists for development, automated tests, diagnostics, and local process
  integration;
- must never be enabled on a non-loopback listener;
- must never silently expand its listener through configuration;
- must remain visibly distinguishable from a secured remote profile.

Permitted listener addresses remain:

```text
127.0.0.1
::1
localhost only when resolved and constrained to loopback
```

### Secured remote profile

The secured remote profile:

- may bind to an explicitly configured non-loopback address;
- requires TLS;
- requires authenticated clients;
- applies default-deny authorization;
- applies resource limits;
- emits security audit records;
- rejects startup when its mandatory security configuration is incomplete,
  invalid, expired, or insecure.

A wildcard listener is not approved merely because TLS and authentication are
configured. The listener address remains explicit deployment configuration and
must be reviewed independently.

## Transport encryption

Every non-loopback gRPC listener must use TLS.

The runtime host must present a server certificate whose identity is validated
by the client.

The runtime host must not:

- permit cleartext HTTP/2 on a non-loopback address;
- downgrade from TLS after a failed secure startup;
- accept an invalid server identity configuration and continue insecurely;
- expose a second unauthenticated non-loopback listener for convenience;
- document certificate-validation bypass as an operational procedure.

TLS termination must occur within the explicitly approved HASE deployment
boundary. A later deployment may introduce an approved reverse proxy or service
mesh, but that topology requires a separate decision defining where the trusted
termination boundary begins and how the runtime host authenticates the
forwarding component.

## Client authentication

Mutual TLS is the initial HASE remote-client authentication mechanism.

A secured remote connection must present a client certificate accepted by the
runtime host trust policy.

The runtime host constructs one authenticated HASE client principal from the
validated certificate.

The principal must use a stable application identity assigned during credential
enrollment. It must not use:

- a display name alone;
- a network address;
- a DNS name observed from the connection;
- a Tailscale IP address;
- an operating-system user name supplied by the client;
- arbitrary certificate subject text without enrollment validation;
- protobuf request fields controlled by the caller.

Certificate validation must include:

- chain or explicitly pinned trust validation;
- validity interval;
- intended client-authentication usage where applicable;
- revocation state according to the configured HASE trust model;
- mapping to one enrolled HASE client principal.

Failure to authenticate terminates the request before the northbound application
service is invoked.

## HASE client principal

The authenticated principal is an immutable security value for one accepted
connection and its RPCs.

It contains at least:

```text
Client principal identifier
Credential identifier
Authentication mechanism
Authentication time
Trust-policy version or identifier
```

The client principal identifier is stable across credential rotation when the
replacement credential is enrolled for the same application identity.

The credential identifier distinguishes individual credentials issued to that
principal so that one credential can be revoked without changing the
application identity.

The security principal is not a physical endpoint identity, runtime-host
identity, attachment generation, instrument identity, or operating-system
process identity.

## Authentication and authorization separation

Authentication answers:

```text
Which enrolled HASE client principal made this request?
```

Authorization answers:

```text
May this principal perform this operation on this target now?
```

Successful authentication never implies unrestricted authorization.

Authorization policy is evaluated after authentication and before invoking the
northbound application operation.

## Default-deny authorization

Secured remote access uses default-deny authorization.

An RPC is denied unless an explicit effective policy grants the authenticated
principal the required permission.

Absence of a policy, an unknown principal, an unknown permission, an ambiguous
policy result, a policy-loading failure, or an unsupported target constraint
results in denial.

Authorization denial:

- must not invoke the northbound application operation;
- must not execute or partially execute a physical operation;
- must not reveal whether a hidden target exists beyond the information allowed
  by the denial policy;
- must emit a security audit record;
- must use one stable remote authorization-failure mapping.

## Version 1 permissions

Authorization permissions are based on semantic northbound operations rather
than protobuf method-name string parsing.

The initial permission set is:

```text
runtime-host.snapshot.read
property.cached.read
property.authoritative.read
property.write
command.execute
observation.subscribe
```

Each gRPC method maps to exactly one required base permission:

```text
GetSnapshot                 -> runtime-host.snapshot.read
ReadCachedProperty          -> property.cached.read
ReadAuthoritativeProperty   -> property.authoritative.read
WriteProperty               -> property.write
ExecuteCommand              -> command.execute
Observe                     -> observation.subscribe
```

Permissions are explicit and independently grantable. For example, permission
to read cached Property values does not imply permission to perform
authoritative endpoint reads, write Properties, execute Commands, or subscribe
to observations.

The permission model is additive only through explicit policy. No permission
inherits another permission implicitly in version 1.

## Target constraints

A policy grant may optionally constrain access by the immutable target
identities already defined by the northbound application contract:

- authoritative `EndpointId`;
- opaque attachment generation;
- `InstrumentId`;
- logical Property path;
- logical Command path.

Observation authorization may optionally constrain the set of observation kinds
or published endpoint identities that may be delivered.

The first secured implementation may support only host-wide permissions if
target-constrained policy is not implemented yet. In that case:

- target constraints must not be silently accepted and ignored;
- unsupported constrained policies must fail closed;
- documentation must identify the implemented policy granularity;
- no inferred or partial matching is permitted.

Authorization does not create another attachment-generation authority. The
existing application service remains authoritative for generation validation
after the request has passed authorization.

## Snapshot and descriptor visibility

`GetSnapshot` exposes runtime-host identity, published endpoint identities,
attachment generations, connection status, and endpoint descriptors.

Snapshot permission is therefore explicit and separate.

A client without `runtime-host.snapshot.read` cannot obtain the snapshot through
another RPC error, diagnostic response, audit endpoint, reflection service,
health endpoint, or authorization side channel.

The first remote API version does not add field-level redaction to snapshots.
When snapshot access is granted, the complete version 1 snapshot contract is
returned. Field-level security views require a separate contract decision.

## Property authorization

A cached Property read requires `property.cached.read`.

An authoritative Property read requires `property.authoritative.read`.

A Property write requires `property.write`.

Permission to read does not imply permission to write. Permission to read the
cache does not imply permission to generate physical endpoint traffic through
an authoritative read.

Authorization occurs before invoking the normalized Property service.

The existing normalized Property service remains authoritative for:

- target existence;
- attachment-generation matching;
- Property access support;
- endpoint availability;
- endpoint-confirmed writes;
- result status;
- cache updates.

Authorization denial is not represented as a normalized Property result because
the application operation was not invoked.

## Command authorization

Command execution requires `command.execute`.

Authorization occurs before invoking the normalized Command service and before
the command argument is passed into the application operation.

A denied Command:

- is never submitted to an endpoint;
- is never retried;
- never updates a Property cache;
- produces a security audit record without recording the Command argument.

The normalized Command service remains authoritative for target validation,
generation matching, endpoint execution, timeout handling, result status, and
optional return value.

Authorization denial is not represented as a normalized Command result because
the application operation was not invoked.

## Observation authorization

Opening an observation stream requires `observation.subscribe`.

Authorization is evaluated before the application observation subscription is
opened.

A denied observation request:

- does not create an application subscription;
- does not receive an initial snapshot;
- does not receive a sequence boundary;
- does not receive lifecycle, connection, Property, or Event observations.

For an authorized stream, the initial snapshot and all later observations must
remain within the same authorization scope for the complete stream lifetime.

Policy changes, principal revocation, credential expiry, or trust revocation may
require an active stream to terminate. The implementation must define one
deterministic revalidation strategy before secured observation is considered
complete.

The initial implementation may choose either:

1. authorization fixed for the lifetime of one accepted stream, with revocation
   enforced by terminating active sessions through a credential or policy-change
   signal; or
2. explicit periodic or event-driven reauthorization.

It must not continue indefinitely after known credential revocation merely
because the stream was authorized at creation.

Observation filtering must not be implemented accidentally in the gRPC adapter.
If target- or kind-filtered authorization is introduced, it must preserve
ADR-0029 ordering, initial-snapshot consistency, sequence semantics, bounded
delivery, and explicit gap behavior.

## Credential lifecycle

Remote credentials are configuration-owned security assets, not endpoint
descriptors or protobuf application data.

The credential lifecycle must support:

- enrollment;
- secure private-key storage;
- trust establishment;
- activation;
- expiry;
- planned rotation;
- revocation;
- replacement;
- recovery from a lost or compromised credential.

Runtime-host server credentials and remote-client credentials have independent
lifecycles.

Private keys must never be:

- committed to the HASE repository;
- embedded in firmware;
- included in endpoint descriptors;
- serialized through the northbound API;
- written to ordinary diagnostic logs;
- included in test fixtures outside explicitly generated test credentials;
- packaged in downloadable implementation archives.

Production private-key storage must use an operating-system or deployment
facility appropriate to the host. Exact Windows and Linux storage mechanisms
are implementation decisions that must preserve the requirements of this ADR.

Credential rotation should not require changing the stable HASE client principal
identifier.

A revoked or expired credential must not authenticate new connections.

The implementation must define how active connections and observation streams
are terminated after known revocation.

## Trust configuration

Trust configuration is explicit and fail-closed.

The secured remote profile must not start when:

- no server credential is available;
- the server credential is expired or not yet valid;
- the private key is unavailable;
- client-authentication trust is empty where remote clients are required;
- trust configuration cannot be parsed;
- configured policy cannot be loaded safely;
- revocation configuration is required but unavailable;
- a cleartext non-loopback listener is configured;
- a development-only authentication bypass is combined with a non-loopback
  listener.

Trust changes must be auditable.

Whether trust and policy changes are hot-reloaded or require restart is an
implementation decision. In either case, failure to apply a new configuration
must preserve the last known secure state or stop the secured listener. It must
not fall back to an insecure state.

## Audit boundary

Every secured remote RPC attempt produces a security audit event.

The minimum audit event contains:

```text
UTC timestamp
RuntimeHostId
Client principal identifier when authenticated
Credential identifier when available
Authentication outcome
Authorization outcome
RPC operation
Target identity metadata required to identify the requested resource
Result category
Correlation identifier
Remote network metadata at an approved privacy level
```

For generation-scoped operations, target metadata includes the supplied
authoritative `EndpointId` and opaque attachment generation.

For Property and Command operations, target metadata may include
`InstrumentId` and logical operation path.

Audit events must not contain:

- Property values;
- previous or current cached values;
- Command arguments;
- Command return values;
- Event values;
- complete endpoint descriptors;
- client private keys;
- server private keys;
- bearer secrets;
- raw certificates unless an explicitly approved fingerprint or identifier is
  required;
- raw native protocol frames;
- raw compact serial frames;
- stack traces;
- arbitrary exception text;
- unbounded protobuf payloads.

Audit distinguishes at least:

- unauthenticated rejection;
- authenticated but unauthorized rejection;
- accepted application invocation;
- normalized application result category;
- request cancellation;
- deadline expiry;
- observation-gap termination;
- server shutdown;
- unexpected internal failure.

Audit recording must not reinterpret a failed or ambiguous Command as successful.

The application result and the audit-write result are separate. The
implementation must define a fail-safe audit-delivery policy before secured
remote exposure is enabled. Silent loss of required audit events is not
acceptable.

## Audit time and correlation

Audit timestamps use UTC.

Each remote request receives one correlation identifier generated or validated
at the security boundary.

A client-supplied correlation value may be accepted only under explicit length,
format, and safety limits. The runtime host must retain its own authoritative
correlation value.

Correlation identifiers are diagnostic and audit values. They do not become
endpoint protocol correlation identifiers, attachment generations, replay
tokens, or authorization credentials.

## Safe diagnostics

Remote diagnostics must be safe by default.

Authentication and authorization failures must not disclose:

- trust-store contents;
- enrolled-principal lists;
- certificate chain internals beyond the stable public failure mapping;
- policy source paths;
- local file paths;
- stack traces;
- endpoint transport details;
- raw protocol frames;
- private host configuration;
- whether a concealed endpoint, instrument, Property, or Command exists.

Detailed diagnostics may be written to protected local operational logs when
permitted by policy, but those logs remain separate from remote error messages
and security audit data.

Normalized Property and Command application outcomes remain response data only
after authentication and authorization succeeded and the application operation
was invoked.

## Resource governance

Authentication does not grant unlimited resource use.

The secured remote profile must define bounded limits for at least:

- concurrent connections;
- concurrent RPCs per principal;
- concurrent observation streams per principal;
- host-wide observation streams;
- request message size;
- response message size where configurable;
- request-header size;
- connection and stream idle time;
- operation deadlines;
- authentication handshake rate;
- failed-authentication rate;
- audit buffering;
- graceful-shutdown duration.

Limits must be explicit, finite, testable, and safe for the runtime host.

Resource rejection:

- occurs before allocating unbounded work;
- does not invoke a denied application operation;
- does not affect endpoint lifecycle;
- uses one stable remote failure mapping;
- emits an audit event at an appropriate rate without enabling log flooding.

The security layer must not add an unbounded observation queue. ADR-0029 bounded
application subscriptions remain authoritative.

## Denial-of-service behavior

The runtime host is not required to resist arbitrary Internet-scale attack in
this phase, but it must fail safely under malformed, unauthorized, excessive,
slow, or abandoned client activity.

A client must not be able to:

- create unbounded observation subscriptions;
- consume unbounded memory through request bodies or queued responses;
- block endpoint observer callbacks;
- block another principal indefinitely;
- disable audit through log flooding;
- force automatic Command retries;
- detach or replace physical endpoints by cancelling calls;
- cause insecure fallback after TLS or authentication failure.

Public Internet exposure is not approved by this ADR.

## Tailscale boundary

Tailscale may be used as a private network path to a secured HASE runtime host.

Tailscale provides network reachability and overlay-network security according
to its own configuration.

HASE still requires:

- TLS for the secured gRPC listener;
- HASE client authentication;
- HASE client-principal mapping;
- HASE authorization;
- HASE resource limits;
- HASE audit records;
- explicit runtime-host listener configuration.

A Tailscale node identity, tag, IP address, ACL, or network membership is not by
itself a HASE client principal or HASE permission grant.

A later integration may use verified Tailscale identity as an additional
authentication claim or policy input, but that requires a separate decision and
must not weaken the base HASE security guarantees.

Tailscale runtime-host discovery remains outside this ADR.

## Reverse proxies and TLS termination

Direct TLS termination in the HASE ASP.NET Core host is the initial approved
topology.

A reverse proxy, ingress controller, service mesh, or external TLS terminator is
not approved automatically.

Such a topology requires a separate architecture decision defining:

- the trusted proxy identity;
- authentication between the proxy and runtime host;
- whether client-certificate identity is forwarded and how it is protected;
- listener exposure between proxy and runtime host;
- spoofing prevention for forwarded identity headers;
- audit attribution;
- failure and revocation behavior.

The runtime host must never trust caller-controlled forwarding headers as an
authenticated principal.

## Reflection, health, and diagnostics endpoints

Production service reflection is disabled unless separately approved.

Any remote health or diagnostics endpoint is part of the same security boundary.

A health endpoint must not expose:

- runtime-host snapshots;
- endpoint identities;
- descriptors;
- connection details;
- credentials;
- policy contents;
- audit data.

Whether a minimal authenticated or unauthenticated liveness endpoint is needed
is an implementation decision requiring explicit review.

The secured profile must not retain development-only endpoints by default.

## Endpoint lifecycle ownership

Security enforcement does not transfer physical lifecycle ownership.

No permission in this ADR authorizes RPCs to:

- discover endpoints;
- attach endpoints;
- detach endpoints;
- replace attachments;
- create or dispose endpoint connections;
- change supervision policy;
- change reconnect policy;
- start or stop endpoint protocols;
- shut down the runtime host.

Closing a TLS connection, rejecting authentication, denying authorization,
expiring a credential, revoking a credential, cancelling an RPC, or terminating
an observation stream never detaches a physical endpoint.

Remote lifecycle administration requires a separate ADR and API contract.

## Southbound protocols and firmware

Native Protocol Version 1 and Compact Serial Protocol Version 1 remain
unchanged.

Physical endpoints do not receive, validate, or store remote client
certificates.

Physical endpoints do not evaluate remote authorization permissions.

The runtime host remains the sole mediator between authenticated northbound
principals and physical endpoint operations.

No security detail from the northbound caller is forwarded into native or
compact endpoint protocols unless a future explicitly reviewed protocol version
defines such behavior.

## Dependency direction

The approved dependency direction is:

```text
Remote application
    -> TLS and mutual authentication
    -> northbound security boundary
    -> versioned gRPC adapter
    -> Hase.Runtime.Northbound application services
    -> authoritative attachment projection
    -> runtime model and host-owned operation routing
    -> native or compact endpoint integration
```

`Hase.Runtime.Northbound` must remain independent of:

- ASP.NET Core authentication middleware;
- TLS certificate types;
- X.509 implementation types;
- authorization-provider packages;
- remote audit sinks;
- Tailscale SDKs;
- generated protobuf security types.

Security implementation belongs at the remote hosting and adapter composition
edge.

Reusable security abstractions may be introduced in a dedicated project when
their contracts are stable. Dependencies still point inward toward the
transport-independent northbound services and never from runtime, transport,
protocol, or endpoint projects toward remote hosting.

---

# Rejected alternatives

## Treat the local network as trusted

Rejected because LAN reachability does not establish application identity,
authorization, revocation, audit attribution, or protection from another host
on the network.

## Treat Tailscale membership as complete HASE authorization

Rejected because Tailscale reachability and ACLs do not replace HASE
application-principal mapping, operation permissions, audit behavior, credential
lifecycle, or resource policy.

## Permit cleartext gRPC over a private network

Rejected because private addressing does not provide an adequate application
security boundary and can change through routing, bridging, container, VPN, or
deployment configuration.

## Use server-only TLS with anonymous clients

Rejected because encryption alone does not identify the calling application and
cannot support principal-specific authorization, revocation, limits, or audit.

## Authenticate using client IP addresses

Rejected because addresses are routing values, may change, may be shared, and do
not provide cryptographic application identity.

## Authenticate using a client-supplied identifier in protobuf

Rejected because the caller controls the request and could impersonate another
application.

## Give every authenticated client full access

Rejected because authentication and authorization are separate and the remote
API includes state-changing Property writes and Command execution.

## Infer permissions from protobuf method names at runtime

Rejected because authorization must use explicit semantic permissions with
stable reviewed mappings.

## Permit by default when policy is absent

Rejected because missing or failed security configuration must fail closed.

## Put authorization in endpoint firmware

Rejected because remote-client policy belongs to the runtime-host security
boundary and must not fragment across physical endpoint capabilities.

## Forward remote credentials to endpoints

Rejected because southbound protocols are not remote identity protocols and
physical endpoints must remain independent of northbound credential schemes.

## Add automatic Command retry after authentication or connection failure

Rejected because authentication does not change ADR-0028 exactly-once
submission semantics or make ambiguous execution safe to retry.

## Log Property values and Command arguments for audit

Rejected because security audit must identify access and outcome without
capturing potentially sensitive operational payloads.

## Use one shared client certificate for every application

Rejected because individual application identity, revocation, policy, limits,
and audit attribution require distinct enrolled credentials or distinct
principal mappings.

## Accept self-signed client certificates without enrollment

Rejected because cryptographic possession alone does not establish an approved
HASE principal or policy assignment.

## Allow development authentication bypass on a remote listener

Rejected because development convenience must remain inseparable from enforced
loopback binding.

## Fall back to loopback cleartext after secured-listener startup failure

Rejected because fallback can conceal security misconfiguration. The secured
profile must fail startup instead.

## Trust reverse-proxy identity headers by default

Rejected because caller-controlled or misconfigured forwarding headers allow
principal spoofing.

## Expose production gRPC reflection by default

Rejected because reflection expands discoverability and is not required for the
versioned client contract.

## Add unbounded security or audit queues

Rejected because a slow sink or malicious client could consume unbounded memory
and undermine runtime-host availability.

## Approve public Internet exposure

Rejected because this ADR defines the application security boundary for
controlled remote deployment, not an Internet-facing threat model.

---

# Initial implementation sequence

Implementation should proceed in small, independently buildable increments:

1. add and accept this ADR without changing the executable listener;
2. define immutable HASE client-principal and permission value contracts at the
   remote composition boundary;
3. define explicit RPC-to-permission mapping and default-deny authorization
   evaluation;
4. add authorization unit tests without enabling non-loopback binding;
5. define secured-host configuration separately from trusted loopback
   configuration;
6. add TLS server-identity configuration and validation;
7. add generated test certificates and TLS-only integration tests;
8. add mutual TLS client authentication and enrolled-principal mapping;
9. verify authentication failure occurs before application-service invocation;
10. integrate authorization before every unary application operation;
11. integrate authorization before opening an observation subscription;
12. define and implement bounded connection, RPC, and observation limits;
13. define immutable security audit events with prohibited-payload tests;
14. add a bounded audit delivery mechanism and explicit audit-failure behavior;
15. define credential expiry, rotation, and revocation behavior;
16. terminate or revalidate active observation streams after known revocation;
17. enforce fail-closed secured-host startup validation;
18. verify cleartext non-loopback hosting remains impossible;
19. verify loopback development hosting remains available and isolated;
20. perform secured process integration through generated gRPC clients;
21. perform controlled non-loopback validation over a private LAN or Tailscale
    path;
22. update ADR implementation verification, ProjectStatus, and Roadmap after all
    required security behavior is verified.

No implementation increment may expose an unauthenticated or cleartext
non-loopback listener.

---

# Consequences

## Positive

- The runtime host becomes one explicit security boundary for every physical
  endpoint family.
- Endpoint firmware and southbound protocols remain independent of remote-user
  security mechanisms.
- Mutual TLS provides cryptographic client authentication without placing a
  bearer secret in each RPC.
- Stable client principals remain independent of individual credential rotation.
- Default-deny authorization prevents authentication from becoming unrestricted
  physical control.
- Semantic permissions separate cached reads, physical reads, writes, Commands,
  snapshots, and observation.
- Security audit records identify access without recording Property values or
  Command payloads.
- Resource limits preserve bounded runtime-host behavior.
- Tailscale can provide private reachability without becoming an implicit
  application authorization system.
- Trusted loopback development remains simple and independently testable.
- Existing normalized Property, Command, and observation semantics remain
  authoritative.
- Physical endpoint lifecycle ownership remains unchanged.

## Costs

- Server and client certificate lifecycle must be implemented and operated.
- Each remote application requires enrollment, credential storage, rotation, and
  revocation procedures.
- Authorization policy requires explicit configuration and tests.
- Active streaming RPCs require defined revocation and policy-change behavior.
- Audit delivery and failure behavior add operational complexity.
- Resource governance requires integration and load verification.
- Windows and Linux credential storage need platform-appropriate implementations.
- Secured process integration is more complex than loopback cleartext testing.
- Reverse proxies and alternate identity providers require separate decisions.
- A secured non-loopback listener cannot be enabled until the mandatory pieces
  are implemented and verified.

---

# Scope exclusions

This decision does not define or introduce:

- remote endpoint discovery;
- remote attachment, detachment, replacement, or shutdown;
- runtime-host shutdown administration;
- persistent Property or Event history;
- Event replay or offline Event queues;
- durable observation cursors;
- browser authentication or gRPC-Web;
- REST or JSON compatibility endpoints;
- OAuth, OpenID Connect, Entra ID, Kerberos, or operating-system integrated
  authentication;
- API keys or bearer-token authentication;
- Tailscale identity as a HASE principal;
- Tailscale runtime-host discovery;
- reverse-proxy deployment;
- service-mesh deployment;
- public Internet exposure;
- field-level snapshot redaction;
- remote policy administration;
- remote credential enrollment;
- a centralized multi-host certificate authority service;
- exact production audit retention duration;
- exact Windows or Linux private-key storage technology;
- endpoint firmware certificates for northbound clients;
- changes to native or compact southbound protocols.

Those require implementation decisions or separate reviewed ADRs.

---

# Verification requirements

Automated verification must demonstrate:

- trusted-local hosting still binds only to loopback;
- trusted-local cleartext HTTP/2 cannot be configured on a non-loopback address;
- secured hosting requires TLS;
- secured hosting rejects incomplete or invalid server credentials;
- secured hosting requires client authentication;
- an unauthenticated request never invokes a northbound application service;
- an untrusted, expired, not-yet-valid, or revoked client credential is rejected;
- one accepted credential maps to one stable HASE client principal;
- credential identity remains distinct from client-principal identity;
- every RPC maps to its explicit semantic permission;
- missing policy denies access;
- unknown principal denies access;
- unsupported policy constraints deny access;
- authorization occurs before application-service invocation;
- authorization denial never executes a Property write or Command;
- authorization denial never opens an observation subscription;
- snapshot permission is independent from Property, Command, and observation
  permissions;
- cached and authoritative Property-read permissions are independent;
- Property-write permission is independent from read permissions;
- Command permission is independent from Property permissions;
- observation permission is independent from snapshot permission;
- authorized normalized Property and Command outcomes remain response data;
- authentication and authorization failures remain transport/security failures;
- remote errors do not expose stack traces, local paths, credentials, trust
  contents, policy contents, or concealed target existence;
- every secured RPC attempt emits the required audit category;
- audit records use UTC;
- audit records include runtime-host and client-principal identity when known;
- generation-scoped audit records include the supplied endpoint identity and
  opaque generation;
- audit records exclude Property values, Command arguments, Command return
  values, Event values, complete descriptors, secrets, and raw protocol frames;
- audit buffering is bounded;
- defined audit-delivery failure behavior is enforced;
- connection, RPC, request-size, and observation limits are finite;
- per-principal and host-wide observation limits are enforced;
- excessive or abandoned clients do not create unbounded queues;
- one principal cannot cancel or dispose another principal's observation stream;
- cancellation, deadline expiry, revocation, and shutdown dispose subscriptions
  deterministically;
- known credential revocation prevents new calls;
- defined active-stream revocation behavior is enforced;
- security cancellation never detaches or replaces a physical endpoint;
- no security layer retries a Command automatically;
- no security layer speculatively updates a Property cache;
- wildcard binding is rejected unless separately approved by a later ADR;
- no unauthenticated non-loopback listener is opened;
- no cleartext non-loopback listener is opened;
- secured IPv4 non-loopback process integration succeeds on an explicitly
  configured private address;
- secured IPv6 non-loopback process integration succeeds where supported;
- secured operation through a Tailscale path, when tested, still requires HASE
  TLS, client authentication, authorization, limits, and audit;
- `Hase.Runtime.Northbound` remains independent of ASP.NET Core authentication,
  certificate implementation types, authorization providers, audit sinks,
  Tailscale SDKs, and protobuf security types;
- native and compact endpoint protocols remain unchanged;
- remote security behavior is identical above both physical endpoint families.

Physical validation must eventually demonstrate for both Native Protocol Version
1 and Compact Serial Protocol Version 1:

1. start a runtime host with an explicitly configured secured non-loopback
   listener;
2. connect with a trusted enrolled client credential;
3. reject an untrusted client credential;
4. reject an authenticated principal without the required permission;
5. retrieve an authorized runtime-host snapshot;
6. perform an authorized cached Property read;
7. perform an authorized physical authoritative Property read;
8. perform an authorized endpoint-confirmed Property write where supported;
9. perform an authorized exactly-once Command where supported;
10. open an authorized observation stream and receive its initial snapshot first;
11. receive later authorized lifecycle, Property, and Event observations;
12. verify audit records for accepted and rejected calls without operational
    payload values;
13. revoke or expire a client credential and verify the defined new-call and
    active-stream behavior;
14. disconnect the remote client and confirm that physical endpoint lifecycle
    remains owned by the runtime host;
15. shut down the secured host and confirm deterministic RPC and subscription
    disposal.

---

# Stop condition

ADR-0031 is complete for architectural review when:

- the runtime host is explicitly established as the northbound security boundary;
- trusted loopback development and secured remote hosting are separate profiles;
- every non-loopback connection requires TLS;
- mutual TLS is selected as the initial client-authentication mechanism;
- stable HASE client principals are distinct from credentials;
- authentication and authorization are explicitly separate;
- default-deny semantic permissions are defined for every version 1 RPC;
- authorization precedes application-service invocation;
- credential lifecycle and fail-closed trust behavior are defined;
- audit contents and prohibited payloads are defined;
- bounded resource and denial-of-service behavior are defined;
- Tailscale remains network reachability rather than implicit HASE authorization;
- endpoint lifecycle ownership and southbound protocols remain unchanged;
- non-loopback exposure remains prohibited until implementation verification is
  complete.

The ADR is accepted. Implementation may proceed in the reviewed incremental sequence.
