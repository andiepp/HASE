# HASE Python Client API Reference

Package `hase-client` 0.6.0 · import namespace `hase` · CPython 3.12/3.13.

This reference documents every public export of the `hase` package. New
users should start with
[Getting Started with HASE — Python API](../Getting-Started-Python.md).

All models are immutable frozen dataclasses. All failures raise a typed
error carrying a stable sanitized `code` string; credential bytes,
addresses, and raw server diagnostics are never included. The client never
discovers, retries, reconnects, resubscribes, or falls back.

Each operation requires one authorization grant on the connecting
principal:

| Operation                          | Required grant               |
| ---------------------------------- | ---------------------------- |
| `get_snapshot`                     | `runtime-host.snapshot.read` |
| `read_authoritative_property`      | `property.authoritative.read`|
| `read_cached_property`             | `property.cached.read`       |
| `write_property`                   | `property.write`             |
| `execute_command`                  | `command.execute`            |
| `observe`                          | `observation.subscribe`      |
| `observe_diagnostics`              | `diagnostics.subscribe` plus Runtime Host profile enablement |

Missing grants surface as `rpc-permission-denied`.

---

## 1. Profiles

### load_runtime_host_profile(path)

```python
def load_runtime_host_profile(
    path: str | os.PathLike[str],
) -> RuntimeHostProfile: ...
```

Loads and strictly validates one version-1 connection profile from an
absolute path. Validates file custody only — credential bytes are not
read here. The profile document must contain exactly:

```json
{
  "formatVersion": 1,
  "address": "https://192.168.1.20:52210",
  "clientCertificate": {
    "certificateChainPath": "C:\\HasePython\\client-certificate.pem",
    "privateKeyPath": "C:\\HasePython\\client-private-key.pem"
  },
  "trustedServerCertificate": {
    "certificatePath": "C:\\HasePython\\runtime-host-certificate.cer"
  }
}
```

The address must be `https://<ip-address>:<port>` with no path, query,
or userinfo. The three credential paths must be absolute, existing,
regular files, and pairwise distinct. Unknown or duplicate JSON members,
a wrong `formatVersion`, and files above 64 KiB are rejected.

Raises `ProfileValidationError` with codes such as
`profile-path-invalid`, `profile-file-unavailable`,
`profile-json-invalid`, `profile-shape-invalid`,
`profile-format-unsupported`, `profile-address-invalid`,
`credential-path-invalid`, `credential-file-unavailable`, and
`credential-files-not-distinct`.

```python
from hase import load_runtime_host_profile

profile = load_runtime_host_profile(r"C:\HasePython\profile.json")
print(profile.address)
```

### RuntimeHostProfile

```python
@dataclass(frozen=True)
class RuntimeHostProfile:
    format_version: int
    address: str
    client_certificate_chain_path: Path
    client_private_key_path: Path
    trusted_server_certificate_path: Path
```

The validated result of `load_runtime_host_profile`. Pass it to
`open_runtime_host_channel`.

### ProfileValidationError

`ValueError` subclass with a `code: str` attribute.

### load_automation_target_registry(path, *, excluded_roots=())

```python
def load_automation_target_registry(
    path: str | os.PathLike[str],
    *,
    excluded_roots: Iterable[str | os.PathLike[str]] = (),
) -> AutomationTargetRegistry: ...
```

Loads the strict two-target automation registry used by the repository's
example scripts and installed automation. The registry maps exactly the
two target IDs `desktop-runtime-host` and `minipc-runtime-host` to two
distinct profiles (distinct paths, addresses, and all six credential
files). `excluded_roots` optionally names directories (for example the
repository root) that no registry or profile file may live inside. Both
profiles are loaded strictly; credential bytes are not read.

This registry shape is specific to the two-host reference setup. Programs
with their own host simply call `load_runtime_host_profile` directly.

Raises `AutomationTargetRegistryError` (codes such as
`registry-shape-invalid`, `target-id-invalid`,
`target-profiles-not-distinct`, `target-credentials-not-distinct`,
`profile-inside-excluded-root`).

```python
from hase import load_automation_target_registry

registry = load_automation_target_registry(r"C:\HasePython\targets.json")
target = registry.resolve("desktop-runtime-host")
print(target.display_name, target.profile.address)
```

### AutomationTargetRegistry / AutomationTarget

```python
@dataclass(frozen=True)
class AutomationTargetRegistry:
    format_version: int
    targets: tuple[AutomationTarget, ...]

    def resolve(self, target_id: str) -> AutomationTarget: ...

@dataclass(frozen=True)
class AutomationTarget:
    target_id: str
    display_name: str
    profile_path: Path
    profile: RuntimeHostProfile
```

