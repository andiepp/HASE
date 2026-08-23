# Getting Started with HASE — Python API

This guide takes you from an empty directory to your first Python programs
against a HASE Runtime Host: listing the hardware inventory, reading and
writing Properties, executing Commands, and watching live changes. The
complete public API is documented in the
[Python Client API Reference](API%20reference/HASE-Python-Client-API-Reference.md).

## What the Python client is — and is not

The `hase-client` package is an asyncio-native Python client for the HASE
Runtime Host's northbound gRPC API. It gives scripts and automation the same
normalized model the WPF Client uses: Endpoints, Instruments, Properties,
Commands, and Events — independent of how each device is physically
connected.

The client is deliberately conservative:

- It connects only to the **secured** HTTPS binding with mutual TLS. The
  certificate-free loopback development profile from
  [Getting Started](Getting-Started.md) is not reachable from Python.
- It never discovers hosts, never retries, never reconnects, and never
  falls back. Every operation happens exactly once or fails with a
  sanitized error code.
- Every operation is authorized individually. Your Python principal only
  has the capabilities its grants allow — a read-only principal cannot
  write, whatever the script asks for.

## Prerequisites

1. **A provisioned secured Runtime Host.** Set one up by following the
   example ladder through
   [Example 3](examples/Example-3-Client-on-a-Second-PC.md), or run the
   Python client on the same PC as the host — the connection model is
   identical.
2. **A dedicated Python client credential.** The WPF Client's private key
   is deliberately non-exportable; Python automation uses its own
   certificate, key, and enrollment. Provision it with the tools described
   in the [Python Client engineering notes](../python/hase-client/README.md)
   (`Test-HasePythonCredentialProvisioningReadiness.ps1` and the
   `Hase.Python.CredentialProvisioning.Operator` `provision` command). The
   result is four files: your client certificate chain, private key, the
   host's trusted server certificate, and a connection profile referencing
   them.
3. **64-bit CPython 3.12 or 3.13.**

## Installing the package

For development directly from the repository, from
`python\hase-client` in an ordinary PowerShell window:

```powershell
.\tools\Initialize-HasePythonDevelopment.ps1
.\tools\Test-HasePythonDevelopment.ps1
```

This creates a private `.venv` inside `python\hase-client` with the package
installed editable. Activate it with
`.\.venv\Scripts\Activate.ps1`.

For use outside the repository, build a wheel with
`.\tools\Build-HasePythonPackage.ps1` and install it into your own virtual
environment with `pip install <wheel>`; the
[engineering notes](../python/hase-client/README.md) describe the verified
installation workflow.

Everything below imports from the single public namespace:

```python
import hase
```

## The connection profile

Every connection starts from one small JSON file you own — the profile. It
names the host's address and your three credential files; it never contains
a certificate or key itself:

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

- `address` must be `https://` with an explicit IP address and port — the
  secured binding you provisioned.
- The three paths must be absolute, existing, and distinct. The trusted
  server certificate pins the exact Runtime Host you expect; TLS authority
  is never overridden.
- Loading the profile validates file custody only — credential bytes are
  read just once, at connection time.

## First program — list the inventory

Save your profile as, for example, `C:\HasePython\profile.json`, start the
secured Runtime Host, and run:

```python
"""List every Endpoint, Instrument, Property, Command, and Event."""
import asyncio

from hase import (
    RuntimeHostClient,
    load_runtime_host_profile,
    open_runtime_host_channel,
)

PROFILE = r"C:\HasePython\profile.json"


async def main() -> None:
    profile = load_runtime_host_profile(PROFILE)
    channel = await open_runtime_host_channel(profile)
    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()

    print(f"API version: {snapshot.api_version.major}.{snapshot.api_version.minor}")
    for endpoint in snapshot.endpoints:
        state = endpoint.connection_status.state.value
        print(f"Endpoint {endpoint.endpoint_id} [{state}]")
        for instrument in endpoint.descriptor.instruments:
            print(f"  Instrument {instrument.instrument_id} ({instrument.kind})")
            for prop in instrument.properties:
                print(f"    Property {prop.property_id} "
                      f"[{prop.access_mode.value}]")
            for command in instrument.commands:
                print(f"    Command {'/'.join(command.path_segments)}")
            for event in instrument.events:
                print(f"    Event {'/'.join(event.path_segments)}")


asyncio.run(main())
```

