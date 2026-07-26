# C-031 - Mutual-TLS Runtime-Host Integration

## Status

**Completed and automated**

Verified baseline:

```text
2,783 automated tests pass
.NET solution builds
Authenticated HTTP/2 gRPC request succeeds
Missing client certificate is rejected at the TLS boundary
Unenrolled client certificate is rejected by HASE authentication
Authenticated principal is projected into HttpContext.User
```

## Purpose

C-031 integrates the security boundary accepted in ADR-0031 with the existing
ASP.NET Core gRPC runtime host. It proves that the remote operational API can be
hosted through Kestrel using mutual TLS while the runtime host retains all
physical endpoint lifecycle ownership.

The capability composes the C-029 authorization boundary and C-030 certificate
authentication pipeline with the C-031 Kestrel host. It does not enable
production non-loopback exposure or remote lifecycle administration.

## Host Boundary

The runtime host is configured with:

- HTTPS only;
- HTTP/2 only;
- TLS 1.2 or TLS 1.3;
- a configured server certificate;
- a required client certificate;
- the HASE authentication pipeline before gRPC endpoint execution;
- loopback-only binding.

Kestrel establishes the encrypted channel and requires presentation of a client
certificate. HASE remains authoritative for client identity, certificate trust,
credential enrollment, and principal construction.

This separation prevents transport reachability or successful TLS negotiation
from granting HASE authority by itself.

## Authentication Flow

For an enrolled client:

```text
TLS client certificate
    -> certificate structure and chain trust
    -> credential enrollment
    -> HASE principal
    -> HttpContext.User
    -> gRPC service
```

The authenticated principal name is the enrolled HASE identity, not a
transport-derived substitute.

## Verified Outcomes

### Enrolled Client Certificate

An enrolled CA-issued client certificate:

- completes mutual-TLS negotiation;
- is accepted by the C-030 authentication pipeline;
- produces an authenticated HASE principal;
- is projected into `HttpContext.User`;
- reaches the gRPC service over HTTP/2;
- retains the expected principal name `client-01`.

### Missing Client Certificate

A client that presents no certificate:

- is rejected at the TLS boundary;
- receives no application authentication result;
- does not execute the gRPC service;
- is observed by the gRPC client as `StatusCode.Unavailable`.

### Unenrolled Client Certificate

A structurally valid CA-issued certificate that is not the enrolled credential:

- completes TLS negotiation;
- reaches the HASE authentication boundary;
- is rejected as unauthenticated;
- does not execute the gRPC service;
- is observed by the gRPC client as `StatusCode.Unauthenticated`.

## Test Certificate Model

The integration fixture creates an isolated test certification authority and
CA-issued server and client certificates. Certificates used by the network
stack are exported to PKCS#12 and reloaded before hosting or client use.

This models an issuer chain accepted by Windows Schannel and avoids depending
on machine certificate-store mutations. The fixture does not represent a
production credential provisioning or rotation design.

## Preserved Semantics

- The runtime host remains the sole owner of physical endpoint connections,
  sessions, synchronization, recovery, attachment, detachment, and disposal.
- Authentication executes before the gRPC service.
- Authorization remains a separate policy decision after authentication.
- Network reachability does not grant HASE authority.
- Successful TLS negotiation does not grant HASE authority.
- Missing or rejected credentials never execute the operational service.
- The gRPC adapter remains above the transport-independent application services.
- Remote lifecycle administration is not introduced.
- Production non-loopback exposure remains prohibited.

## Result

C-031 completes the automated mutual-TLS runtime-host integration baseline:

- encrypted HTTP/2 gRPC hosting;
- mandatory client-certificate presentation;
- enrolled certificate authentication;
- authenticated principal projection;
- rejection at the correct TLS or application boundary;
- preservation of the existing runtime-host and gRPC architecture.

Production deployment, credential provisioning and rotation, revocation
operations, audit, Tailscale reachability, and non-loopback exposure remain
separately approved backlog.