`resolve` returns the single matching target or raises
`AutomationTargetRegistryError("target-id-unknown")`.

---

## 2. Channel

### open_runtime_host_channel(profile, *, readiness_timeout=10.0)

```python
async def open_runtime_host_channel(
    profile: RuntimeHostProfile,
    *,
    readiness_timeout: float = 10.0,
) -> RuntimeHostChannel: ...
```

Opens one mutual-TLS gRPC channel to the profile's address. Credential
files are read once with bounded sizes and change detection; the trusted
server certificate is the only TLS trust anchor (PEM or DER), and TLS
authority is never overridden. The call waits up to `readiness_timeout`
seconds for HTTP/2 readiness and never retries; gRPC-internal retries are
disabled on the channel. No RPC is invoked by opening.

Raises `RuntimeHostChannelError` with codes such as
`channel-profile-invalid`, `credential-file-unavailable`,
`credential-file-size-invalid`, `credential-file-changed`,
`trusted-certificate-invalid`, `channel-credentials-invalid`,
`channel-readiness-timeout`, and `channel-readiness-failed`.
`channel-readiness-timeout` is what you see when the Runtime Host is not
running or not reachable.

```python
from hase import open_runtime_host_channel

channel = await open_runtime_host_channel(profile, readiness_timeout=5.0)
async with channel:
    ...  # use the channel
```

### RuntimeHostChannel

An opened channel with deterministic close semantics.

- `async close()` — closes exactly once; concurrent and repeated calls
  await the same underlying close operation.
- Async context manager — `async with channel:` closes on exit,
  including failure paths.
- `grpc_channel` — the underlying `grpc.aio.Channel`, exposed for the
  package's own client; treat it as opaque.

### RuntimeHostChannelError

`RuntimeError` subclass with a `code: str` attribute.

---

## 3. RuntimeHostClient

```python
class RuntimeHostClient:
    def __init__(self, channel: RuntimeHostChannel) -> None: ...
```

An asyncio API client over one caller-owned channel. The client never
closes your channel; close it yourself (normally via `async with`). All
methods accept a keyword-only `timeout` in seconds (default 10.0) and
raise `RuntimeHostClientError("rpc-timeout-invalid")` for a non-positive
or non-finite value.

RPC failures map to stable codes shared by all operations:

| Code                    | Meaning                                        |
| ----------------------- | ---------------------------------------------- |
| `rpc-unauthenticated`   | the TLS client identity was not accepted       |
| `rpc-permission-denied` | the principal lacks the operation's grant      |
| `rpc-deadline-exceeded` | the bounded timeout elapsed                    |
| `rpc-unavailable`       | the connection was lost or refused             |
| `rpc-cancelled`         | the server observed a cancellation             |
| `rpc-failed`            | any other failure, sanitized                   |

### get_snapshot(*, timeout=10.0)

```python
async def get_snapshot(*, timeout: float = 10.0) -> RuntimeHostSnapshot: ...
```

Invokes `GetSnapshot` exactly once and returns the immutable projection
of the complete current model: host identity, API version, and every
endpoint with its attachment generation, connection status, and full
descriptor graph. Grant: `runtime-host.snapshot.read`.

```python
snapshot = await client.get_snapshot()
for endpoint in snapshot.endpoints:
    print(endpoint.endpoint_id, endpoint.connection_status.state.value)
```

### read_authoritative_property(target, *, timeout=10.0)

```python
async def read_authoritative_property(
    target: PropertyTarget,
    *,
    timeout: float = 10.0,
) -> PropertyOperationResult: ...
```

Reads one Property from the device itself, exactly once, without cached
fallback. Grant: `property.authoritative.read`. The target's attachment
generation must be current; otherwise the result status is
`ATTACHMENT_NOT_CURRENT`.

```python
result = await client.read_authoritative_property(target)
if result.is_success:
    print(result.confirmed_value.value, result.confirmed_value.quality)
```

### read_cached_property(target, *, timeout=10.0)

```python
async def read_cached_property(
    target: PropertyTarget,
    *,
    timeout: float = 10.0,
) -> CachedPropertyResult: ...
```

Reads one entry from the Runtime Host's synchronized cache without
contacting the endpoint and without authoritative fallback. Grant:
`property.cached.read`. A successful result carries the target, the
Property descriptor, the endpoint connection status, and the cached value
(which may be absent).

