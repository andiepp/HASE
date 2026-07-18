# ADR-0018 - mDNS/DNS-SD Network Endpoint Discovery

- Status: Accepted
- Date: 2026-07-18

---

# Context

HASE physical endpoints can be reached through framed TCP, but a client previously required a configured host name or IP address and TCP port before it could establish a connection.

This is sufficient for explicitly configured endpoints, but it does not provide local-network discovery when:

- an endpoint receives its IPv4 address through DHCP;
- the address changes after a restart or network reconfiguration;
- several HASE endpoints are present;
- an application needs to present reachable endpoints for explicit user selection.

HASE already provides authoritative endpoint discovery at the protocol layer through:

```text
DiscoverRequest
    ->
DiscoverResponse
```

`DiscoverResponse` contains the authoritative `EndpointId` and instrument identities reported by the physical endpoint.

Network discovery must not create a second identity authority. In particular, the following network values can change independently of the physical endpoint identity:

- DNS-SD service instance name;
- mDNS host name;
- IPv4 or IPv6 address;
- TCP port;
- DNS-SD TXT metadata.

The discovery mechanism must therefore locate reachable candidates and then verify each candidate through the existing HASE protocol.

The implementation must also isolate individual candidate failures. An unreachable, timed-out, malformed, or non-HASE peer must not terminate browsing for other endpoints.

Discovery must not automatically replace or mutate an existing runtime endpoint. Runtime attachment requires a separate explicit application or user decision.

The first implementation target is local-link IPv4 discovery. IPv6, authentication, authorization, encryption, and cross-subnet discovery remain outside the approved scope.

---

# Decision

HASE uses multicast DNS with DNS-Based Service Discovery for local-network endpoint discovery.

The registered DNS-SD service type is:

```text
_hase._tcp.local
```

The current physical ESP32 endpoint advertises:

```text
Instance : doit-esp32-devkitc-v4-01
Port     : 5000
```

The ESP32 advertises the service only while its Wi-Fi connection and TCP endpoint are available.

## Discovery Layers

Network discovery is separated into three layers.

### Candidate browsing

`Hase.Transport` owns transport-level discovery contracts and the mDNS implementation:

```text
NetworkEndpointCandidate
INetworkEndpointBrowser
MdnsNetworkEndpointBrowser
```

`NetworkEndpointCandidate` contains:

- DNS-SD service instance name;
- resolved network address;
- advertised TCP port.

The service instance name is descriptive metadata. It is not authoritative endpoint identity.

The first browser implementation:

- browses `_hase._tcp.local`;
- accepts IPv4 addresses only;
- yields candidates asynchronously;
- stops on caller cancellation;
- isolates malformed advertisements;
- deduplicates candidates by address and port.

The `Tmds.MDns` package is isolated behind an internal adapter so the HASE browser contract and lifecycle can be tested without multicast network access.

### Candidate verification

`Hase.Runtime.Transport` owns HASE-specific verification:

```text
INetworkEndpointCandidateVerifier
TcpProtocolNetworkEndpointCandidateVerifier
NetworkEndpointVerificationResult
VerifiedNetworkEndpoint
RejectedNetworkEndpointCandidate
```

Every candidate is verified by:

1. opening a framed TCP connection to the advertised address and port;
2. sending the existing Protocol Version 1 `DiscoverRequest`;
3. requiring a correlated `DiscoverResponse`;
4. accepting `DiscoverResponse.EndpointId` as authoritative identity;
5. disposing the temporary verification connection.

The Protocol Version 1 wire contract is unchanged.

Each candidate verification has an independent timeout.

Verification failures are represented as isolated results:

```text
Unreachable
TimedOut
NonHaseEndpoint
InvalidProtocolResponse
```

Caller cancellation is not converted into a rejected candidate. It propagates as cancellation and stops browsing and active verification.

### Discovery orchestration

`NetworkEndpointDiscoveryService` combines the browser and verifier.

The initial orchestration implementation verifies candidates sequentially. This provides deterministic behavior and avoids choosing a concurrency policy before physical evidence demonstrates a need for parallel verification.

The service:

- preserves rejected candidates for diagnostics;
- deduplicates verified endpoints by authoritative `EndpointId`;
- propagates cancellation;
- does not create a runtime endpoint;
- does not attach a verified endpoint;
- does not replace an existing runtime endpoint;
- does not mutate runtime connection state.

## Deduplication

Discovery uses two intentionally different identity scopes.

Transport candidates are deduplicated by:

```text
address + port
```

Verified HASE endpoints are deduplicated by:

```text
DiscoverResponse.EndpointId
```

This prevents repeated network advertisements from producing duplicate candidate work while still allowing multiple network candidates to be recognized as the same authoritative physical endpoint.

