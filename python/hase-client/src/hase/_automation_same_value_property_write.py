"""Confirmed installed workflow for one reconciled same-value Property write."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
import sys

from hase._physical_property_write_validation import (
    _PhysicalWriteValidationError,
    _validate,
)
from hase.channel import RuntimeHostChannelError
from hase.client import RuntimeHostClientError
from hase.mutation import RuntimeHostMutationError
from hase.profile import ProfileValidationError
from hase.property import PropertyProjectionError
from hase.snapshot import SnapshotProjectionError


_CONFIRMATION = "confirm-same-value-write"


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 2 or supplied[1] != _CONFIRMATION:
        print(
            "HASE same-value Property workflow failed: confirmation-required.",
            file=sys.stderr,
        )
        return 1

    try:
        asyncio.run(_validate(supplied[0]))
    except (
        ProfileValidationError,
        RuntimeHostChannelError,
        RuntimeHostClientError,
        RuntimeHostMutationError,
        SnapshotProjectionError,
        PropertyProjectionError,
        _PhysicalWriteValidationError,
    ) as failure:
        print(
            f"HASE same-value Property workflow failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "HASE same-value Property workflow failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded              : True")
    print("Safe KEL-103 state verified : True")
    print("Same-value write completed  : True")
    print("Confirmation matched        : True")
    print("Reconciliation matched      : True")
    print("Channel closed              : True")
    print("Workflow succeeded          : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