```python
cached = await client.read_cached_property(target)
if cached.is_success and cached.snapshot.current_value is not None:
    print(cached.snapshot.current_value.value)
```

### write_property(target, requested_value, *, timeout=10.0)

```python
async def write_property(
    target: PropertyTarget,
    requested_value: MutationValue,
    *,
    timeout: float = 10.0,
) -> PropertyOperationResult: ...
```

Writes one Property exactly once. Grant: `property.write`. Accepted value
types are `bool`, `str`, `bytes`, `float`, and `int` when exactly
representable as the wire double (see `normalize_mutation_value`). A
successful result carries the **device-confirmed** value.

Failures raise `RuntimeHostMutationError` with a classification (see
section 7). Explicit server rejections (`ATTACHMENT_NOT_CURRENT`,
`INSTRUMENT_NOT_FOUND`, `PROPERTY_NOT_FOUND`, `WRITE_NOT_SUPPORTED`,
`INVALID_VALUE`, `ENDPOINT_UNAVAILABLE`, `ENDPOINT_REJECTED`) are
`REJECTED`; endpoint failure, timeout, cancellation, and transport loss
after sending are `OUTCOME_UNCERTAIN` and must be reconciled with an
authoritative read before any further mutation.

```python
try:
    result = await client.write_property(target, 0.5)
    print("confirmed:", result.confirmed_value.value)
except RuntimeHostMutationError as failure:
    if failure.outcome_uncertain:
        result = await client.read_authoritative_property(target)
```

### execute_command(target, argument=None, *, timeout=10.0)

```python
async def execute_command(
    target: CommandTarget,
    argument: MutationValue | None = None,
    *,
    timeout: float = 10.0,
) -> CommandOperationResult: ...
```

Executes one Command exactly once, with an optional typed argument when
the Command descriptor declares one. Grant: `command.execute`. The same
mutation semantics as `write_property` apply: explicit rejections raise
`REJECTED`, ambiguous outcomes raise `OUTCOME_UNCERTAIN`, and the client
never retries or replays. A successful result may carry a typed
`return_value`.

```python
try:
    result = await client.execute_command(target)
    print("status:", result.status.value)
except RuntimeHostMutationError as failure:
    print(failure.classification.value, failure.code)
```

### observe()

```python
async def observe() -> AsyncIterator[ObservationMessage]: ...
```

Opens exactly one observation stream. Grant: `observation.subscribe`.
The first item is always an `ObservationInitialSnapshot`; every following
item is a `RuntimeHostObservation` with a strictly contiguous sequence
number. Closing or cancelling the iterator cancels the subscription; the
client never reconnects or resubscribes.

The stream terminates with `RuntimeHostClientError` on a malformed
message (`observation-message-invalid`), a missing or repeated initial
snapshot, a sequence gap (`observation-sequence-gap` or
`observation-gap`), or an RPC failure (the shared `rpc-*` codes,
otherwise `observation-failed`).

```python
async for message in client.observe():
    if isinstance(message, ObservationInitialSnapshot):
        continue
    print(message.sequence, message.kind.value, message.endpoint_id)
```

### observe_diagnostics()

```python
async def observe_diagnostics() -> AsyncIterator[DiagnosticObservation]: ...
```

Opens exactly one authorized live diagnostic stream. Requires the
`diagnostics.subscribe` grant **and** remote diagnostics enabled in the
Runtime Host's application profile — both are off by default. Records
arrive with strictly contiguous stream sequence numbers; a gap
(`diagnostics-sequence-gap`), malformed record
(`diagnostics-message-invalid`), or RPC failure terminates the stream.
The client never resubscribes or synthesizes records.

```python
async for item in client.observe_diagnostics():
    record = item.record
    print(record.timestamp_utc, record.category.value, record.event_name)
```

### RuntimeHostClientError

`RuntimeError` subclass with a `code: str` attribute; raised by every
client operation for non-mutation failures.

---

## 4. Snapshot model

`project_runtime_host_snapshot(response)` converts one generated
`GetSnapshotResponse` into the public model below; `get_snapshot` calls
it for you. Malformed transport data raises `SnapshotProjectionError`
(a `ValueError` with a `code`).