The pattern shown here is the pattern of every HASE Python program: load
the profile, open one channel, use it inside `async with` so it always
closes, and work with immutable result models afterwards. `get_snapshot`
requires the `runtime-host.snapshot.read` grant.

## Reading a Property

Operations on a Property need a `PropertyTarget` carrying four identities:
the endpoint, its **attachment generation**, the instrument, and the
Property. The attachment generation identifies one connection epoch of the
device — you take it from a current snapshot, and the host rejects the
operation with `ATTACHMENT_NOT_CURRENT` if the device has reattached since.
That is deliberate: your read is bound to the device state you inspected.

```python
"""Read one Property authoritatively from the device."""
import asyncio

from hase import (
    NumericDataDescriptor,
    PropertyTarget,
    RuntimeHostClient,
    load_runtime_host_profile,
    open_runtime_host_channel,
)

PROFILE = r"C:\HasePython\profile.json"
ENDPOINT = "arduino-uno-01"
INSTRUMENT = "arduino-uno-controller-01"
PROPERTY = "analog-input-voltage"


async def main() -> None:
    profile = load_runtime_host_profile(PROFILE)
    channel = await open_runtime_host_channel(profile)
    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()

        endpoint = next(
            item for item in snapshot.endpoints
            if item.endpoint_id == ENDPOINT
        )
        target = PropertyTarget(
            endpoint_id=endpoint.endpoint_id,
            attachment_generation=endpoint.attachment_generation,
            instrument_id=INSTRUMENT,
            property_id=PROPERTY,
        )
        result = await client.read_authoritative_property(target)

    if not result.is_success:
        print(f"Read failed: {result.status.value}")
        return

    value = result.confirmed_value
    instrument = next(
        item for item in endpoint.descriptor.instruments
        if item.instrument_id == INSTRUMENT
    )
    descriptor = next(
        item for item in instrument.properties
        if item.property_id == PROPERTY
    )
    unit = ""
    if isinstance(descriptor.data, NumericDataDescriptor):
        unit = " " + descriptor.data.native_unit.symbol
    print(f"{descriptor.display_name}: {value.value}{unit}")
    print(f"Quality: {value.quality.value}")
    print(f"Timestamp (UTC): {value.timestamp_utc.isoformat()}")


asyncio.run(main())
```

An **authoritative** read goes to the device; `read_cached_property` reads
the host's synchronized cache instead, without contacting the endpoint.
They require the `property.authoritative.read` and `property.cached.read`
grants respectively.

## Writing a Property

Writes are mutations, and HASE treats mutations with respect: a write
happens exactly once or the failure tells you exactly what is known.
Failures raise `RuntimeHostMutationError` with one of three
classifications:

- `NOT_SENT` — the request never left your process; nothing happened.
- `REJECTED` — the host or device explicitly refused; nothing happened.
- `OUTCOME_UNCERTAIN` — the request may or may not have taken effect
  (timeout, transport loss). The client will never retry for you:
  reconcile with an authoritative read before doing anything else.

```python
"""Write one Property and reconcile the result."""
import asyncio

from hase import (
    PropertyTarget,
    RuntimeHostClient,
    RuntimeHostMutationError,
    load_runtime_host_profile,
    open_runtime_host_channel,
)

PROFILE = r"C:\HasePython\profile.json"
ENDPOINT = "arduino-uno-01"
INSTRUMENT = "arduino-uno-controller-01"
PROPERTY = "built-in-led-state"
NEW_VALUE = True


async def main() -> None:
    profile = load_runtime_host_profile(PROFILE)
    channel = await open_runtime_host_channel(profile)
    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()
        endpoint = next(
            item for item in snapshot.endpoints
            if item.endpoint_id == ENDPOINT
        )
        target = PropertyTarget(
            endpoint.endpoint_id,
            endpoint.attachment_generation,
            INSTRUMENT,
            PROPERTY,
        )
        try:
            result = await client.write_property(target, NEW_VALUE)
        except RuntimeHostMutationError as failure:
            print(f"Write failed ({failure.classification.value}): "
                  f"{failure.code}")
            if failure.outcome_uncertain:
                check = await client.read_authoritative_property(target)
                if check.is_success:
                    print(f"Reconciled value: {check.confirmed_value.value}")
            return
        print(f"Confirmed value: {result.confirmed_value.value}")


asyncio.run(main())
```

