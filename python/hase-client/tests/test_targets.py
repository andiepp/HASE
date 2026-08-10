from dataclasses import FrozenInstanceError
import json
from pathlib import Path

import pytest

from hase import AutomationTargetRegistryError
from hase import load_automation_target_registry


def _profile(root: Path, address: str, prefix: str) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    certificate = root / f"{prefix}-certificate.pem"
    key = root / f"{prefix}-key.pem"
    server = root / f"{prefix}-server.cer"
    for path in (certificate, key, server):
        path.write_bytes(b"custody-only")
    path = root / f"{prefix}-profile.json"
    path.write_text(
        json.dumps(
            {
                "formatVersion": 1,
                "address": address,
                "clientCertificate": {
                    "certificateChainPath": str(certificate),
                    "privateKeyPath": str(key),
                },
                "trustedServerCertificate": {"certificatePath": str(server)},
            }
        ),
        encoding="utf-8",
    )
    return path


def _document(tmp_path: Path) -> dict[str, object]:
    desktop = _profile(tmp_path / "desktop", "https://192.0.2.10:50443", "desktop")
    minipc = _profile(tmp_path / "minipc", "https://192.0.2.11:50443", "minipc")
    return {
        "formatVersion": 1,
        "targets": [
            {
                "targetId": "desktop-runtime-host",
                "displayName": "Desktop Runtime Host",
                "profilePath": str(desktop),
            },
            {
                "targetId": "minipc-runtime-host",
                "displayName": "MiniPC Runtime Host",
                "profilePath": str(minipc),
            },
        ],
    }


def _write(tmp_path: Path, document: object) -> Path:
    path = tmp_path / "targets.json"
    path.write_text(json.dumps(document), encoding="utf-8")
    return path


def _failure(path: Path, code: str, **kwargs: object) -> None:
    with pytest.raises(AutomationTargetRegistryError) as captured:
        load_automation_target_registry(path, **kwargs)  # type: ignore[arg-type]
    assert captured.value.code == code
    assert str(path) not in str(captured.value)


def test_loads_exact_immutable_two_target_registry(tmp_path: Path) -> None:
    path = _write(tmp_path, _document(tmp_path))

    registry = load_automation_target_registry(path)

    assert registry.format_version == 1
    assert tuple(item.target_id for item in registry.targets) == (
        "desktop-runtime-host",
        "minipc-runtime-host",
    )
    assert registry.resolve("minipc-runtime-host").profile.address == (
        "https://192.0.2.11:50443"
    )
    with pytest.raises(FrozenInstanceError):
        registry.format_version = 2  # type: ignore[misc]


def test_rejects_unknown_target_resolution(tmp_path: Path) -> None:
    registry = load_automation_target_registry(_write(tmp_path, _document(tmp_path)))
    with pytest.raises(AutomationTargetRegistryError) as captured:
        registry.resolve("automatic")
    assert captured.value.code == "target-id-unknown"


@pytest.mark.parametrize(
    ("content", "code"),
    [
        (b"\xff", "registry-encoding-invalid"),
        (b"{", "registry-json-invalid"),
        (b'{"formatVersion":1,"formatVersion":1}', "registry-json-invalid"),
    ],
)
def test_rejects_invalid_registry_content(
    tmp_path: Path, content: bytes, code: str
) -> None:
    path = tmp_path / "targets.json"
    path.write_bytes(content)
    _failure(path, code)


def test_rejects_registry_larger_than_64_kib(tmp_path: Path) -> None:
    path = tmp_path / "targets.json"
    path.write_bytes(b" " * ((64 * 1024) + 1))
    _failure(path, "registry-file-too-large")


@pytest.mark.parametrize("version", [True, 0, 2, "1"])
def test_rejects_unsupported_format(tmp_path: Path, version: object) -> None:
    document = _document(tmp_path)
    document["formatVersion"] = version
    _failure(_write(tmp_path, document), "registry-format-unsupported")