```python
@dataclass(frozen=True)
class RuntimeHostSnapshot:
    runtime_host_id: str
    api_version: RuntimeHostApiVersion   # major, minor
    endpoints: tuple[RuntimeEndpointSnapshot, ...]

@dataclass(frozen=True)
class RuntimeEndpointSnapshot:
    endpoint_id: str
    attachment_generation: str           # one connection epoch
    descriptor: EndpointDescriptor
    connection_status: EndpointConnectionStatus

@dataclass(frozen=True)
class EndpointConnectionStatus:
    state: EndpointConnectionState       # see enum below
    changed_at_utc: datetime | None
    detail: str | None

@dataclass(frozen=True)
class EndpointDescriptor:
    endpoint_id: str
    display_name: str | None
    description: str | None
    instruments: tuple[InstrumentDescriptor, ...]

@dataclass(frozen=True)
class InstrumentDescriptor:
    instrument_id: str
    name: str
    kind: str
    manufacturer: str | None
    model: str | None
    serial_number: str | None
    firmware_version: str | None
    hardware_revision: str | None
    description: str | None
    properties: tuple[PropertyDescriptor, ...]
    commands: tuple[CommandDescriptor, ...]
    events: tuple[EventDescriptor, ...]

@dataclass(frozen=True)
class PropertyDescriptor:
    property_id: str
    path_segments: tuple[str, ...]
    display_name: str
    description: str | None
    access_mode: PropertyAccessMode      # NONE, READ, WRITE, READ_WRITE
    data: DataDescriptor

@dataclass(frozen=True)
class CommandDescriptor:
    path_segments: tuple[str, ...]       # address for CommandTarget
    display_name: str
    description: str | None
    argument: CommandArgumentDescriptor | None

@dataclass(frozen=True)
class CommandArgumentDescriptor:
    display_name: str
    description: str | None
    data: DataDescriptor

@dataclass(frozen=True)
class EventDescriptor:
    path_segments: tuple[str, ...]
    display_name: str
    description: str | None
    payload: EventPayloadDescriptor | None

@dataclass(frozen=True)
class EventPayloadDescriptor:
    display_name: str
    description: str | None
    data: DataDescriptor
```

`DataDescriptor` is a union of four kinds:

```python
DataDescriptor = (
    NumericDataDescriptor      # quantity, native_unit, value_range, resolution
    | BooleanDataDescriptor
    | StringDataDescriptor
    | ByteArrayDataDescriptor
)

@dataclass(frozen=True)
class NumericDataDescriptor:
    quantity: Quantity                   # id, display_name
    native_unit: Unit                    # id, display_name, symbol, quantity
    value_range: ValueRange | None       # minimum, maximum
    resolution: float | None
```

`EndpointConnectionState` values: `DISCONNECTED`, `CONNECTING`,
`SYNCHRONIZING`, `READY`, `RECONNECTING`, `FAULTED`. Operate only on
endpoints whose state is `READY`.

Example — resolve a numeric Property's unit and range:

```python
from hase import NumericDataDescriptor

descriptor = next(
    prop
    for endpoint in snapshot.endpoints
    for instrument in endpoint.descriptor.instruments
    for prop in instrument.properties
    if prop.property_id == "analog-input-voltage"
)
if isinstance(descriptor.data, NumericDataDescriptor):
    data = descriptor.data
    print(data.native_unit.symbol, data.value_range)
```

---

## 5. Property model

### PropertyTarget

```python
@dataclass(frozen=True)
class PropertyTarget:
    endpoint_id: str
    attachment_generation: str
    instrument_id: str
    property_id: str
```

The four identities addressing one Property in one connection epoch. All
four must be non-empty trimmed strings; violations raise
`PropertyProjectionError`. Take the attachment generation from a current
snapshot — the host rejects stale generations with
`ATTACHMENT_NOT_CURRENT`.

### PropertyValue / PropertyQuality / PropertyScalar

```python
@dataclass(frozen=True)
class PropertyValue:
    value: PropertyScalar        # bool | str | float | bytes | None
    timestamp_utc: datetime      # timezone-aware UTC
    quality: PropertyQuality     # GOOD, UNCERTAIN, BAD
```

### PropertyOperationResult / PropertyOperationStatus

```python
@dataclass(frozen=True)
class PropertyOperationResult:
    status: PropertyOperationStatus
    confirmed_value: PropertyValue | None   # exactly on SUCCESS
    diagnostic: str | None                  # never on SUCCESS

    @property
    def is_success(self) -> bool: ...
```

Statuses: `SUCCESS`, `ATTACHMENT_NOT_CURRENT`, `INSTRUMENT_NOT_FOUND`,
`PROPERTY_NOT_FOUND`, `READ_NOT_SUPPORTED`, `WRITE_NOT_SUPPORTED`,
`INVALID_VALUE`, `ENDPOINT_UNAVAILABLE`, `ENDPOINT_REJECTED`,
`ENDPOINT_FAILURE`, `TIMED_OUT`.

