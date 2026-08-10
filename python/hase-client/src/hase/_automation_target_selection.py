"""Installed launcher boundary for explicit target-registry selection."""

from __future__ import annotations

from collections.abc import Sequence
import sys

from hase.profile import ProfileValidationError
from hase.targets import AutomationTargetRegistryError, load_automation_target_registry


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) < 2:
        print("HASE target selection failed: arguments-invalid.", file=sys.stderr)
        return 1
    registry_path, target_id, *excluded_roots = supplied
    try:
        registry = load_automation_target_registry(
            registry_path,
            excluded_roots=excluded_roots,
        )
        target = registry.resolve(target_id)
    except (AutomationTargetRegistryError, ProfileValidationError) as failure:
        print(f"HASE target selection failed: {failure.code}.", file=sys.stderr)
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print("HASE target selection failed: unexpected-failure.", file=sys.stderr)
        return 1
    print(target.profile_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
