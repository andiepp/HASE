# C-033 - Authenticated Physical Northbound Command Validation

## Status

**Completed, automated, and physically verified**

Verified baseline:

```text
2,831 automated tests pass
.NET solution builds
Authenticated Command RPC succeeds through mutual TLS
Physical Arduino Uno Led.Toggle succeeds through the secured gRPC host
Authoritative Led.State reads confirm Off -> On -> Off
Missing client certificate is rejected before Command-service execution
Unenrolled client certificate is rejected before Command-service execution
Physical endpoint detaches orderly and finishes Disconnected
```

## Purpose

C-033 validates an authenticated, state-changing northbound operation against
a physical compact endpoint. It composes:

- Windows USB serial candidate discovery and VID/PID filtering;
- authoritative Compact Serial Protocol bootstrap verification;
- explicit endpoint selection and runtime-host-owned attachment;
- normalized northbound Property and Command services;
- the version 1 gRPC remote contract;
- the C-031 HTTPS/HTTP/2 mutual-TLS host;
- the C-030 enrolled-certificate authentication pipeline;
- an authenticated generated gRPC client.

The capability proves that mutual TLS and HASE authentication preserve the
existing Command semantics and that rejected credentials cannot reach the
Command service. It does not introduce remote lifecycle administration or
production non-loopback exposure.

## Validated Path

```text
Physical Arduino Uno
    -> Compact Serial Protocol Version 1
    -> explicit runtime-host attachment
    -> published attachment generation
    -> normalized Property and Command services
    -> mutual-TLS HTTPS/HTTP/2 gRPC host
    -> enrolled client-01 certificate
    -> ReadAuthoritativeProperty RPC
    -> ExecuteCommand RPC
    -> ReadAuthoritativeProperty RPC
    -> ExecuteCommand RPC
    -> ReadAuthoritativeProperty RPC
```

The runtime host remains the sole owner of discovery-derived attachment,
compact synchronization, connection supervision, detachment, and disposal.

## Secure Command-Host Composition

C-033 extends the mutual-TLS host composition with:

- `IRuntimeHostCommandService`;
- `IRuntimeHostCommandTargetMapper`;
- `IRuntimeHostCommandOperationResultMapper`;
- the shared version 1 remote-value mapper;
- optional Property operations for authoritative state confirmation.

Property and Command operations share one remote-value mapper when both are
enabled. Authentication middleware remains before the gRPC service endpoint.

The physical-validation host owns:

- the ASP.NET Core application;
- the IPv4-loopback Kestrel listener;
- the ephemeral validation certificates;
- the certificate-authentication pipeline;
- the generated gRPC channel and HTTP handler.

## Automated Verification

Automated tests verify:

- secure Command-host dependency composition;
- required Command-service validation;
- Command target and operation-result mapper registration;
- shared Property and Command remote-value mapping;
- enrolled certificate authentication as `client-01`;
- authenticated `ExecuteCommand` execution through real HTTPS/HTTP/2 gRPC;
- exact preservation of endpoint identity, attachment generation, instrument
  identity, Command path, and argument;
- `StatusCode.Unavailable` when no client certificate is presented;
- `StatusCode.Unauthenticated` for a valid but unenrolled client certificate;
- zero Command-service executions for both rejected requests;
- IPv4 loopback and ephemeral-port host startup;
- deterministic host, client, channel, handler, and certificate disposal;
- strict C-033 command-line argument parsing;
- Protocol Explorer scenario registration.

## Physical Verification

Physical validation used:

```text
Endpoint family       : Compact Serial Protocol V1
Candidate filter      : VID 0x2341, PID 0x0043
Physical endpoint     : arduino-uno-01
Verified port         : COM10
Baud rate             : 115200
Verification timeout  : 00:00:03
Remote transport      : HTTPS / HTTP/2 gRPC
Remote binding        : IPv4 loopback, ephemeral port
Authenticated client  : client-01
Command               : Led.Toggle
Authoritative Property: Led.State
```

Observed result:

```text
Runtime host          : protocol-explorer-authenticated-command-validation
API version           : 1.0
Connection state      : Ready
Original Property read: Off
Toggled Property read : On
Restored Property read: Off
Orderly detachment    : True
Final connection state: Disconnected
Process exit code     : 0
```

The concrete attachment generation and ephemeral HTTPS port are intentionally
run-specific.

## State Restoration

C-033 treats physical state restoration as part of successful validation:

1. Read the initial `Led.State` authoritatively through gRPC.
2. Execute authenticated `Led.Toggle`.
3. Read `Led.State` authoritatively and require the opposite value.
4. Execute authenticated `Led.Toggle` again.
5. Read `Led.State` authoritatively and require the original value.

The compact Command has no return payload. State confirmation therefore comes
only from authoritative Property reads against the physical endpoint.

## Preserved Semantics

- The Arduino endpoint remains authoritative.
- USB metadata identifies candidates only.
- Compact bootstrap supplies authoritative endpoint identity.
- Discovery never attaches automatically.
- The verified endpoint is selected explicitly before attachment.
- Every target includes the expected attachment generation.
- Command execution is never retried automatically.
- Command execution does not update the Property cache.
- State confirmation uses authoritative Property reads.
- Authentication completes before the gRPC operation executes.
- Rejected credentials never reach the Command service.
- Network reachability does not grant HASE authority.
- Successful TLS negotiation alone does not grant HASE authority.
- Endpoint lifecycle ownership remains local to the runtime host.
- Remote lifecycle administration is not introduced.
- Production non-loopback exposure remains prohibited.

## Result

C-033 closes the authenticated physical Command-validation gap left after
C-032. The secured northbound boundary is now physically proven for:

- authoritative Property access through the native ESP32 endpoint family;
- state-changing Command execution through the compact Arduino endpoint family.

Production non-loopback deployment, persistent credential provisioning,
rotation, revocation, audit, Tailscale reachability, authenticated streaming
observation validation, and remote lifecycle administration remain separately
approved backlog.