Read operations return failed results; mutation operations raise
`RuntimeHostMutationError` instead, so a returned result from
`write_property` is always a success.

### CachedPropertyResult / CachedPropertySnapshot

```python
@dataclass(frozen=True)
class CachedPropertyResult:
    status: PropertyOperationStatus
    snapshot: CachedPropertySnapshot | None  # exactly on SUCCESS
    diagnostic: str | None

    @property
    def is_success(self) -> bool: ...

@dataclass(frozen=True)
class CachedPropertySnapshot:
    target: PropertyTarget
    descriptor: PropertyDescriptor
    connection_status: EndpointConnectionStatus
    current_value: PropertyValue | None      # cache may be empty
```

### Projection functions

`project_property_target`, `project_property_operation_result`, and
`project_cached_property_result` convert generated transport messages
into these models with strict shape checks; the client calls them for
you. They raise `PropertyProjectionError` (a `ValueError` with a
`code`).

---

## 6. Command model

### CommandTarget

```python
@dataclass(frozen=True)
class CommandTarget:
    endpoint_id: str
    attachment_generation: str
    instrument_id: str
    command_path_segments: tuple[str, ...]   # e.g. ("Led", "Toggle")
```

The path segments must match a `CommandDescriptor.path_segments` from the
snapshot exactly, as a non-empty tuple of non-empty strings.

### CommandOperationResult / CommandOperationStatus

```python
@dataclass(frozen=True)
class CommandOperationResult:
    status: CommandOperationStatus
    return_value: CommandScalar      # bool | str | float | bytes | None
    diagnostic: str | None

    @property
    def is_success(self) -> bool: ...
```

Statuses: `SUCCESS`, `ATTACHMENT_NOT_CURRENT`, `INSTRUMENT_NOT_FOUND`,
`COMMAND_NOT_FOUND`, `ARGUMENT_NOT_SUPPORTED`, `ENDPOINT_UNAVAILABLE`,
`ENDPOINT_REJECTED`, `ENDPOINT_FAILURE`, `TIMED_OUT`.

`project_command_operation_result` is the strict projection;
`CommandProjectionError` its failure type.

---

## 7. Mutation values and failure semantics

### MutationValue / normalize_mutation_value(value)

```python
MutationValue = bool | str | int | float | bytes

def normalize_mutation_value(value: object) -> MutationValue: ...
```

Validates one mutation value before any transport object exists. `bool`,
`str`, and `bytes` pass through; `float` must be finite; `int` must
convert to the version-1 wire `double` **exactly** — a lossy integer
raises `mutation-number-not-exact`. `None`, mutable byte arrays, and
other types are rejected. `write_property` and `execute_command` apply
this normalization internally.

```python
from hase import normalize_mutation_value

normalize_mutation_value(0.5)          # 0.5
normalize_mutation_value(2**53)        # 9007199254740992.0 (exact)
normalize_mutation_value(2**53 + 1)    # raises: mutation-number-not-exact
```

### RuntimeHostMutationError / MutationFailureClassification

```python
class RuntimeHostMutationError(RuntimeError):
    code: str
    classification: MutationFailureClassification

    @property
    def outcome_uncertain(self) -> bool: ...
    @property
    def automatic_retry_permitted(self) -> bool: ...   # always False
```

Classifications:

| Classification      | Meaning                                          |
| ------------------- | ------------------------------------------------ |
| `NOT_SENT`          | the request never left the process; nothing happened |
| `REJECTED`          | the host or endpoint explicitly refused; nothing happened |
| `OUTCOME_UNCERTAIN` | the mutation may or may not have taken effect    |

`OUTCOME_UNCERTAIN` covers timeouts, cancellation, endpoint failure, and
transport loss after sending. The client never retries; reconcile with an
authoritative read (or explicit operator action) before considering
another mutation.

---

## 8. Observation model

`observe()` yields `ObservationMessage` values:

```python
ObservationMessage = ObservationInitialSnapshot | RuntimeHostObservation

@dataclass(frozen=True)
class ObservationInitialSnapshot:
    snapshot: RuntimeHostSnapshot
    snapshot_sequence: int           # observations continue from here

@dataclass(frozen=True)
class RuntimeHostObservation:
    sequence: int                    # strictly contiguous
    endpoint_id: str
    attachment_generation: str
    kind: ObservationKind
    payload: ObservationPayload
```