@pytest.mark.parametrize(
    "mutation",
    [
        lambda document: document.update({"unknown": True}),
        lambda document: document.pop("targets"),
        lambda document: document.update({"targets": {}}),
        lambda document: document.update({"targets": []}),
        lambda document: document["targets"].append(document["targets"][0]),
    ],
)
def test_rejects_invalid_root_or_target_count(
    tmp_path: Path, mutation: object
) -> None:
    document = _document(tmp_path)
    mutation(document)  # type: ignore[operator]
    code = (
        "registry-shape-invalid"
        if "targets" not in document or "unknown" in document
        else "registry-targets-invalid"
    )
    _failure(_write(tmp_path, document), code)


@pytest.mark.parametrize(
    ("field", "value", "code"),
    [
        ("targetId", "other", "target-id-invalid"),
        ("targetId", 1, "target-id-invalid"),
        ("displayName", "", "target-display-name-invalid"),
        ("displayName", " MiniPC", "target-display-name-invalid"),
        ("profilePath", "relative.json", "profile-path-invalid"),
    ],
)
def test_rejects_invalid_target_fields(
    tmp_path: Path, field: str, value: object, code: str
) -> None:
    document = _document(tmp_path)
    document["targets"][1][field] = value  # type: ignore[index]
    _failure(_write(tmp_path, document), code)


def test_rejects_duplicate_required_target_id(tmp_path: Path) -> None:
    document = _document(tmp_path)
    document["targets"][1]["targetId"] = "desktop-runtime-host"  # type: ignore[index]
    _failure(_write(tmp_path, document), "target-ids-not-exact")


def test_rejects_shared_profile(tmp_path: Path) -> None:
    document = _document(tmp_path)
    document["targets"][1]["profilePath"] = document["targets"][0]["profilePath"]  # type: ignore[index]
    _failure(_write(tmp_path, document), "target-profiles-not-distinct")


def test_rejects_shared_address(tmp_path: Path) -> None:
    document = _document(tmp_path)
    minipc_path = Path(document["targets"][1]["profilePath"])  # type: ignore[index]
    minipc = json.loads(minipc_path.read_text(encoding="utf-8"))
    minipc["address"] = "https://192.0.2.10:50443"
    minipc_path.write_text(json.dumps(minipc), encoding="utf-8")
    _failure(_write(tmp_path, document), "target-addresses-not-distinct")


def test_rejects_shared_credential_custody(tmp_path: Path) -> None:
    document = _document(tmp_path)
    desktop_path = Path(document["targets"][0]["profilePath"])  # type: ignore[index]
    minipc_path = Path(document["targets"][1]["profilePath"])  # type: ignore[index]
    desktop = json.loads(desktop_path.read_text(encoding="utf-8"))
    minipc = json.loads(minipc_path.read_text(encoding="utf-8"))
    minipc["clientCertificate"]["privateKeyPath"] = desktop["clientCertificate"]["privateKeyPath"]
    minipc_path.write_text(json.dumps(minipc), encoding="utf-8")
    _failure(_write(tmp_path, document), "target-credentials-not-distinct")


def test_rejects_registry_or_profile_inside_excluded_root(tmp_path: Path) -> None:
    excluded = tmp_path / "excluded"
    excluded.mkdir()
    outside = tmp_path / "outside"
    outside.mkdir()
    document = _document(outside)
    registry = _write(excluded, document)
    _failure(
        registry,
        "registry-inside-excluded-root",
        excluded_roots=(excluded,),
    )

    registry = _write(outside, document)
    _failure(
        registry,
        "profile-inside-excluded-root",
        excluded_roots=(outside / "desktop",),
    )


def test_rejects_invalid_excluded_root(tmp_path: Path) -> None:
    path = _write(tmp_path, _document(tmp_path))
    _failure(path, "excluded-root-invalid", excluded_roots=(tmp_path / "missing",))
