# HASE Northbound Runtime-Host API Reference

## Document status

| Item | Value |
| --- | --- |
| Project | HASE — Hardware Access System Environment |
| Contract | Northbound Runtime-Host Remote API |
| API version | 1.0 |
| Transport | HTTPS / HTTP/2 gRPC |
| Authentication | Mutual TLS |
| Authoritative baseline | Commit `0ad8bcbd3b1e1539796f578d3e5498274984ab71` |
| Verification baseline | 3,029 automated tests passing |
| Deployment profile | ADR-0032 controlled private-network validation |
| Generated C# namespace | `Hase.Runtime.Remote.Grpc.V1` |
| Protobuf package | `hase.runtime.remote.v1` |

This document describes the complete public version 1 remote contract and the
supported .NET client entry points at the ADR-0032 baseline.

The API is operational. It exposes runtime-host snapshots, Property operations,
Command execution, and live observation. It does not expose endpoint
attachment, detachment, discovery, host configuration, credential management,
or runtime-host shutdown.

---

# 1. Contract sources

The normative protobuf contract is:

```text
src/Hase.Runtime.Remote.Grpc.Contracts/Protos/runtime_host_remote_api_v1.proto
```

The generated C# contract assembly is:

```text
src/Hase.Runtime.Remote.Grpc.Contracts
```

The supported private-network client composition is:

```text
src/Hase.Runtime.Remote.Grpc.Hosting
```

Important public client types:

```csharp
Hase.Runtime.Remote.Grpc.Hosting.RuntimeHostPrivateNetworkClientOptions
Hase.Runtime.Remote.Grpc.Hosting.RuntimeHostPrivateNetworkClientOptionsFile
Hase.Runtime.Remote.Grpc.Hosting.RuntimeHostPrivateNetworkClientDeployment
Hase.Runtime.Remote.Grpc.Hosting.RuntimeHostPrivateNetworkGrpcClient
Hase.Runtime.Remote.Grpc.Hosting.RuntimeHostCertificateStoreReference
```

The generated service client is:

```csharp
Hase.Runtime.Remote.Grpc.V1.RuntimeHostRemoteApi
    .RuntimeHostRemoteApiClient
```

---

# 2. Security and connection model

## 2.1 Required transport

The ADR-0032 client uses:

- HTTPS only;
- HTTP/2 gRPC;
- TLS 1.2 or TLS 1.3;
- one client certificate with an accessible private key;
- exact pinning of the provisioned server certificate;
- an explicit IP-address URI;
- a fixed server port.

The client does not fall back to cleartext, anonymous access, wildcard
certificate acceptance, or ordinary platform-only server validation.

## 2.2 External client configuration

The generated laptop configuration has format version 1:

```json
{
  "formatVersion": 1,
  "address": "https://<private-address>:<fixed-port>",
  "clientCertificate": {
    "storeName": "My",
    "storeLocation": "CurrentUser",
    "thumbprint": "<client-certificate-thumbprint>"
  },
  "trustedServerCertificate": {
    "storeName": "TrustedPeople",
    "storeLocation": "CurrentUser",
    "thumbprint": "<server-certificate-thumbprint>"
  }
}
```

The real address and thumbprints are deployment data. They must remain outside
the repository, documentation examples, fixtures, packaged archives, logs, and
screenshots.

Configuration rules:

- the file path passed to `LoadAsync` must be fully qualified;
- the document is limited to 64 KiB;
- property names are case-sensitive;
- unknown JSON members are rejected;
- `formatVersion` must be `1`;
- `address` must be an absolute HTTPS URI;
- the URI host must be an explicit IP address;
- user information, path, query, and fragment are prohibited;
- both certificate references are required;
- store names and locations must be exact .NET enum names;
- each thumbprint must normalize to exactly 40 hexadecimal characters.

## 2.3 Authentication and authorization

The server authenticates the presented certificate and resolves it to a stable
HASE client principal. Authentication and authorization occur before the
application service is invoked.

Each operation requires a distinct semantic permission:

