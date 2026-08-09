"""Fixed-output physical validation for one authorized Runtime Host snapshot."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
import sys

from hase.channel import RuntimeHostChannelError, open_runtime_host_channel
from hase.client import RuntimeHostClient, RuntimeHostClientError
from hase.profile import ProfileValidationError, load_runtime_host_profile
from hase.snapshot import SnapshotProjectionError


async def _validate(profile_path: str) -> None:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    try:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot(timeout=10.0)
        if snapshot.api_version.major != 1:
            raise RuntimeHostClientError("snapshot-api-version-unsupported")
    finally:
        await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    """Validate one physical, authorized GetSnapshot operation."""

    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print(
            "Python physical snapshot validation failed: arguments-invalid.",
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
    ) as failure:
        print(
            f"Python physical snapshot validation failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "Python physical snapshot validation failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded       : True")
    print("Channel ready        : True")
    print("Snapshot received    : True")
    print("Snapshot valid       : True")
    print("Channel closed       : True")
    print("Validation succeeded : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