A successful `write_property` returns the confirmed value the device
reported back — not the value you asked for. Writes require the
`property.write` grant. Supported value types are `bool`, `str`, `int`
(when exactly representable), `float`, and `bytes`.

## Executing a Command

Commands are addressed by their path segments from the snapshot descriptor
and follow the same once-only mutation semantics as writes:

```python
"""Execute one parameterless Command."""
import asyncio

from hase import (
    CommandTarget,
    RuntimeHostClient,
    RuntimeHostMutationError,
    load_runtime_host_profile,
    open_runtime_host_channel,
)

PROFILE = r"C:\HasePython\profile.json"
ENDPOINT = "arduino-uno-01"
INSTRUMENT = "arduino-uno-controller-01"
COMMAND_PATH = ("Led", "Toggle")


async def main() -> None:
    profile = load_runtime_host_profile(PROFILE)
    channel = await open_runtime_host_channel(profile)
    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()
        endpoint = next(
            item for item in snapshot.endpoints
            if item.endpoint_id == ENDPOINT
        )
        target = CommandTarget(
            endpoint.endpoint_id,
            endpoint.attachment_generation,
            INSTRUMENT,
            COMMAND_PATH,
        )
        try:
            result = await client.execute_command(target)
        except RuntimeHostMutationError as failure:
            print(f"Command failed ({failure.classification.value}): "
                  f"{failure.code}")
            return
        if result.return_value is not None:
            print(f"Returned: {result.return_value}")
        print(f"Status: {result.status.value}")


asyncio.run(main())
```

Commands require the `command.execute` grant. A typed argument, when the
descriptor declares one, is passed as the second positional parameter.

## Watching live changes

`observe()` opens one stream: the first item is a complete initial
snapshot, and every following item is one typed observation — an
attachment appearing or ending, a connection-status change, a Property
value change, or an Event:

```python
"""Print live observations until interrupted."""
import asyncio

from hase import (
    EventOccurred,
    ObservationInitialSnapshot,
    PropertyValueChanged,
    RuntimeHostClient,
    load_runtime_host_profile,
    open_runtime_host_channel,
)

PROFILE = r"C:\HasePython\profile.json"


async def main() -> None:
    profile = load_runtime_host_profile(PROFILE)
    channel = await open_runtime_host_channel(profile)
    async with channel:
        client = RuntimeHostClient(channel)
        async for message in client.observe():
            if isinstance(message, ObservationInitialSnapshot):
                count = len(message.snapshot.endpoints)
                print(f"Initial snapshot: {count} endpoint(s)")
                continue
            payload = message.payload
            if isinstance(payload, PropertyValueChanged):
                print(f"{message.endpoint_id} "
                      f"{payload.instrument_id}/{payload.property_id}: "
                      f"{payload.current_value.value}")
            elif isinstance(payload, EventOccurred):
                path = "/".join(payload.event_path_segments)
                print(f"{message.endpoint_id} event {path}: "
                      f"{payload.value}")
            else:
                print(f"{message.kind.value} on {message.endpoint_id}")


asyncio.run(main())
```

Stop it with Ctrl+C — cancelling the iterator cancels the subscription.
The stream is strictly ordered; a gap or malformed message terminates it
with an error, and the client never resubscribes on its own. Observation
requires the `observation.subscribe` grant.

## When something fails

Every failure carries a stable, sanitized `code` — never credential bytes,
addresses, or raw server diagnostics:

- `ProfileValidationError` — the profile file is malformed or its
  credential files are missing.
- `RuntimeHostChannelError` — the channel could not be opened or closed
  (for example `channel-readiness-timeout` when the host is not running or
  not reachable).
- `RuntimeHostClientError` — an operation failed
  (`rpc-permission-denied` means your principal lacks the grant;
  `rpc-unavailable` means the connection was lost).
- `RuntimeHostMutationError` — a write or Command failed, with the
  classification explained above.

The [API reference](API%20reference/HASE-Python-Client-API-Reference.md)
lists the codes per operation.

## Where to go next

- The complete
  [Python Client API Reference](API%20reference/HASE-Python-Client-API-Reference.md)
  documents every public function and model with an example.
- `python\hase-client\examples\` contains ready-to-run command-line
  versions of these programs (they use the two-target automation registry
  described in the reference).
- The [Python Client engineering notes](../python/hase-client/README.md)
  cover provisioning, wheel distribution, and the guarded automation
  workflows.