| RPC | Permission |
| --- | --- |
| `GetSnapshot` | `runtime-host.snapshot.read` |
| `ReadCachedProperty` | `property.cached.read` |
| `ReadAuthoritativeProperty` | `property.authoritative.read` |
| `WriteProperty` | `property.write` |
| `ExecuteCommand` | `command.execute` |
| `Observe` | `observation.subscribe` |

An authenticated but unauthorized call fails with gRPC status
`PermissionDenied`.

Network reachability does not grant HASE authority.

---

# 3. Service overview

```proto
service RuntimeHostRemoteApi {
  rpc GetSnapshot (GetSnapshotRequest) returns (GetSnapshotResponse);
  rpc ReadCachedProperty (ReadCachedPropertyRequest)
      returns (CachedPropertyResult);
  rpc ReadAuthoritativeProperty (ReadAuthoritativePropertyRequest)
      returns (PropertyOperationResult);
  rpc WriteProperty (WritePropertyRequest)
      returns (PropertyOperationResult);
  rpc ExecuteCommand (ExecuteCommandRequest)
      returns (CommandOperationResult);
  rpc Observe (ObserveRequest) returns (stream ObserveResponse);
}
```

| RPC | Shape | Purpose |
| --- | --- | --- |
| `GetSnapshot` | Unary | Capture the currently published runtime-host inventory and descriptors. |
| `ReadCachedProperty` | Unary | Read the runtime-host cache without endpoint I/O. |
| `ReadAuthoritativeProperty` | Unary | Read directly from the current physical endpoint attachment. |
| `WriteProperty` | Unary | Write a Property and await endpoint confirmation. |
| `ExecuteCommand` | Unary | Execute a Command exactly once. |
| `Observe` | Server streaming | Receive an initial snapshot and subsequent live observations. |

Clients should set explicit deadlines on unary calls and use a cancellation
token for the observation stream.

Automatic retry must not be applied to `WriteProperty` or `ExecuteCommand`.
Their effects may already have occurred when a transport failure is observed.

---

# 4. Identity and targeting

## 4.1 Runtime-host identity

`GetSnapshotResponse.runtime_host_id` identifies the runtime host. It is stable
across normal process restarts when the host identity store is preserved.

## 4.2 Endpoint identity

`endpoint_id` is the authoritative endpoint identity.

## 4.3 Attachment generation

`attachment_generation` identifies one particular published attachment of an
endpoint. It is serialized as a GUID string.

All active Property and Command targets contain both:

- `endpoint_id`;
- `attachment_generation`.

The combination prevents a stale UI selection from silently operating on a
replacement attachment with the same endpoint identity.

When an operation returns `AttachmentNotCurrent`, the client must refresh its
snapshot or continue from a newer observation. It must not substitute another
generation automatically.

## 4.4 Instrument and member identity

Property targets use:

```text
endpoint_id
attachment_generation
instrument_id
property_id
```

Command targets use:

```text
endpoint_id
attachment_generation
instrument_id
command_path_segments
```

Events are identified by:

```text
endpoint_id
attachment_generation
instrument_id
event_path_segments
```

Clients should obtain all identifiers from the snapshot descriptors. Display
names are presentation text and must not be used as operational identity.

---

# 5. Common value model

## 5.1 `RemoteValue`

Version 1 supports a closed union:

| Protobuf member | Generated C# `KindCase` | CLR value |
| --- | --- | --- |
| `boolean_value` | `BooleanValue` | `bool` |
| `string_value` | `StringValue` | `string` |
| `numeric_value` | `NumericValue` | `double` |
| no selected member | `None` | No value / null argument |

All integral, floating-point, and decimal runtime values are normalized to
`double` on the remote contract.

Clients must inspect `RemoteValue.KindCase` before reading a union member.

## 5.2 `PropertyValue`

| Field | Meaning |
| --- | --- |
| `value` | Normalized Boolean, string, or numeric value. |
| `timestamp_utc` | UTC timestamp associated with the value. |
| `quality` | `Good`, `Uncertain`, or `Bad`. |

`PROPERTY_QUALITY_UNSPECIFIED` is not a successful domain quality and should be
displayed as unknown.

