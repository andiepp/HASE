# C-024 — Compact Serial Endpoint Attachment

## Status

Completed, automated, and physically verified on Windows.

Validation baseline:

```text
.NET solution builds
1,677 automated tests pass
Arduino Uno firmware builds
Physical C-024 scenario exits with code 0
```

## Goal

Explicitly attach a configured or selected-discovery compact serial endpoint to
the HASE runtime host, synchronize its readable properties, publish it through
the authoritative attachment inventory, supervise its operational connection,
and detach it orderly.

Discovery never attaches an endpoint automatically. The application or user
must explicitly select a verified endpoint and request attachment.

## Architecture

C-024 implements the compact serial path of the local endpoint communication
lifecycle defined by ADR-0019 and the resource-constrained endpoint model
defined by ADR-0020.

```text
manual configuration or selected discovery result
    -> serial connection definition
    -> temporary authoritative compact bootstrap
    -> exact compact endpoint-definition resolution
    -> staged runtime endpoint construction
    -> independent operational compact connection
    -> authoritative identity and descriptor revalidation
    -> readable-property synchronization
    -> Ready
    -> runtime publication
    -> authoritative attachment inventory
    -> health supervision and recovery
    -> explicit detachment
    -> orderly shutdown
```

Compact Serial Protocol V1 and Protocol Version 1 remain unchanged.

## Explicit attachment

`CompactSerialEndpointAttachmentService` implements the common
`IEndpointAttachmentService` contract. It accepts only:

- `SerialEndpointConnectionDefinition`;
- `HostRepositoryDescriptorSource`.

Unsupported connection definitions or descriptor sources are rejected before
any serial connection is opened. Attachment is always explicit through
`IRuntimeEndpointAttachmentInventory.AttachAsync`.

## Connection-definition origins

Compact serial attachment accepts configured and discovered definitions.

### Configured

A manually configured definition supplies the serial target and communication
settings plus an optional expected authoritative endpoint identity. The
identity returned by compact bootstrap remains authoritative.

### Discovered

A discovered definition is created explicitly from one
`VerifiedUsbSerialEndpoint` and the `UsbSerialEndpointDiscoveryOptions` used
during verification. It preserves the verified serial settings and candidate
port. Its expected identity is the authoritative `EndpointId` returned by the
earlier bootstrap. USB metadata is not used as HASE identity.

Configured and discovered definitions enter the same attachment service path.

## Compact endpoint definition

`CompactEndpointDefinition` binds one exact versioned `DescriptorReference` to
the complete `EndpointDescriptorDefinition` and validated compact property
mappings required for operation.

```text
compact property ID
    -> InstrumentId
    -> PropertyId
    -> CompactPropertyValueEncoding
```

The Arduino Uno registration contains:

```text
Descriptor reference : arduino-uno-validation v1
Instrument           : arduino-uno-controller-01
Runtime property     : built-in-led-state
Property path        : Led.State
Compact property ID  : 0x01
Value encoding       : Boolean
Access               : ReadWrite
```

`CompactPropertyMap` remains the internal validated operational lookup.

## Compact endpoint-definition repository

`ICompactEndpointDefinitionRepository` resolves exact versioned compact
endpoint definitions. `InMemoryCompactEndpointDefinitionRepository` snapshots
registrations, rejects null or duplicate entries, uses exact descriptor ID and
version matching, and propagates cancellation.

The repository is independent of physical endpoint identity. Bootstrap supplies
the authoritative `EndpointId` used to materialize the registered descriptor.

## Descriptor projection

`CompactEndpointDescriptorRepositoryAdapter` projects the descriptor portion
of the compact repository through `IEndpointDescriptorRepository`:

```text
ICompactEndpointDefinitionRepository
    -> CompactEndpointDefinition
    -> EndpointDescriptorDefinition for bootstrap
    -> compact property mappings for operation
```

Bootstrap and operation therefore use one host registration.

## Temporary authoritative bootstrap

Before runtime construction, C-024 opens a temporary compact connection:

```text
SerialEndpointConnectionDefinition
    -> temporary serial byte stream
    -> CompactBootstrapRequest
    -> CompactBootstrapResponse
    -> optional expected-identity validation
    -> exact DescriptorReference resolution
    -> materialized EndpointDescriptor
    -> temporary connection disposal
```

`CompactEndpointAttachmentBootstrapResult` preserves the authoritative
`EndpointId`, exact `DescriptorReference`, resolved definition, and materialized
descriptor. The temporary connection is disposed before operational attachment
and is never reused operationally.

## Operational-definition validation

`CompactEndpointOperationalDefinitionResolver` resolves the exact definition
again and verifies that the repository still returns the requested reference
and a descriptor strictly compatible with the bootstrap descriptor. This
protects attachment if a custom repository changes between bootstrap and
operational construction.

## Staged runtime construction

The runtime endpoint is created from the authoritative bootstrap descriptor but
is not published immediately. The operational resource graph contains:

- `CompactRuntimeEndpointConnectionCoordinator`;
- `CompactRuntimeEndpointConnectionSupervisor`;
- `EndpointConnectionSupervisionLifetime`;
- the validated `CompactPropertyMap`;
- operational compact serial connection ownership.

Resource construction is passive and opens no serial port.

## Independent operational connection

