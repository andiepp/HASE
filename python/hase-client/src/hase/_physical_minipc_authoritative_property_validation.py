"""Fixed-output MiniPC validation for one authoritative Arduino A0 read."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
from datetime import timezone
import math
import sys

from hase.channel import RuntimeHostChannelError, open_runtime_host_channel
from hase.client import RuntimeHostClient, RuntimeHostClientError
from hase.profile import ProfileValidationError, load_runtime_host_profile
from hase.property import (
    PropertyOperationStatus,
    PropertyProjectionError,
    PropertyQuality,
    PropertyTarget,
)
from hase.snapshot import (
    EndpointConnectionState,
    NumericDataDescriptor,
    PropertyAccessMode,
    RuntimeHostSnapshot,
    SnapshotProjectionError,
)


_INSTRUMENT_ID = "arduino-uno-controller-01"
_PROPERTY_ID = "analog-input-voltage"


class _MiniPcPropertyValidationError(RuntimeError):
    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def _resolve_target(
    snapshot: RuntimeHostSnapshot,
) -> tuple[PropertyTarget, NumericDataDescriptor]:
    candidates: list[tuple[PropertyTarget, NumericDataDescriptor]] = []
    for endpoint in snapshot.endpoints:
        if endpoint.connection_status.state is not EndpointConnectionState.READY:
            continue
        for instrument in endpoint.descriptor.instruments:
            if instrument.instrument_id != _INSTRUMENT_ID:
                continue
            for descriptor in instrument.properties:
                if (
                    descriptor.property_id == _PROPERTY_ID
                    and descriptor.access_mode
                    in (PropertyAccessMode.READ, PropertyAccessMode.READ_WRITE)
                    and isinstance(descriptor.data, NumericDataDescriptor)
                    and descriptor.data.value_range is not None
                ):
                    candidates.append(
                        (
                            PropertyTarget(
                                endpoint.endpoint_id,
                                endpoint.attachment_generation,
                                instrument.instrument_id,
                                descriptor.property_id,
                            ),
                            descriptor.data,
                        )
                    )
    if len(candidates) != 1:
        raise _MiniPcPropertyValidationError("property-target-not-unique")
    return candidates[0]


def _validate_result(result: object, descriptor: NumericDataDescriptor) -> None:
    if (
        getattr(result, "status", None) is not PropertyOperationStatus.SUCCESS
        or getattr(result, "diagnostic", None) is not None
        or getattr(result, "confirmed_value", None) is None
    ):
        raise _MiniPcPropertyValidationError("property-result-invalid")
    confirmed = result.confirmed_value
    value_range = descriptor.value_range
    if (
        type(confirmed.value) is not float
        or not math.isfinite(confirmed.value)
        or confirmed.quality is not PropertyQuality.GOOD
        or confirmed.timestamp_utc.tzinfo is not timezone.utc
        or value_range is None
        or confirmed.value < value_range.minimum
        or confirmed.value > value_range.maximum
    ):
        raise _MiniPcPropertyValidationError("property-result-invalid")


async def _validate(profile_path: str) -> None:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    try:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot(timeout=10.0)
        target, descriptor = _resolve_target(snapshot)
        result = await client.read_authoritative_property(target, timeout=10.0)
        _validate_result(result, descriptor)
    finally:
        await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    """Validate one physical authoritative MiniPC Arduino A0 read."""

    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print(
            "MiniPC Python authoritative Property validation failed: arguments-invalid.",
            file=sys.stderr,
        )
        return 1

    try:
        asyncio.run(_validate(supplied[0]))
    except (
        ProfileValidationError,
        RuntimeHostChannelError,
        RuntimeHostClientError,
        SnapshotProjectionError,
        PropertyProjectionError,
        _MiniPcPropertyValidationError,
    ) as failure:
        print(
            "MiniPC Python authoritative Property validation failed: "
            f"{failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "MiniPC Python authoritative Property validation failed: "
            "unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded       : True")
    print("Channel ready        : True")
    print("A0 target resolved   : True")
    print("Read completed       : True")
    print("Result valid         : True")
    print("Channel closed       : True")
    print("Validation succeeded : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