---

# 6. Snapshot API

## 6.1 Request

`GetSnapshotRequest` has no fields.

## 6.2 Response

`GetSnapshotResponse` contains:

| Field | Type | Meaning |
| --- | --- | --- |
| `runtime_host_id` | string | Stable runtime-host identity. |
| `api_version` | `RuntimeHostApiVersion` | Contract version reported by the host. |
| `endpoints` | repeated endpoint snapshot | Currently published attachments. |

The current version is:

```text
major = 1
minor = 0
```

A version 1 client should reject an unsupported major version. A higher minor
version within major version 1 may add backward-compatible data; clients must
continue to follow protobuf unknown-field behavior.

## 6.3 Endpoint snapshot

`PublishedRuntimeEndpointSnapshot` contains:

| Field | Meaning |
| --- | --- |
| `endpoint_id` | Authoritative endpoint identity. |
| `attachment_generation` | Current attachment generation GUID string. |
| `descriptor` | Complete endpoint and instrument descriptor tree. |
| `connection_status` | Current runtime connection status. |

In generated C#, the `descriptor` property is named `Descriptor_` because
`Descriptor` conflicts with generated protobuf infrastructure.

---

# 7. Descriptor model

## 7.1 Endpoint descriptor

`EndpointDescriptor` contains:

- authoritative `endpoint_id`;
- optional display name;
- optional description;
- zero or more instrument descriptors.

## 7.2 Instrument descriptor

`InstrumentDescriptor` contains:

- `instrument_id`;
- name;
- kind;
- optional manufacturer;
- optional model;
- optional serial number;
- optional firmware version;
- optional hardware revision;
- optional description;
- Property descriptors;
- Command descriptors;
- Event descriptors.

## 7.3 Property descriptor

`PropertyDescriptor` contains:

| Field | Meaning |
| --- | --- |
| `property_id` | Stable Property identity within the instrument. |
| `path_segments` | Hierarchical descriptor path for presentation/navigation. |
| `display_name` | User-facing name. |
| `description` | Optional user-facing description. |
| `access_mode` | None, Read, Write, or ReadWrite. |
| `data` | Boolean, string, or numeric data descriptor. |

The UI should enable operations from `access_mode`:

| Access mode | Cached/read UI | Write UI |
| --- | --- | --- |
| `None` | Disabled | Disabled |
| `Read` | Enabled | Disabled |
| `Write` | Disabled | Enabled |
| `ReadWrite` | Enabled | Enabled |

## 7.4 Numeric descriptor

`NumericDataDescriptor` contains:

- quantity;
- native unit;
- minimum and maximum range;
- resolution.

The native unit contains its own quantity identity. A client should display the
provided unit symbol and enforce the advertised range before sending a write.
Resolution describes endpoint data resolution; it is not a mandatory UI step
size policy.

## 7.5 Command descriptor

`CommandDescriptor` contains:

- `path_segments`;
- display name;
- optional description.

The complete ordered path is the Command identity used in `CommandTarget`.

## 7.6 Event descriptor

`EventDescriptor` contains:

- `path_segments`;
- display name;
- optional description.

The complete ordered path identifies the Event in observation payloads.

---

# 8. Connection status

`EndpointConnectionStatus` contains:

- connection state;
- UTC time at which the state changed;
- optional detail.

States:

| State | Meaning |
| --- | --- |
| `Disconnected` | No active endpoint connection. |
| `Connecting` | Initial connection is being established. |
| `Synchronizing` | Descriptor and authoritative runtime state are being synchronized. |
| `Ready` | The attachment is ready for supported active operations. |
| `Reconnecting` | Recovery is in progress after a connection loss. |
| `Faulted` | The current connection attempt or session is faulted. |

Cached values may remain available while an endpoint is disconnected. Active
operations can return `EndpointUnavailable`.

---

# 9. Property APIs

## 9.1 `PropertyTarget`

```csharp
var target = new PropertyTarget
{
    EndpointId = endpoint.EndpointId,
    AttachmentGeneration = endpoint.AttachmentGeneration,
    InstrumentId = instrument.InstrumentId,
    PropertyId = property.PropertyId
};
```