## Protocol Explorer

Protocol Explorer provides the read-only scenario:

```text
network-discovery
```

The scenario displays:

- service instance name;
- candidate address and port;
- verification result;
- authoritative endpoint ID or isolated failure detail.

The scenario runs until Ctrl+C and never attaches a discovered endpoint to the runtime.

## ESP32 Advertisement

The physical endpoint uses `HaseMdnsAdvertiser` to:

- initialize mDNS after Wi-Fi and UTC synchronization;
- use host name `doit-esp32-devkitc-v4-01`;
- use DNS-SD instance name `doit-esp32-devkitc-v4-01`;
- advertise `_hase._tcp` on port 5000;
- stop advertising when Wi-Fi is lost;
- restart advertising after Wi-Fi, UTC, and TCP recovery.

Failure to initialize mDNS does not disable the existing explicitly addressed TCP endpoint. The endpoint remains usable through a known address, but it is not discoverable through mDNS.

---

# Consequences

## Positive consequences

- Applications can discover local HASE TCP endpoints without configured IP addresses.
- DHCP address changes do not change authoritative HASE identity.
- Protocol Version 1 remains transport-independent and unchanged.
- Network metadata cannot impersonate authoritative endpoint identity without also completing the HASE protocol exchange.
- Invalid and unreachable peers are isolated from other discovery results.
- Cancellation has clear browsing and verification semantics.
- The runtime is protected from automatic endpoint replacement.
- Browser lifecycle and candidate verification are independently testable.
- The design can support additional discovery implementations without changing verification contracts.

## Negative consequences

- mDNS is normally limited to the local link.
- Multicast availability depends on host and network configuration.
- Firewalls, access points, VLANs, and multicast filtering can prevent discovery.
- A third-party .NET mDNS package is required.
- Sequential verification can delay later candidates when an earlier candidate times out.
- DNS-SD discovery does not provide authentication, authorization, or encryption.
- IPv6 candidates are currently ignored.

## Neutral consequences

- Explicitly configured TCP endpoints remain supported.
- The mDNS service instance currently matches the authoritative endpoint ID, but this equality is not required and must not be relied upon.
- Runtime endpoint attachment remains a separate future workflow.

---

# Alternatives Considered

## Use mDNS identity as authoritative endpoint identity

Rejected.

DNS-SD instance names and host names are network metadata. They can change, collide, or be advertised by non-HASE peers. Only Protocol Version 1 `DiscoverResponse` is authoritative.

## Add discovery metadata to Protocol Version 1

Rejected.

The current protocol already provides authoritative discovery after a connection is established. Network reachability discovery belongs below the protocol and does not require a wire-contract change.

## Use broadcast UDP discovery

Rejected for the first implementation.

A custom broadcast protocol would duplicate service-discovery behavior, require another wire format, and introduce additional maintenance and interoperability work.

## Use a central registry

Rejected for local discovery.

A central registry introduces infrastructure, availability, configuration, and trust requirements that are unnecessary for local-link endpoint discovery.

## Automatically attach the first verified endpoint

Rejected.

Discovery must not silently replace or mutate runtime state. Selection and attachment require an explicit application policy or user action.

## Verify candidates concurrently without a bound

Rejected.

Unbounded connection attempts would make cancellation, resource use, ordering, and diagnostics harder to control. The initial implementation remains sequential until bounded concurrency is justified.

## Implement IPv4 and IPv6 together

Deferred.

IPv4 is the first physical target. The contracts retain `IPAddress`, allowing IPv6 to be added later without changing the candidate model.

---

# Validation

The implementation is covered by unit and end-to-end tests for:

- candidate validation and equality;
- IPv4 filtering;
- malformed advertisement isolation;
- address-and-port deduplication;
- browser cancellation and cleanup;
- candidate verification result contracts;
- verification timeouts;
- caller cancellation;
- unreachable candidates;
- non-HASE candidates;
- invalid protocol responses;
- real loopback framed TCP verification;
- authoritative endpoint-ID deduplication;
- discovery orchestration.

The physical ESP32 validation produced:

```text
Service  : doit-esp32-devkitc-v4-01
Candidate: 192.168.0.223:5000
Result   : Verified
Endpoint : doit-esp32-devkitc-v4-01
```

The verified automated-test baseline is:

```text
861 automated tests passing
```

---

# Follow-up Work

- keep Project Status and Roadmap synchronized with the accepted decision;
- validate discovery during ESP32 reset;
- validate advertisement recovery after Wi-Fi interruption;
- decide whether bounded parallel verification is necessary;
- design explicit endpoint selection and runtime attachment;
- validate discovery on Linux;
- add IPv6 browsing and verification;
- evaluate security requirements before discovery is used across untrusted networks.