Starting supervision opens a new operational connection and performs compact
bootstrap again. It validates authoritative identity and strict descriptor
compatibility. The operational connection remains owned by the attachment
session until replacement, recovery, or orderly shutdown.

Discovery verification, attachment bootstrap, and operational connection are
three distinct ownership scopes.

## Initial property synchronization

After operational validation, every mapped readable property is synchronized.
For the Arduino Uno, C-024 reads compact property `0x01`, decodes its Boolean
value, and updates `Led.State` with a UTC timestamp and `Good` quality.

An unsuccessful initial synchronization prevents `Ready` and publication.

## Readiness-gated publication

The shared successful attachment path:

1. creates the staged runtime endpoint;
2. creates operational resources;
3. starts supervision;
4. waits for `Ready`;
5. publishes into the runtime context;
6. returns the owning attachment session.

If supervision completes or fails before `Ready`, nothing is published.

## Shared attachment lifecycle

Native framed-TCP and compact serial attachment share
`IEndpointOperationalResources`. The common lifecycle owns readiness waiting,
publication, session construction, failed-attachment cleanup, cleanup-failure
aggregation, and ordered shutdown.

## Runtime-host inventory

`RuntimeEndpointAttachmentHost.CreateCompactSerial` composes a runtime context,
production System.IO.Ports transport, compact attachment service, attachment
inventory, reconnect policy, and health-probe options.

Inventory identity comes from the attached runtime endpoint's authoritative
`EndpointId`, never from COM port or USB metadata. Duplicate authoritative
identity is rejected without automatic replacement.

## Supervision and recovery

The existing compact supervisor owns recurring probing, fault detection,
connection detachment, bounded reconnect, identity and descriptor revalidation,
property resynchronization, cache preservation, and cancellation-aware
shutdown.

```text
Retry schedule : immediate, 1 s, 2 s, 5 s, 10 s maximum
Probe interval : 1 second
Probe timeout  : 3 seconds
```

## Failure behavior

- Bootstrap failure prevents resolution, resource creation, and publication.
- Operational-definition failure prevents operational construction.
- Operational connection or synchronization failure prevents `Ready` and
  publication and cleans up created resources.
- Caller cancellation remains distinct and triggers cleanup.
- Independent cleanup failures are aggregated without hiding the attachment
  failure.

## Orderly shutdown

The session owns runtime publication, supervision lifetime, and compact
coordinator in shutdown order. Explicit inventory detachment removes
publication, cancels and awaits supervision, disposes the operational
connection, transitions the endpoint to `Disconnected`, and removes the
inventory entry. Repeated disposal is safe.

## Automated validation

Coverage includes descriptor-source selection, bootstrap preservation and
disposal, compact definition validation, exact repository lookup, duplicate
rejection, descriptor projection, operational revalidation, passive resource
composition, shared ownership, readiness-gated publication, service validation,
failure cleanup, production host composition, configured and discovered common
paths, independent connection ownership, inventory integration, orderly
detachment, and initial Boolean cache synchronization.

```text
1,677 automated tests pass
```

## Protocol Explorer

```text
c024 [baud rate] [verification timeout seconds]
```

Defaults:

```text
Baud rate            : 115200
Verification timeout : 3 seconds
Candidate filter     : VID 0x2341, PID 0x0043
Probe interval       : 1 second
Probe timeout        : 3 seconds
```

Example:

```powershell
dotnet run --project src/HASE.ProtocolExplorer -- c024
```

Press Ctrl+C once after successful attachment to detach and stop.

## Physical verification

Physical validation completed on Windows with the Arduino Uno discovered as
COM3.

```text
Candidate port         : COM3
VID                    : 0x2341
PID                    : 0x0043
Product                : Arduino Uno
Authoritative endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
```

Attachment result:

```text
Connection origin      : Discovered
Operational port       : COM3
Connection state       : Ready
Inventory entries      : 1
Authoritative lookup   : Same entry
Published endpoints    : 1
Led.State value        : False
Timestamp              : 2026-07-22T19:05:51.3378289+00:00
Quality                : Good
```

Connection ownership:

```text
Discovery verification connection : Disposed
Attachment bootstrap connection    : Disposed
Operational connection             : Owned by attachment
```

Ctrl+C detachment:

```text
Detached               : True
Connection state       : Disconnected
Inventory entries      : 0
Published endpoints    : 0
Operational connection : Disposed
Exit code               : 0
```

## Approved boundaries confirmed

- Discovery never attaches automatically.
- Explicit selection is required.
- Configured and discovered definitions share one attachment path.
- USB metadata identifies candidates only.
- Compact bootstrap identity remains authoritative.
- Attachment bootstraps again on temporary and operational connections.
- Complete descriptor and mappings come from one host registration.
- Publication occurs only after `Ready` and initial synchronization.
- Inventory identity comes from the attached runtime endpoint.
- Duplicate identity never causes automatic replacement.
- The runtime host owns supervision, recovery, detachment, and shutdown.
- Compact Serial Protocol V1 and Protocol Version 1 remain unchanged.

## Explicit backlog

- Linux USB serial discovery and physical validation.
- Formal compact-profile compatibility rules before activating incompatible
  profile classification.
- Additional compact property-value encodings.
- Compact command and event mappings in the operational definition.
- BLE endpoint attachment.
- IPv6 network discovery.
- Northbound runtime-host API.
- Tailscale-assisted runtime-host discovery.