The target must be copied from one coherent snapshot or observation state.

## 9.2 Cached read

Request:

```csharp
var request = new ReadCachedPropertyRequest
{
    Target = target
};
```

Response: `CachedPropertyResult`

| Field | Meaning |
| --- | --- |
| `status` | Property operation status. |
| `snapshot` | Published Property snapshot when available. |
| `diagnostic` | Optional bounded diagnostic intended for diagnosis, not program logic. |

The returned `PublishedRuntimePropertySnapshot` contains the target, descriptor,
connection status, and current cached value.

Cached read performs no physical endpoint I/O.

## 9.3 Authoritative read

Request:

```csharp
var request = new ReadAuthoritativePropertyRequest
{
    Target = target
};
```

Response: `PropertyOperationResult`

On success, `confirmed_value` contains the endpoint-confirmed value.

## 9.4 Write

Request:

```csharp
var request = new WritePropertyRequest
{
    Target = target,
    RequestedValue = new RemoteValue
    {
        BooleanValue = true
    }
};
```

Response: `PropertyOperationResult`

On success, `confirmed_value` is authoritative. The client should replace its
optimistic or pending UI state with the confirmed value.

Do not infer success from the absence of a transport exception. Inspect
`PropertyOperationResult.Status`.

## 9.5 Property operation statuses

| Status | Meaning | Recommended client action |
| --- | --- | --- |
| `Success` | Operation completed. | Consume the returned snapshot or confirmed value. |
| `AttachmentNotCurrent` | Target generation is stale. | Refresh/reconcile inventory; do not retry against another generation automatically. |
| `InstrumentNotFound` | Instrument is absent from the targeted attachment. | Refresh descriptors and selection. |
| `PropertyNotFound` | Property is absent. | Refresh descriptors and selection. |
| `ReadNotSupported` | Property is not readable. | Disable read action according to descriptor. |
| `WriteNotSupported` | Property is not writable. | Disable write action according to descriptor. |
| `InvalidValue` | Requested value violates the Property contract. | Keep editor open and show validation failure. |
| `EndpointUnavailable` | Endpoint cannot currently perform the operation. | Show connection state; allow deliberate later retry. |
| `EndpointRejected` | Endpoint rejected the request. | Show failure; do not assume state changed. |
| `EndpointFailure` | Endpoint or adapter failed. | Refresh authoritative state before another mutation. |
| `TimedOut` | Endpoint confirmation was not obtained in time. | Treat outcome as uncertain; refresh authoritative state before retry. |
| `Unspecified` | No valid result status. | Treat as protocol/application failure. |

---

# 10. Command API

## 10.1 `CommandTarget`

```csharp
var target = new CommandTarget
{
    EndpointId = endpoint.EndpointId,
    AttachmentGeneration = endpoint.AttachmentGeneration,
    InstrumentId = instrument.InstrumentId
};
target.CommandPathSegments.AddRange(command.PathSegments);
```

## 10.2 Request

For a Command with no argument:

```csharp
var request = new ExecuteCommandRequest
{
    Target = target
};
```

For a Command with an argument:

```csharp
var request = new ExecuteCommandRequest
{
    Target = target,
    Argument = new RemoteValue
    {
        NumericValue = 1.0
    }
};
```

An absent `Argument` maps to a null argument.

## 10.3 Response

`CommandOperationResult` contains:

- status;
- optional normalized return value;
- optional diagnostic.

Command execution is exactly once at the northbound service boundary. The
client must not automatically retry a timed-out or transport-failed Command.

## 10.4 Command operation statuses

