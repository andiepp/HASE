"""Fixed-output physical validation for the Python mutual-TLS channel."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
import sys

from hase.channel import RuntimeHostChannelError, open_runtime_host_channel
from hase.profile import ProfileValidationError, load_runtime_host_profile


async def _validate(profile_path: str) -> None:
    profile = load_runtime_host_profile(profile_path)
    channel = await open_runtime_host_channel(profile, readiness_timeout=10.0)
    await channel.close()


def main(arguments: Sequence[str] | None = None) -> int:
    """Validate one physical channel without invoking a Runtime Host RPC."""

    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print(
            "Python physical channel validation failed: arguments-invalid.",
            file=sys.stderr,
        )
        return 1

    try:
        asyncio.run(_validate(supplied[0]))
    except ProfileValidationError as failure:
        print(
            f"Python physical channel validation failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except RuntimeHostChannelError as failure:
        print(
            f"Python physical channel validation failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "Python physical channel validation failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded       : True")
    print("Channel ready        : True")
    print("Channel closed       : True")
    print("Validation succeeded : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

