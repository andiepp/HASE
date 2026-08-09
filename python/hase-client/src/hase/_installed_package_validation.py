"""Fixed-output validation for an isolated hase-client wheel installation."""

from __future__ import annotations

import importlib.metadata
from pathlib import Path
import sys

import hase
from hase._generated import runtime_host_remote_api_v1_pb2 as pb2
from hase._generated import runtime_host_remote_api_v1_pb2_grpc as pb2_grpc


_EXPECTED_RPCS = {
    "ExecuteCommand",
    "GetSnapshot",
    "ObserveDiagnostics",
    "Observe",
    "ReadCachedProperty",
    "ReadAuthoritativeProperty",
    "WriteProperty",
}


def _is_inside(path: Path, directory: Path) -> bool:
    try:
        path.resolve(strict=True).relative_to(directory.resolve(strict=True))
    except ValueError:
        return False
    return True


def main() -> int:
    environment = Path(sys.prefix)
    package_file = Path(hase.__file__)
    distribution = importlib.metadata.distribution("hase-client")
    service = pb2.DESCRIPTOR.services_by_name["RuntimeHostRemoteApi"]

    outcomes = {
        "Installed distribution found": distribution.version == hase.__version__,
        "Import is isolated": _is_inside(package_file, environment),
        "Public API is complete": bool(hase.__all__)
        and len(hase.__all__) == len(set(hase.__all__))
        and all(hasattr(hase, name) for name in hase.__all__),
        "Generated messages included": hasattr(pb2, "GetSnapshotRequest"),
        "Generated gRPC client included": hasattr(
            pb2_grpc, "RuntimeHostRemoteApiStub"
        ),
        "All version-1 RPCs included": {
            method.name for method in service.methods
        }
        == _EXPECTED_RPCS,
    }

    for label, succeeded in outcomes.items():
        print(f"{label}: {succeeded}")

    return 0 if all(outcomes.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