| Status | Meaning | Recommended client action |
| --- | --- | --- |
| `Success` | Command completed. | Consume the optional return value; authoritatively read related state when confirmation matters. |
| `AttachmentNotCurrent` | Target generation is stale. | Refresh inventory; do not retarget automatically. |
| `InstrumentNotFound` | Instrument is absent. | Refresh descriptors. |
| `CommandNotFound` | Command path is absent. | Refresh descriptors. |
| `ArgumentNotSupported` | Argument is invalid or unsupported. | Correct the input; do not retry unchanged. |
| `EndpointUnavailable` | Endpoint is not available. | Show connection state; allow deliberate later retry. |
| `EndpointRejected` | Endpoint rejected execution. | Show failure; do not infer state. |
| `EndpointFailure` | Endpoint or adapter failed. | Refresh relevant authoritative state. |
| `TimedOut` | Completion was not confirmed in time. | Treat outcome as uncertain; read authoritative state before any retry. |
| `Unspecified` | No valid result status. | Treat as protocol/application failure. |

---

# 11. Observation API

## 11.1 Stream establishment

`ObserveRequest` has no fields.

The server returns `ObserveResponse` messages. The first message is always an
`initial_snapshot`. Every later message is an `observation`.

```text
InitialSnapshot
    snapshot
    snapshot_sequence

Observation
    sequence
    endpoint_id
    attachment_generation
    kind
    payload
```

The initial snapshot and its sequence define the coherent starting point for
the subscription. A client should use it instead of issuing a separate
`GetSnapshot` for stream initialization.

## 11.2 Sequence semantics

- sequences are local to one subscription;
- sequences are strictly increasing;
- sequences are not persistent Event identifiers;
- there is no replay;
- a new subscription starts with a new initial snapshot;
- an observation with `sequence <= lastSequence` is invalid for that client
  subscription.

If the server detects a stream gap, it terminates the RPC with gRPC status
`DataLoss` and the client must open a new subscription.

## 11.3 Observation kinds

### Attachment published

Payload: `AttachmentPublishedObservation`

Contains the complete newly published endpoint snapshot. Add or replace only
the exact `(endpoint_id, attachment_generation)` attachment represented by the
observation.

### Attachment ended

Payload: `AttachmentEndedObservation`

Contains `ended_at_utc`. Remove or mark ended only the matching endpoint and
generation. A later attachment with the same endpoint ID is a different
attachment.

### Connection status changed

Payload: `ConnectionStatusChangedObservation`

Contains previous and current connection statuses for the matching attachment.

### Property value changed

Payload: `PropertyValueChangedObservation`

Contains:

- instrument ID;
- Property ID;
- previous Property value;
- current Property value.

Update only the matching endpoint, generation, instrument, and Property.

### Event occurred

Payload: `EventOccurredObservation`

Contains:

- instrument ID;
- ordered Event path segments;
- UTC occurrence time;
- optional normalized value.

Events are current-connection live notifications. Version 1 provides no Event
history or replay.

## 11.4 C# streaming pattern

```csharp
using AsyncServerStreamingCall<ObserveResponse> call =
    client.Observe(
        new ObserveRequest(),
        cancellationToken: cancellationToken);

ulong lastSequence = 0;
bool receivedInitialSnapshot = false;

while (await call.ResponseStream.MoveNext(cancellationToken))
{
    ObserveResponse response = call.ResponseStream.Current;

    switch (response.ContentCase)
    {
        case ObserveResponse.ContentOneofCase.InitialSnapshot
            when !receivedInitialSnapshot:
            receivedInitialSnapshot = true;
            lastSequence = response.InitialSnapshot.SnapshotSequence;
            ApplySnapshot(response.InitialSnapshot.Snapshot);
            break;

        case ObserveResponse.ContentOneofCase.Observation
            when receivedInitialSnapshot:
            RuntimeHostObservation observation = response.Observation;

            if (observation.Sequence <= lastSequence)
            {
                throw new InvalidDataException(
                    "Observation sequence is not strictly increasing.");
            }

            lastSequence = observation.Sequence;
            ApplyObservation(observation);
            break;

        default:
            throw new InvalidDataException(
                "Invalid observation stream ordering.");
    }
}
```

The stream should be cancelled explicitly when the UI disconnects or closes.

---

# 12. gRPC and application failures

The API has two distinct failure channels.

## 12.1 gRPC failures

Examples:

- TLS or client-certificate failure;
- `PermissionDenied` for authorization rejection;
- `DeadlineExceeded`;
- `Cancelled`;
- `Unavailable`;
- `DataLoss` for an observation gap;
- unexpected server failure.

