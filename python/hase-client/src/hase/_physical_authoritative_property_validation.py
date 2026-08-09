"""Fixed-output physical validation for one authoritative Property read."""

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


_INSTRUMENT_ID = "electronic-load-01"
_PROPERTY_ID = "measured-voltage"


class _PhysicalPropertyValidationError(RuntimeError):
    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def _resolve_target(snapshot: RuntimeHostSnapshot) -> PropertyTarget:
    candidates: list[PropertyTarget] = []
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
                ):
                    candidates.append(
                        PropertyTarget(
                            endpoint.endpoint_id,
                            endpoint.attachment_generation,
                            instrument.instrument_id,
                            descriptor.property_id,
                        )
                    )
    if len(candidates) != 1:
        raise _PhysicalPropertyValidationError("property-target-not-unique")
    return candidates[0]


def _validate_result(result: object) -> None:
    if (
        getattr(result, "status", None) is not PropertyOperationStatus.SUCCESS
        or getattr(result, "diagnostic", None) is not None
        or getattr(result, "confirmed_value", None) is None
    ):
        raise _PhysicalPropertyValidationError("property-result-invalid")
    confirmed = result.confirmed_value
    if (
        type(confirmed.value) is not float
        or not math.isfinite(confirmed.value)
        or confirmed.quality is not PropertyQuality.GOOD
        or confirmed.timestamp_utc.tzinfo is not timezone.utc
    ):
        raise _PhysicalPropertyValidationError("property-result-invalid")


async def _validate(profile_path: str) -> None:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    try:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot(timeout=10.0)
        target = _resolve_target(snapshot)
        result = await client.read_authoritative_property(target, timeout=10.0)
        _validate_result(result)
    finally:
        await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    """Validate one physical authoritative measured-voltage read."""

    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print(
            "Python physical Property validation failed: arguments-invalid.",
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
        _PhysicalPropertyValidationError,
    ) as failure:
        print(
            f"Python physical Property validation failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "Python physical Property validation failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded       : True")
    print("Channel ready        : True")
    print("Target resolved      : True")
    print("Read completed       : True")
    print("Result valid         : True")
    print("Channel closed       : True")
    print("Validation succeeded : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