`ObservationKind` and the matching payload types:

| Kind                        | Payload type              | Payload fields |
| --------------------------- | ------------------------- | -------------- |
| `ATTACHMENT_PUBLISHED`      | `AttachmentPublished`     | `endpoint: RuntimeEndpointSnapshot` |
| `ATTACHMENT_ENDED`          | `AttachmentEnded`         | `ended_at_utc: datetime` |
| `CONNECTION_STATUS_CHANGED` | `ConnectionStatusChanged` | `previous_status`, `current_status` |
| `PROPERTY_VALUE_CHANGED`    | `PropertyValueChanged`    | `instrument_id`, `property_id`, `previous_value`, `current_value` |
| `EVENT_OCCURRED`            | `EventOccurred`           | `instrument_id`, `event_path_segments`, `occurred_at_utc`, `value` |

Match on the payload type rather than the kind when convenient:

```python
from hase import EventOccurred

async for message in client.observe():
    match message:
        case ObservationInitialSnapshot():
            pass
        case RuntimeHostObservation(payload=EventOccurred() as event):
            print("/".join(event.event_path_segments), event.value)
```

`project_observe_response` is the strict per-message projection;
`ObservationProjectionError` its failure type.

---

## 9. Diagnostic model

`observe_diagnostics()` yields:

```python
@dataclass(frozen=True)
class DiagnosticObservation:
    sequence: int                # strictly contiguous stream sequence
    record: DiagnosticRecord

@dataclass(frozen=True)
class DiagnosticRecord:
    runtime_host_id: str
    source_sequence: int         # the host's own capture sequence
    timestamp_utc: datetime
    level: DiagnosticLevel       # OPERATIONAL, PROTOCOL, BYTES
    category: DiagnosticCategory
    event_name: str
    severity: DiagnosticSeverity # TRACE, INFORMATION, WARNING, ERROR
    endpoint_id: str | None
    attachment_generation: str | None
    direction: DiagnosticDirection | None    # OUTBOUND, INBOUND
    operation_id: str | None
    duration: timedelta | None
    outcome: DiagnosticOutcome | None
                                 # SUCCEEDED, FAILED, CANCELLED, TIMED_OUT
    details: tuple[tuple[str, str], ...]     # ordered key/value pairs
    byte_snapshot: DiagnosticByteSnapshot | None

@dataclass(frozen=True)
class DiagnosticByteSnapshot:
    original_byte_count: int
    captured_bytes: bytes        # bounded capture, at most 256 bytes
    is_truncated: bool
```

`DiagnosticCategory` values: `RUNTIME_ATTACHMENT`, `RUNTIME_CONNECTION`,
`RUNTIME_SYNCHRONIZATION`, `RUNTIME_RECOVERY`, `RUNTIME_PROPERTY`,
`RUNTIME_COMMAND`, `RUNTIME_EVENT`, `PROTOCOL_EXCHANGE`,
`TRANSPORT_BYTES`.

Records are sanitized at capture — they never contain secrets,
credentials, or network addresses. `project_diagnostic_observation` is
the strict projection; `DiagnosticProjectionError` its failure type.

For durable diagnostics without a live stream, use the diagnostics-window
Export in either application and the `Hase.Diagnostics.Offline` tool
instead; the live stream requires explicit host-side enablement.

---

## 10. Error type summary

| Type                            | Base           | Raised by |
| ------------------------------- | -------------- | --------- |
| `ProfileValidationError`        | `ValueError`   | `load_runtime_host_profile` |
| `AutomationTargetRegistryError` | `ValueError`   | registry loading and `resolve` |
| `RuntimeHostChannelError`       | `RuntimeError` | channel open and close |
| `RuntimeHostClientError`        | `RuntimeError` | all non-mutation client operations |
| `RuntimeHostMutationError`      | `RuntimeError` | `write_property`, `execute_command` |
| `SnapshotProjectionError`       | `ValueError`   | snapshot projection |
| `PropertyProjectionError`       | `ValueError`   | Property model and projection |
| `CommandProjectionError`        | `ValueError`   | Command model and projection |
| `ObservationProjectionError`    | `ValueError`   | observation projection |
| `DiagnosticProjectionError`     | `ValueError`   | diagnostic projection |

Every type carries a stable `code: str`. Codes are sanitized: they never
include credential bytes, private paths, addresses, or raw server
diagnostics.

`hase.__version__` is the installed package version (`"0.6.0"`).
