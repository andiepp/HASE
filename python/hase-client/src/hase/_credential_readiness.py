"""Internal strict parsing for the Windows credential-readiness probe."""

from __future__ import annotations

import ipaddress
import json
from pathlib import Path
import re
import sys
from typing import Any


_MAXIMUM_DOCUMENT_BYTES = 64 * 1024
_THUMBPRINT = re.compile(r"[0-9A-Fa-f]{40}\Z")
_CREDENTIAL_ID = re.compile(r"x509-sha256:[0-9a-f]{64}\Z")


class _ReadinessDocumentError(ValueError):
    pass


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}

    for name, value in pairs:
        if name in result:
            raise _ReadinessDocumentError
        result[name] = value

    return result


def _read_document(path: Path) -> Any:
    if not path.is_absolute() or not path.is_file():
        raise _ReadinessDocumentError

    try:
        if path.stat().st_size > _MAXIMUM_DOCUMENT_BYTES:
            raise _ReadinessDocumentError
        text = path.read_bytes().decode("utf-8", errors="strict")
        return json.loads(text, object_pairs_hook=_strict_object)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise _ReadinessDocumentError from error


def _require_object(value: Any, names: frozenset[str]) -> dict[str, Any]:
    if not isinstance(value, dict) or frozenset(value) != names:
        raise _ReadinessDocumentError
    return value


def _load_configuration(path: Path) -> tuple[str, Path]:
    root = _require_object(
        _read_document(path),
        frozenset(
            {
                "formatVersion",
                "binding",
                "serverCertificate",
                "clientEnrollmentFilePath",
            }
        ),
    )

    if type(root["formatVersion"]) is not int or root["formatVersion"] != 1:
        raise _ReadinessDocumentError

    binding = _require_object(root["binding"], frozenset({"address", "port"}))
    address = binding["address"]
    port = binding["port"]

    if not isinstance(address, str) or not address or address != address.strip():
        raise _ReadinessDocumentError
    try:
        ipaddress.ip_address(address)
    except ValueError as error:
        raise _ReadinessDocumentError from error
    if type(port) is not int or port < 1 or port > 65535:
        raise _ReadinessDocumentError

    server_certificate = _require_object(
        root["serverCertificate"],
        frozenset({"storeName", "storeLocation", "thumbprint"}),
    )
    if (
        server_certificate["storeName"] != "My"
        or server_certificate["storeLocation"] != "CurrentUser"
        or not isinstance(server_certificate["thumbprint"], str)
        or _THUMBPRINT.fullmatch(server_certificate["thumbprint"]) is None
    ):
        raise _ReadinessDocumentError

    enrollment_value = root["clientEnrollmentFilePath"]
    if not isinstance(enrollment_value, str) or not enrollment_value:
        raise _ReadinessDocumentError
    enrollment_path = Path(enrollment_value)
    if not enrollment_path.is_absolute():
        raise _ReadinessDocumentError

    _validate_enrollment(enrollment_path)
    return server_certificate["thumbprint"].upper(), enrollment_path.resolve()


def _validate_enrollment(path: Path) -> None:
    root = _require_object(
        _read_document(path),
        frozenset({"formatVersion", "enrollments"}),
    )

    if type(root["formatVersion"]) is not int or root["formatVersion"] != 1:
        raise _ReadinessDocumentError

    enrollments = root["enrollments"]
    if not isinstance(enrollments, list) or not enrollments:
        raise _ReadinessDocumentError

    credential_ids: set[str] = set()
    for value in enrollments:
        enrollment = _require_object(
            value,
            frozenset({"credentialId", "principalId", "trustPolicyId"}),
        )
        credential_id = enrollment["credentialId"]
        principal_id = enrollment["principalId"]
        trust_policy_id = enrollment["trustPolicyId"]

        if (
            not isinstance(credential_id, str)
            or _CREDENTIAL_ID.fullmatch(credential_id) is None
            or credential_id in credential_ids
            or not isinstance(principal_id, str)
            or not principal_id.strip()
            or not isinstance(trust_policy_id, str)
            or not trust_policy_id.strip()
        ):
            raise _ReadinessDocumentError
        credential_ids.add(credential_id)


def main(arguments: list[str] | None = None) -> int:
    values = sys.argv[1:] if arguments is None else arguments
    if len(values) != 1:
        print("CONFIGURATION_INVALID")
        return 2

    try:
        thumbprint, enrollment_path = _load_configuration(Path(values[0]))
    except (OSError, ValueError):
        print("CONFIGURATION_INVALID")
        return 2

    print(
        json.dumps(
            {
                "serverThumbprint": thumbprint,
                "enrollmentPath": str(enrollment_path),
            },
            separators=(",", ":"),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

