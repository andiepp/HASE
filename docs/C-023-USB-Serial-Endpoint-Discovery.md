# C-023 — USB Serial Endpoint Discovery and Verification

## Status

Completed, automated, and physically verified on Windows.

Validation baseline:

```text
.NET solution builds
1,600 automated tests pass
Arduino Uno firmware builds
Physical C-023 scenario exits with code 0
```

## Goal

Discover USB serial connection candidates automatically and verify compatible
compact endpoints through authoritative Compact Serial Protocol bootstrap.

USB metadata identifies connection candidates only. It does not establish HASE
endpoint identity.

`CompactBootstrapResponse.EndpointId` remains authoritative.

## Architecture

C-023 is governed by ADR-0021.

Discovery is separated into:

1. operating-system candidate enumeration;
2. optional metadata filtering;
3. temporary serial connection ownership;
4. Compact Serial Protocol bootstrap;
5. authoritative endpoint identity extraction;
6. exact descriptor-reference resolution;
7. candidate-outcome retention;
8. authoritative endpoint-inventory deduplication.

Discovery never attaches, publishes, replaces, or mutates runtime endpoints.

Manual COM-port configuration remains supported through the existing compact
serial connection path.

## Windows candidate enumeration

The first implementation targets Windows.

Candidate records are obtained from `Win32_PnPEntity` through
`System.Management`.

The Windows provider reports platform-neutral
`UsbSerialEndpointCandidate` values containing:

- port name;
- optional USB vendor identifier;
- optional USB product identifier;
- optional product name;
- optional manufacturer name;
- optional USB serial number.

Malformed operating-system records are isolated. Missing optional USB metadata
does not invalidate a candidate.

Windows port names are normalized before candidate delivery. Candidate
deduplication uses the normalized port identity.

Enumeration is read-only. It opens no serial ports and sends no protocol
traffic.

## Candidate filtering

`UsbSerialEndpointMetadataFilter` may filter by:

- port name;
- VID;
- PID;
- product name;
- manufacturer name;
- USB serial number.

Every configured criterion must match.

Filtering reduces active probes. A filter match does not:

- assign endpoint identity;
- select an endpoint descriptor;
- prove HASE compatibility;
- attach an endpoint;
- bypass Compact Serial Protocol bootstrap.

The physical C-023 scenario filters for:

```text
VID : 0x2341
PID : 0x0043
```

These values select the Arduino Uno connection candidate only. They are not
HASE identity.

## Verification

Each eligible candidate is verified sequentially.

The verification path:

```text
UsbSerialEndpointCandidate
    ->
candidate-specific SerialTransportOptions
    ->
temporary System.IO.Ports byte stream
    ->
Compact Serial Protocol connection
    ->
CompactBootstrapRequest
    ->
CompactBootstrapResponse
    ->
authoritative EndpointId
    ->
exact DescriptorReference lookup
    ->
verification result
    ->
temporary connection disposal
```

Physical serial settings:

```text
Baud rate : 115200
Data bits : 8
Parity    : None
Stop bits : One
Handshake : None
Timeout   : 3 seconds per candidate
```

Compact Serial Protocol V1 and its protocol version remain unchanged.

## Verification outcomes

Candidate outcomes distinguish:

- port busy;
- port unavailable;
- access denied;
- connection failed;
- verification timeout;
- non-HASE endpoint;
- invalid compact response;
- unsupported compact protocol version;
- invalid endpoint identity;
- unknown descriptor reference;
- incompatible descriptor or compact profile.

Semantic compact failures are preserved through dedicated exceptions and mapped
at the verification boundary.

A clean end-of-stream before a complete compact frame is classified as a
non-HASE endpoint.

A malformed or unexpected compact response is classified as an invalid compact
response.

Unclassified verification I/O failures propagate from the verifier and are
isolated by discovery as connection failures.

Caller cancellation remains distinct, propagates immediately, and stops further
enumeration and verification.

## Discovery result

`UsbSerialEndpointDiscoveryResult` exposes two views.

### CandidateResults

Retains every eligible distinct-port candidate outcome in source order.

This preserves diagnostics for successful and rejected candidates.

### VerifiedEndpoints

Contains a unique authoritative endpoint inventory.

When distinct connection candidates report equal authoritative `EndpointId`
values, every candidate outcome remains in `CandidateResults`, while the first
verified result is retained once in `VerifiedEndpoints`.

Discovery does not use this rule to replace or attach runtime endpoints.

## Production composition

`WindowsUsbSerialEndpointDiscovery.Create` composes:

- `WindowsUsbSerialEndpointCandidateSource`;
- `SystemIoPortsSerialByteStreamFactory`;
- `CompactSerialEndpointConnector`;
- `CompactUsbSerialEndpointVerificationOperation`;
- `CompactUsbSerialEndpointCandidateVerifier`;
- `UsbSerialEndpointDiscoveryService`.

Creating the service performs no enumeration, opens no ports, sends no protocol
traffic, and mutates no runtime state.

Discovery begins only when explicitly requested.

## Protocol Explorer

The physical scenario is:

```text
c023 [baud rate] [verification timeout seconds]
```

Defaults:

```text
Baud rate            : 115200
Verification timeout : 3 seconds
Candidate filter     : VID 0x2341, PID 0x0043
```

Example:

```powershell
dotnet run --project src/HASE.ProtocolExplorer/HASE.ProtocolExplorer.csproj -- c023
```

## Physical verification

Physical verification was completed on Windows with an Arduino Uno connected
as COM10.

Observed candidate metadata:

```text
Port              : COM10
VID               : 0x2341
PID               : 0x0043
Product           : Arduino Uno
USB serial number : 75836333537351D06110
```

Observed authoritative compact result:

```text
Outcome                : Verified
Authoritative Endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
```

Observed unique inventory:

```text
Unique verified endpoints : 1
Endpoint ID               : arduino-uno-01
Candidate port            : COM10
Descriptor reference      : arduino-uno-validation v1
```

Lifecycle evidence:

```text
Runtime attachment   : None
Runtime mutation     : None
Verification streams : Disposed
Process exit code    : 0
```

The USB serial number, VID, PID, product name, and COM port described the
connection candidate only.

The endpoint identity `arduino-uno-01` came exclusively from
`CompactBootstrapResponse.EndpointId`.

## Approved boundaries confirmed

- USB serial metadata identifies candidates only.
- Compact bootstrap identity is authoritative.
- Discovery does not attach or replace runtime endpoints automatically.
- Manual COM-port configuration remains supported.
- Busy, unavailable, non-HASE, incompatible, and malformed candidates are
  represented as isolated outcomes.
- Windows implementation remains behind platform-neutral contracts.
- Compact Serial Protocol V1 remains unchanged.
- Protocol Version 1 remains unchanged.

## Explicit backlog

- Linux USB serial discovery and physical validation.
- A formal compact-profile compatibility contract before actively producing
  `IncompatibleDescriptor`.
- Optional bounded-parallel verification if future physical evidence justifies
  the additional ownership and coordination complexity.
- Additional compact USB serial devices and metadata filters.
