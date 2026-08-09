"""Fixed-output physical validation for one same-value Property write."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
from datetime import timezone
import math
import sys

from hase.channel import RuntimeHostChannelError, open_runtime_host_channel
from hase.client import RuntimeHostClient, RuntimeHostClientError
from hase.mutation import RuntimeHostMutationError
from hase.profile import ProfileValidationError, load_runtime_host_profile
from hase.property import (
    PropertyOperationResult,
    PropertyOperationStatus,
    PropertyProjectionError,
    PropertyQuality,
    PropertyTarget,
)
from hase.snapshot import (
    BooleanDataDescriptor,
    EndpointConnectionState,
    NumericDataDescriptor,
    PropertyAccessMode,
    RuntimeHostSnapshot,
    SnapshotProjectionError,
    StringDataDescriptor,
)


_INSTRUMENT_ID = "electronic-load-01"
_MODE_ID = "operating-mode"
_INPUT_ID = "input-enabled"
_TARGET_ID = "target-current"


class _PhysicalWriteValidationError(RuntimeError):
    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def _resolve_targets(snapshot: RuntimeHostSnapshot) -> tuple[PropertyTarget, ...]:
    candidates: list[tuple[PropertyTarget, ...]] = []
    for endpoint in snapshot.endpoints:
        if endpoint.connection_status.state is not EndpointConnectionState.READY:
            continue
        for instrument in endpoint.descriptor.instruments:
            if instrument.instrument_id != _INSTRUMENT_ID:
                continue
            descriptors = {item.property_id: item for item in instrument.properties}
            mode = descriptors.get(_MODE_ID)
            input_enabled = descriptors.get(_INPUT_ID)
            target = descriptors.get(_TARGET_ID)
            if (
                mode is not None
                and mode.access_mode in (PropertyAccessMode.READ,
                    PropertyAccessMode.READ_WRITE)
                and isinstance(mode.data, StringDataDescriptor)
                and input_enabled is not None
                and input_enabled.access_mode in (PropertyAccessMode.READ,
                    PropertyAccessMode.READ_WRITE)
                and isinstance(input_enabled.data, BooleanDataDescriptor)
                and target is not None
                and target.access_mode is PropertyAccessMode.READ_WRITE
                and isinstance(target.data, NumericDataDescriptor)
            ):
                candidates.append(tuple(
                    PropertyTarget(endpoint.endpoint_id,
                        endpoint.attachment_generation, _INSTRUMENT_ID, item)
                    for item in (_MODE_ID, _INPUT_ID, _TARGET_ID)
                ))
    if len(candidates) != 1:
        raise _PhysicalWriteValidationError("property-targets-not-unique")
    return candidates[0]


def _value(result: PropertyOperationResult, expected_type: type) -> object:
    if (
        result.status is not PropertyOperationStatus.SUCCESS
        or result.diagnostic is not None
        or result.confirmed_value is None
        or type(result.confirmed_value.value) is not expected_type
        or result.confirmed_value.quality is not PropertyQuality.GOOD
        or result.confirmed_value.timestamp_utc.tzinfo is not timezone.utc
    ):
        raise _PhysicalWriteValidationError("property-result-invalid")
    value = result.confirmed_value.value
    if expected_type is float and not math.isfinite(value):
        raise _PhysicalWriteValidationError("property-result-invalid")
    return value


async def _validate(profile_path: str) -> None:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    try:
        client = RuntimeHostClient(channel)
        targets = _resolve_targets(await client.get_snapshot(timeout=10.0))
        mode = _value(
            await client.read_authoritative_property(targets[0], timeout=10.0),
            str,
        )
        input_enabled = _value(
            await client.read_authoritative_property(targets[1], timeout=10.0),
            bool,
        )
        original = _value(
            await client.read_authoritative_property(targets[2], timeout=10.0),
            float,
        )
        if mode != "CC" or input_enabled is not False:
            raise _PhysicalWriteValidationError("kel103-state-not-safe")

        confirmed = _value(
            await client.write_property(targets[2], original, timeout=10.0),
            float,
        )
        if confirmed != original:
            raise _PhysicalWriteValidationError("write-confirmation-mismatch")
        reconciled = _value(
            await client.read_authoritative_property(targets[2], timeout=10.0),
            float,
        )
        if reconciled != original:
            raise _PhysicalWriteValidationError("write-reconciliation-mismatch")
    finally:
        await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print("Python physical Property-write validation failed: arguments-invalid.",
            file=sys.stderr)
        return 1
    try:
        asyncio.run(_validate(supplied[0]))
    except (ProfileValidationError, RuntimeHostChannelError,
        RuntimeHostClientError, RuntimeHostMutationError,
        SnapshotProjectionError, PropertyProjectionError,
        _PhysicalWriteValidationError) as failure:
        print(f"Python physical Property-write validation failed: {failure.code}.",
            file=sys.stderr)
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print("Python physical Property-write validation failed: unexpected-failure.",
            file=sys.stderr)
        return 1

    print("Profile loaded              : True")
    print("Safe KEL-103 state verified : True")
    print("Same-value write completed  : True")
    print("Confirmation matched        : True")
    print("Reconciliation matched      : True")
    print("Channel closed              : True")
    print("Validation succeeded        : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
