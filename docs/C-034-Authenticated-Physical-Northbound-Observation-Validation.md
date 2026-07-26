# C-034 - Authenticated Physical Northbound Observation Validation

## Status

**Completed, automated, and physically verified**

Verified baseline:

```text
2,850 automated tests pass
.NET solution builds
Authenticated server-streaming Observe RPC succeeds through mutual TLS
Physical ESP32 attachment lifecycle is observed through the secured gRPC host
BME280 PropertyValueChanged is observed
GPIO17 Controller.ButtonPressed is observed
Missing client certificate is rejected before subscription creation
Unenrolled client certificate is rejected before subscription creation
Physical endpoint detaches orderly
Process exits with code 0
```

## Purpose

C-034 validates authenticated northbound live observation against a physical
native endpoint. It composes:

- configured physical ESP32 attachment through Native Protocol Version 1;
- runtime-host-owned endpoint synchronization and lifecycle management;
- normalized northbound live observation;
- the version 1 server-streaming gRPC contract;
- the C-031 HTTPS/HTTP/2 mutual-TLS host;
- the C-030 enrolled-certificate authentication pipeline;
- an authenticated generated gRPC client.

The capability proves that authentication protects the streaming `Observe`
operation itself and preserves the existing snapshot-first, sequence, identity,
Property, Event, and lifecycle semantics.

## Validated Path

```text
Physical ESP32/BME280
    -> Native Protocol Version 1 over framed TCP
    -> runtime-host-owned attachment
    -> normalized northbound observation service
    -> mutual-TLS HTTPS/HTTP/2 gRPC host
    -> enrolled client-01 certificate
    -> initial snapshot
    -> AttachmentPublished
    -> PropertyValueChanged
    -> EventOccurred
    -> AttachmentEnded
```

The runtime host remains the sole owner of the physical connection,
synchronization, recovery, observation routing, detachment, and disposal.

## Secure Observation-Host Composition

C-034 extends the mutual-TLS host composition with:

- `IRuntimeHostObservationService`;
- `IObservationInitialSnapshotMapper`;
- `IRuntimeHostObservationMapper`;
- the existing version 1 observation payload mapper graph.

Authentication middleware remains before the gRPC endpoint. Rejected
credentials cannot open a normalized observation subscription.

The Protocol Explorer validation composition owns:

- the ASP.NET Core application;
- the IPv4-loopback Kestrel listener and ephemeral HTTPS port;
- the isolated validation certificates;
- the certificate-authentication pipeline;
- the generated gRPC channel and HTTP handler;
- the server-streaming gRPC call.

## Automated Verification

Automated tests verify:

- secure observation-host dependency composition;
- required observation-service validation;
- initial-snapshot and observation mapper registration;
- authentication service registration;
- enrolled certificate authentication as `client-01`;
- authenticated `Observe` execution through real HTTPS/HTTP/2 gRPC;
- initial snapshot delivery before live observations;
- observation sequence and Event payload mapping;
- exact subscription opening and disposal;
- missing-certificate rejection before `OpenSubscriptionAsync`;
- unenrolled-certificate rejection before `OpenSubscriptionAsync`;
- strict HTTPS client validation;
- explicit client-certificate selection;
- exact validation-server certificate matching;
- deterministic host, client, channel, handler, call, and certificate disposal;
- strict C-034 command-line argument parsing;
- Protocol Explorer scenario registration.

## Physical Verification

Physical validation used:

```text
Endpoint family       : Native Protocol Version 1
Physical host         : 192.168.0.223
Physical port         : 5000
Physical endpoint     : doit-esp32-devkitc-v4-01
Remote transport      : HTTPS / HTTP/2 gRPC
Remote binding        : IPv4 loopback, ephemeral port
Client authentication : Mutual TLS
Authenticated client  : client-01
Physical Event        : GPIO17 Controller.ButtonPressed
Initial snapshot       : Empty before attachment
Sequence scope         : Subscription-local
Replay                 : None
```

Observed result:

```text
Sequence 1 : AttachmentPublished
Sequence 2 : PropertyValueChanged
Sequence 3 : EventOccurred
Sequence 4 : AttachmentEnded

Runtime host          : protocol-explorer-authenticated-observation-validation
API version           : 1.0
Published endpoint    : doit-esp32-devkitc-v4-01
Authenticated principal: client-01
Final sequence        : 4
Process exit code     : 0
```

The attachment generation and ephemeral HTTPS port are intentionally
run-specific.

## Stream Shutdown

The normalized observation subscription remains open after
`AttachmentEnded`, ready for later runtime-host observations. The physical
validation disposes its client call after receiving the required ending
milestone.

ASP.NET Core therefore records the request as cancelled and may log the
expected `OperationCanceledException` from the subscription's channel wait.
The gRPC request completes, the physical endpoint is already detached, and the
Protocol Explorer process exits with code `0`. This is normal streaming-call
shutdown and not a validation failure.

Suppressing expected cancellation logging without changing client cancellation
or deadline semantics remains an optional diagnostics refinement.

## Preserved Semantics

- The ESP32 remains authoritative.
- The configured target does not become authoritative endpoint identity.
- Native bootstrap supplies authoritative endpoint identity.
- Attachment occurs only after the authenticated stream's empty initial
  snapshot is received.
- Every observation retains the current attachment generation.
- Observation sequences are subscription-local and strictly increasing.
- Intermediate observations are retained and sequence-validated.
- Events remain transient and have no offline queue or replay.
- The physical button Event carries no value.
- Authentication completes before subscription creation.
- Rejected credentials never reach the observation service.
- Network reachability does not grant HASE authority.
- Successful TLS negotiation alone does not grant HASE authority.
- Endpoint lifecycle ownership remains local to the runtime host.
- Remote lifecycle administration is not introduced.
- Production non-loopback exposure remains prohibited.

## Result

C-034 closes the authenticated physical operational-validation set for the
current northbound API:

- C-032 proves authenticated authoritative Property access against the physical
  ESP32/BME280 endpoint;
- C-033 proves authenticated state-changing Command execution against the
  physical Arduino Uno;
- C-034 proves authenticated server-streaming observation against the physical
  ESP32/BME280 and GPIO17 endpoint.

Production non-loopback deployment, persistent credential provisioning,
rotation, revocation, audit, Tailscale reachability, remote lifecycle
administration, and optional expected-cancellation log suppression remain
separately approved backlog.
