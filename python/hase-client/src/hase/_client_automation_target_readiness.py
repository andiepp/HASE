"""Fixed-output Laptop target-registry readiness validation."""

from __future__ import annotations

from collections.abc import Sequence
import sys

from hase.profile import ProfileValidationError
from hase.targets import AutomationTargetRegistryError, load_automation_target_registry


def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    if len(supplied) != 3:
        print("Laptop Python target readiness failed: arguments-invalid.", file=sys.stderr)
        return 1
    registry_path, repository_root, installation_root = supplied
    try:
        registry = load_automation_target_registry(
            registry_path,
            excluded_roots=(repository_root, installation_root),
        )
        desktop = registry.resolve("desktop-runtime-host")
        minipc = registry.resolve("minipc-runtime-host")
    except (AutomationTargetRegistryError, ProfileValidationError) as failure:
        print(
            f"Laptop Python target readiness failed: {failure.code}.",
            file=sys.stderr,
        )
        return 1
    except (KeyboardInterrupt, SystemExit):
        raise
    except Exception:
        print(
            "Laptop Python target readiness failed: unexpected-failure.",
            file=sys.stderr,
        )
        return 1

    if desktop.profile_path == minipc.profile_path:
        print(
            "Laptop Python target readiness failed: target-profiles-not-distinct.",
            file=sys.stderr,
        )
        return 1
    print("Registry loaded          : True")
    print("Desktop target ready     : True")
    print("MiniPC target ready      : True")
    print("Profile custody external : True")
    print("Profiles distinct        : True")
    print("Credentials distinct     : True")
    print("Connection attempted     : False")
    print("Laptop targets ready     : True")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
