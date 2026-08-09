"""Confirmed installed workflow for one reconciled same-state CC Command."""

from __future__ import annotations

import asyncio
from collections.abc import Sequence
import sys

from hase._physical_command_validation import ValidationError, validate
from hase.channel import RuntimeHostChannelError
from hase.client import RuntimeHostClientError
from hase.command import CommandProjectionError
from hase.mutation import RuntimeHostMutationError
from hase.profile import ProfileValidationError
from hase.property import PropertyProjectionError
from hase.snapshot import SnapshotProjectionError


_CONFIRMATION = "confirm-same-state-cc-command"


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 2 or supplied[1] != _CONFIRMATION:
        print(
            "HASE same-state Command workflow failed: confirmation-required.",
            file=sys.stderr,
        )
        return 1

    try:
        asyncio.run(validate(supplied[0]))
    except (
        ProfileValidationError,
        RuntimeHostChannelError,
        RuntimeHostClientError,
        RuntimeHostMutationError,
        SnapshotProjectionError,
        PropertyProjectionError,
        CommandProjectionError,
        ValidationError,
    ) as failure:
        print(
            f"HASE same-state Command workflow failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "HASE same-state Command workflow failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    print("Profile loaded              : True")
    print("Safe KEL-103 state verified : True")
    print("CC command executed once    : True")
    print("CC/OFF reconciliation exact : True")
    print("Channel closed              : True")
    print("Workflow succeeded          : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
