# C-032 - Authenticated Physical Northbound gRPC Validation

## Status

**Completed, automated, and physically verified**

Verified baseline:

```text
2,813 automated tests pass
.NET solution builds
Authenticated authoritative Property RPC succeeds through mutual TLS
Physical ESP32 temperature read succeeds through the secured gRPC host
Missing client certificate is rejected before Property-service execution
Unenrolled client certificate is rejected before Property-service execution
Physical endpoint detaches orderly and finishes Disconnected
```

## Purpose

C-032 validates the complete secured northbound path against a physical
endpoint. It composes:

- the runtime-host-owned native endpoint attachment lifecycle;
- the normalized northbound snapshot and Property services;
- the version 1 gRPC remote contract;
- the C-031 HTTPS/HTTP/2 mutual-TLS host;
- the C-030 enrolled-certificate authentication pipeline;
- an authenticated generated gRPC client.

The capability proves that mutual TLS and HASE authentication preserve the
existing authoritative Property semantics. It does not introduce remote
lifecycle administration or production non-loopback exposure.

## Validated Path

```text
Physical ESP32/BME280
    -> native Protocol Version 1 attachment
    -> runtime-host attachment inventory
    -> published attachment generation
    -> normalized authoritative Property service
    -> mutual-TLS HTTPS/HTTP/2 gRPC host
    -> enrolled client-01 certificate
    -> ReadAuthoritativeProperty RPC
    -> confirmed protobuf Property value
```

The runtime host remains the sole owner of attachment, synchronization,
connection supervision, detachment, and disposal.

## Secure Validation Composition

Protocol Explorer creates one isolated validation credential set:

- ephemeral self-signed certification authority;
- CA-issued localhost server certificate;
- CA-issued client certificate;
- server-authentication and client-authentication extended key usages;
- localhost DNS and IPv4 loopback subject alternative names;
- PKCS#12 export and reload for network-stack compatibility.

The validation client certificate is enrolled as:

```text
Principal    : client-01
Trust policy : c032-physical-validation-v1
```

The credential set is process-local, does not mutate the machine certificate
store, and is disposed after each run. It validates composition and framework
behavior; it is not a production provisioning or rotation mechanism.

## Automated Verification

Automated tests verify:

- secure Property-host dependency composition;
- target and result mapper registration;
- enrolled certificate authentication as `client-01`;
- authenticated `ReadAuthoritativeProperty` execution through real
  HTTPS/HTTP/2 gRPC;
- exact preservation of endpoint identity, attachment generation, instrument
  identity, and Property identity;
- authoritative `IRuntimeHostPropertyService.ReadAsync` execution;
- confirmed Property-value protobuf mapping;
- no use of the cached or write paths;
- `StatusCode.Unauthenticated` for a structurally valid but unenrolled
  certificate;
- `StatusCode.Unavailable` when no client certificate is presented;
- zero Property-service executions for both rejected requests;
- IPv4 loopback and ephemeral-port host startup;
- expected-server-certificate validation by thumbprint;
- deterministic host, client, channel, handler, and certificate disposal.

## Physical Verification

Physical validation used:

```text
Endpoint family       : Native Protocol Version 1
Physical endpoint     : doit-esp32-devkitc-v4-01
Physical address      : 192.168.0.223:5000
Remote transport      : HTTPS / HTTP/2 gRPC
Remote binding        : IPv4 loopback, ephemeral port
Authenticated client  : client-01
Property              : physical.environment-sensor.temperature
```

Observed result:

```text
Runtime host          : protocol-explorer-authenticated-physical-validation
API version           : 1.0
Connection state      : Ready
Authoritative value   : 25.290000915527344
Timestamp             : 2026-07-26T09:47:36.3910000+00:00
Quality               : Good
Orderly detachment    : True
Final connection state: Disconnected
Process exit code     : 0
```

The concrete attachment generation and ephemeral HTTPS port are intentionally
run-specific.

## Preserved Semantics

- The physical endpoint remains authoritative.
- The remote target includes authoritative endpoint identity and the expected
  attachment generation.
- The authoritative RPC communicates with the endpoint and does not substitute
  the runtime cache.
- Authentication completes before the gRPC operation executes.
- Rejected credentials never reach the Property service.
- Network reachability does not grant HASE authority.
- Successful TLS negotiation alone does not grant HASE authority.
- The gRPC adapter remains above transport-independent application services.
- Endpoint lifecycle ownership remains local to the runtime host.
- Remote lifecycle administration is not introduced.
- Production non-loopback exposure remains prohibited.

## Result

C-032 closes the authenticated physical Property-validation gap left after
C-031. The security boundary is now proven through a real endpoint operation,
not only through synthetic snapshot and Property services.

Production non-loopback deployment, persistent credential provisioning,
rotation, revocation, audit, Tailscale reachability, and remote lifecycle
administration remain separately approved backlog.
