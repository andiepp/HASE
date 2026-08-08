"""Strict external Runtime Host profile loading."""

from __future__ import annotations

from dataclasses import dataclass
import ipaddress
import json
import os
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit


_MAXIMUM_PROFILE_BYTES = 64 * 1024


class ProfileValidationError(ValueError):
    """A sanitized failure raised while loading a Runtime Host profile."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host profile validation failed: {code}.")


@dataclass(frozen=True, slots=True)
class RuntimeHostProfile:
    """Validated file custody for one Runtime Host connection profile."""

    format_version: int
    address: str
    client_certificate_chain_path: Path
    client_private_key_path: Path
    trusted_server_certificate_path: Path


class _DuplicatePropertyError(ValueError):
    pass


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}

    for name, value in pairs:
        if name in result:
            raise _DuplicatePropertyError
        result[name] = value

    return result


def _require_object(
    value: Any,
    required_names: frozenset[str],
) -> dict[str, Any]:
    if not isinstance(value, dict) or frozenset(value) != required_names:
        raise ProfileValidationError("profile-shape-invalid")
    return value


def _validate_address(value: Any) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ProfileValidationError("profile-address-invalid")

    try:
        parsed = urlsplit(value)
        port = parsed.port
    except ValueError:
        raise ProfileValidationError("profile-address-invalid") from None

    if (
        parsed.scheme != "https"
        or not parsed.netloc
        or parsed.username is not None
        or parsed.password is not None
        or parsed.hostname is None
        or port is None
        or parsed.path
        or parsed.query
        or parsed.fragment
    ):
        raise ProfileValidationError("profile-address-invalid")

    try:
        ipaddress.ip_address(parsed.hostname)
    except ValueError:
        raise ProfileValidationError("profile-address-invalid") from None

    return value


def _validate_credential_path(value: Any) -> Path:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ProfileValidationError("credential-path-invalid")

    path = Path(value)

    if not path.is_absolute():
        raise ProfileValidationError("credential-path-invalid")

    try:
        if not path.is_file():
            raise ProfileValidationError("credential-file-unavailable")
        return path.resolve(strict=True)
    except ProfileValidationError:
        raise
    except OSError:
        raise ProfileValidationError("credential-file-unavailable") from None


def load_runtime_host_profile(path: str | os.PathLike[str]) -> RuntimeHostProfile:
    """Load one strict version-1 profile without reading credential bytes."""

    try:
        profile_path = Path(path)
    except (TypeError, ValueError):
        raise ProfileValidationError("profile-path-invalid") from None

    if not profile_path.is_absolute():
        raise ProfileValidationError("profile-path-invalid")

    try:
        if not profile_path.is_file():
            raise ProfileValidationError("profile-file-unavailable")
        if profile_path.stat().st_size > _MAXIMUM_PROFILE_BYTES:
            raise ProfileValidationError("profile-file-too-large")
        profile_bytes = profile_path.read_bytes()
    except ProfileValidationError:
        raise
    except OSError:
        raise ProfileValidationError("profile-file-unavailable") from None

    try:
        profile_text = profile_bytes.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        raise ProfileValidationError("profile-encoding-invalid") from None

    try:
        document = json.loads(profile_text, object_pairs_hook=_strict_object)
    except (json.JSONDecodeError, _DuplicatePropertyError):
        raise ProfileValidationError("profile-json-invalid") from None

    root = _require_object(
        document,
        frozenset(
            {
                "formatVersion",
                "address",
                "clientCertificate",
                "trustedServerCertificate",
            }
        ),
    )

    if type(root["formatVersion"]) is not int or root["formatVersion"] != 1:
        raise ProfileValidationError("profile-format-unsupported")

    client_certificate = _require_object(
        root["clientCertificate"],
        frozenset({"certificateChainPath", "privateKeyPath"}),
    )
    trusted_server_certificate = _require_object(
        root["trustedServerCertificate"],
        frozenset({"certificatePath"}),
    )

    address = _validate_address(root["address"])
    client_certificate_chain_path = _validate_credential_path(
        client_certificate["certificateChainPath"]
    )
    client_private_key_path = _validate_credential_path(
        client_certificate["privateKeyPath"]
    )
    trusted_server_certificate_path = _validate_credential_path(
        trusted_server_certificate["certificatePath"]
    )

    normalized_paths = {
        os.path.normcase(str(client_certificate_chain_path)),
        os.path.normcase(str(client_private_key_path)),
        os.path.normcase(str(trusted_server_certificate_path)),
    }

    if len(normalized_paths) != 3:
        raise ProfileValidationError("credential-files-not-distinct")

    return RuntimeHostProfile(
        format_version=1,
        address=address,
        client_certificate_chain_path=client_certificate_chain_path,
        client_private_key_path=client_private_key_path,
        trusted_server_certificate_path=trusted_server_certificate_path,
    )


__all__ = [
    "ProfileValidationError",
    "RuntimeHostProfile",
    "load_runtime_host_profile",
]
