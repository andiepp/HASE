"""Strict external Laptop automation target-registry model."""

from __future__ import annotations

from dataclasses import dataclass
import json
import os
from pathlib import Path
from typing import Any, Iterable

from hase.profile import RuntimeHostProfile, load_runtime_host_profile


_MAXIMUM_REGISTRY_BYTES = 64 * 1024
_REQUIRED_TARGET_IDS = frozenset(
    {"desktop-runtime-host", "minipc-runtime-host"}
)


class AutomationTargetRegistryError(ValueError):
    """Sanitized target-registry validation failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Automation target registry validation failed: {code}.")


@dataclass(frozen=True, slots=True)
class AutomationTarget:
    target_id: str
    display_name: str
    profile_path: Path
    profile: RuntimeHostProfile


@dataclass(frozen=True, slots=True)
class AutomationTargetRegistry:
    format_version: int
    targets: tuple[AutomationTarget, ...]

    def resolve(self, target_id: str) -> AutomationTarget:
        matches = tuple(item for item in self.targets if item.target_id == target_id)
        if len(matches) != 1:
            raise AutomationTargetRegistryError("target-id-unknown")
        return matches[0]


class _DuplicatePropertyError(ValueError):
    pass


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for name, value in pairs:
        if name in result:
            raise _DuplicatePropertyError
        result[name] = value
    return result


def _normalized(path: Path) -> str:
    return os.path.normcase(str(path))


def _resolve_external_file(
    value: Any,
    *,
    role: str,
    excluded_roots: tuple[Path, ...],
) -> Path:
    if isinstance(value, str):
        if not value or value != value.strip():
            raise AutomationTargetRegistryError(f"{role}-path-invalid")
    elif not isinstance(value, os.PathLike):
        raise AutomationTargetRegistryError(f"{role}-path-invalid")
    try:
        path = Path(value)
    except (TypeError, ValueError):
        raise AutomationTargetRegistryError(f"{role}-path-invalid") from None
    if not path.is_absolute():
        raise AutomationTargetRegistryError(f"{role}-path-invalid")
    try:
        absolute = Path(os.path.abspath(path))
        resolved = path.resolve(strict=True)
        if not resolved.is_file():
            raise AutomationTargetRegistryError(f"{role}-file-unavailable")
    except AutomationTargetRegistryError:
        raise
    except OSError:
        raise AutomationTargetRegistryError(f"{role}-file-unavailable") from None
    if _normalized(absolute) != _normalized(resolved):
        raise AutomationTargetRegistryError(f"{role}-reparse-point-rejected")
    for root in excluded_roots:
        try:
            resolved.relative_to(root)
        except ValueError:
            continue
        raise AutomationTargetRegistryError(f"{role}-inside-excluded-root")
    return resolved


def _excluded_roots(values: Iterable[str | os.PathLike[str]]) -> tuple[Path, ...]:
    roots: list[Path] = []
    for value in values:
        try:
            root = Path(value)
            if not root.is_absolute():
                raise ValueError
            resolved = root.resolve(strict=True)
            if not resolved.is_dir():
                raise ValueError
        except (OSError, TypeError, ValueError):
            raise AutomationTargetRegistryError("excluded-root-invalid") from None
        roots.append(resolved)
    return tuple(roots)


def load_automation_target_registry(
    path: str | os.PathLike[str],
    *,
    excluded_roots: Iterable[str | os.PathLike[str]] = (),
) -> AutomationTargetRegistry:
    """Load the exact two-target Laptop registry without reading credential bytes."""

    roots = _excluded_roots(excluded_roots)
    registry_path = _resolve_external_file(
        path,
        role="registry",
        excluded_roots=roots,
    )
    try:
        if registry_path.stat().st_size > _MAXIMUM_REGISTRY_BYTES:
            raise AutomationTargetRegistryError("registry-file-too-large")
        content = registry_path.read_bytes()
    except AutomationTargetRegistryError:
        raise
    except OSError:
        raise AutomationTargetRegistryError("registry-file-unavailable") from None
    try:
        text = content.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        raise AutomationTargetRegistryError("registry-encoding-invalid") from None
    try:
        document = json.loads(text, object_pairs_hook=_strict_object)
    except (json.JSONDecodeError, _DuplicatePropertyError):
        raise AutomationTargetRegistryError("registry-json-invalid") from None
    if not isinstance(document, dict) or frozenset(document) != frozenset(
        {"formatVersion", "targets"}
    ):
        raise AutomationTargetRegistryError("registry-shape-invalid")
    if type(document["formatVersion"]) is not int or document["formatVersion"] != 1:
        raise AutomationTargetRegistryError("registry-format-unsupported")
    target_documents = document["targets"]
    if not isinstance(target_documents, list) or len(target_documents) != 2:
        raise AutomationTargetRegistryError("registry-targets-invalid")

    targets: list[AutomationTarget] = []
    for source in target_documents:
        if not isinstance(source, dict) or frozenset(source) != frozenset(
            {"targetId", "displayName", "profilePath"}
        ):
            raise AutomationTargetRegistryError("target-shape-invalid")
        target_id = source["targetId"]
        display_name = source["displayName"]
        if not isinstance(target_id, str) or target_id not in _REQUIRED_TARGET_IDS:
            raise AutomationTargetRegistryError("target-id-invalid")
        if (
            not isinstance(display_name, str)
            or not display_name
            or display_name != display_name.strip()
            or len(display_name) > 128
        ):
            raise AutomationTargetRegistryError("target-display-name-invalid")
        profile_path = _resolve_external_file(
            source["profilePath"],
            role="profile",
            excluded_roots=roots,
        )
        targets.append(
            AutomationTarget(
                target_id,
                display_name,
                profile_path,
                load_runtime_host_profile(profile_path),
            )
        )

    if frozenset(item.target_id for item in targets) != _REQUIRED_TARGET_IDS:
        raise AutomationTargetRegistryError("target-ids-not-exact")
    profile_paths = {_normalized(item.profile_path) for item in targets}
    if len(profile_paths) != 2:
        raise AutomationTargetRegistryError("target-profiles-not-distinct")
    if len({item.profile.address for item in targets}) != 2:
        raise AutomationTargetRegistryError("target-addresses-not-distinct")
    credential_paths = [
        _normalized(path)
        for item in targets
        for path in (
            item.profile.client_certificate_chain_path,
            item.profile.client_private_key_path,
            item.profile.trusted_server_certificate_path,
        )
    ]
    if len(set(credential_paths)) != 6:
        raise AutomationTargetRegistryError("target-credentials-not-distinct")
    return AutomationTargetRegistry(1, tuple(targets))


__all__ = [
    "AutomationTarget",
    "AutomationTargetRegistry",
    "AutomationTargetRegistryError",
    "load_automation_target_registry",
]
