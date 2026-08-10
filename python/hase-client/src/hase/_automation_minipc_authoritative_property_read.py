"""Installed read-only workflow for one authoritative MiniPC Arduino A0 read."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
import sys

from hase._physical_minipc_authoritative_property_validation import (
    _MiniPcPropertyValidationError,
    _validate,
)
from hase.channel import RuntimeHostChannelError
from hase.client import RuntimeHostClientError
from hase.profile import ProfileValidationError
from hase.property import PropertyProjectionError
from hase.snapshot import SnapshotProjectionError


def main(arguments: Sequence[str] | None = None) -> int:
    """Run one installed, bounded, authoritative MiniPC Arduino A0 read."""

    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 1:
        print(
            "HASE MiniPC authoritative read failed: arguments-invalid.",
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
            f"HASE MiniPC authoritative read failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "HASE MiniPC authoritative read failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded       : True")
    print("Channel ready        : True")
    print("A0 target resolved   : True")
    print("Read completed       : True")
    print("Result valid         : True")
    print("Channel closed       : True")
    print("Workflow succeeded   : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
