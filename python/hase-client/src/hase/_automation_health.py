"""Read-only health workflow for a locally installed automation environment."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
from dataclasses import dataclass
import sys

from hase.channel import RuntimeHostChannelError, open_runtime_host_channel
from hase.client import RuntimeHostClient, RuntimeHostClientError
from hase.profile import ProfileValidationError, load_runtime_host_profile
from hase.snapshot import SnapshotProjectionError


@dataclass(frozen=True, slots=True)
class _HealthSummary:
    endpoint_count: int
    ready_endpoint_count: int
    instrument_count: int
    property_count: int
    command_count: int
    event_count: int


async def _run(profile_path: str) -> _HealthSummary:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    try:
        snapshot = await RuntimeHostClient(channel).get_snapshot(timeout=10.0)
        if snapshot.api_version.major != 1:
            raise RuntimeHostClientError("snapshot-api-version-unsupported")
        endpoints = snapshot.endpoints
        instruments = tuple(
            instrument
            for endpoint in endpoints
            for instrument in endpoint.descriptor.instruments
        )
        return _HealthSummary(
            endpoint_count=len(endpoints),
            ready_endpoint_count=sum(
                endpoint.connection_status.state.value == "ready"
                for endpoint in endpoints
            ),
            instrument_count=len(instruments),
            property_count=sum(len(item.properties) for item in instruments),
            command_count=sum(len(item.commands) for item in instruments),
            event_count=sum(len(item.events) for item in instruments),
        )
    finally:
        await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print("HASE automation health failed: arguments-invalid.", file=sys.stderr)
        return 1

    try:
        summary = asyncio.run(_run(supplied[0]))
    except (
        ProfileValidationError,
        RuntimeHostChannelError,
        RuntimeHostClientError,
        SnapshotProjectionError,
    ) as failure:
        print(f"HASE automation health failed: {failure.code}.", file=sys.stderr)
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print("HASE automation health failed: unexpected-failure.", file=sys.stderr)
        return 1

    print("Profile loaded        : True")
    print("Channel ready         : True")
    print("Snapshot valid        : True")
    print(f"Endpoint count        : {summary.endpoint_count}")
    print(f"Ready endpoint count  : {summary.ready_endpoint_count}")
    print(f"Instrument count      : {summary.instrument_count}")
    print(f"Property count        : {summary.property_count}")
    print(f"Command count         : {summary.command_count}")
    print(f"Event count           : {summary.event_count}")
    print("Channel closed        : True")
    print("Workflow succeeded    : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