Handle these through `RpcException.StatusCode`.

## 12.2 Application operation results

Property and Command domain outcomes are returned in their result-status enums.
They are not normally represented as gRPC failures.

The UI should therefore:

1. catch `RpcException`;
2. separately inspect the returned Property or Command status;
3. never parse diagnostic strings to determine behavior.

Diagnostics are optional explanatory text. Status enums are the stable
machine-readable contract.

---

# 13. Supported .NET client lifecycle

## 13.1 Load configuration

```csharp
RuntimeHostPrivateNetworkClientOptions options =
    await RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync(
        fullyQualifiedConfigurationPath,
        cancellationToken);
```

## 13.2 Create deployment

```csharp
using RuntimeHostPrivateNetworkClientDeployment deployment =
    RuntimeHostPrivateNetworkClientDeployment.Create(options);
```

Creation:

- loads the client certificate from the configured operating-system store;
- requires its private key;
- loads the pinned server certificate;
- creates the mutual-TLS channel;
- creates the generated version 1 client.

## 13.3 Access generated client

```csharp
RuntimeHostRemoteApi.RuntimeHostRemoteApiClient client =
    deployment.Client.Client;
```

## 13.4 Dispose

Dispose the deployment once. It owns:

- the generated client channel;
- the HTTP handler;
- the loaded client certificate;
- the loaded trusted-server certificate.

A long-running desktop UI should normally create one deployment per connected
session and reuse its channel for unary calls and the observation stream.

---

# 14. Client state-management rules

A correct client should preserve these invariants:

1. Treat the observation initial snapshot as the subscription baseline.
2. Key attachments by endpoint ID plus attachment generation.
3. Build Property and Command targets only from the active descriptor model.
4. Never retarget a stale operation to a new generation automatically.
5. Inspect union cases before reading values or payloads.
6. Inspect operation status before consuming returned values.
7. Use authoritative reads to resolve uncertain mutation outcomes.
8. Never automatically retry Command execution or Property writes.
9. Enforce strictly increasing subscription-local sequences.
10. Reopen observation after `DataLoss`; do not attempt replay.
11. Cancel stream and outstanding calls during disconnect or application exit.
12. Keep all physical endpoint lifecycle ownership on the runtime host.

---

# 15. Current scope and exclusions

The following are intentionally outside remote API version 1:

- endpoint discovery;
- attachment and detachment;
- automatic endpoint replacement;
- runtime-host lifecycle administration;
- host shutdown;
- certificate provisioning through the operational API;
- credential rotation or revocation operations;
- persistent Event history;
- observation replay;
- remote audit retrieval;
- Tailscale node discovery;
- unrestricted production or Internet deployment.

ADR-0032 authorizes controlled private-network validation only.

---

# 16. Physical validation coverage

The version 1 API has been validated from a Windows 11 laptop against a separate
Windows 11 desktop runtime host owning:

- a native Protocol Version 1 ESP32/BME280 endpoint;
- a Compact Serial Protocol V1 Arduino Uno endpoint.

Validated client behavior includes:

- initial two-endpoint observation snapshot;
- authoritative ESP32 numeric Property read;
- authoritative Arduino Boolean Property read;
- Arduino Command execution;
- authoritative Command confirmation;
- restoration of original Arduino state;
- Property-change observation;
- physical Event observation;
- explicit stream cancellation;
- orderly client, runtime-host, and endpoint shutdown.

---

# 17. Descriptor-driven Property values

Remote API version 1 supports Boolean, Numeric, String, and ByteArray Property
values for authoritative reads and writes. ByteArray mapping is exact in both
directions and preserves leading zero and `FF` bytes.

The WPF client validates input from the authoritative descriptor before
creating an RPC. Invalid input therefore remains local. Client validation does
not replace runtime or endpoint validation. Writes remain explicit,
generation-qualified, and never automatically retried.

Confirmed reads remain available during subsequent observation reprojection
for the current attachment. They are removed when that attachment generation
is no longer published.
