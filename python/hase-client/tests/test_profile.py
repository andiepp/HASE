from dataclasses import FrozenInstanceError
import json
from pathlib import Path

import pytest

from hase import ProfileValidationError
from hase import RuntimeHostProfile
from hase import load_runtime_host_profile


def _credential_files(tmp_path: Path) -> tuple[Path, Path, Path]:
    certificate_chain = tmp_path / "client-chain.pem"
    private_key = tmp_path / "client-key.pem"
    trusted_server = tmp_path / "trusted-server.pem"

    certificate_chain.write_bytes(b"not parsed by Increment 50C1")
    private_key.write_bytes(b"not parsed by Increment 50C1")
    trusted_server.write_bytes(b"not parsed by Increment 50C1")
    return certificate_chain, private_key, trusted_server


def _valid_document(tmp_path: Path) -> dict[str, object]:
    certificate_chain, private_key, trusted_server = _credential_files(tmp_path)
    return {
        "formatVersion": 1,
        "address": "https://192.0.2.10:50443",
        "clientCertificate": {
            "certificateChainPath": str(certificate_chain),
            "privateKeyPath": str(private_key),
        },
        "trustedServerCertificate": {
            "certificatePath": str(trusted_server),
        },
    }


def _write_document(tmp_path: Path, document: object) -> Path:
    profile_path = tmp_path / "runtime-host-profile.json"
    profile_path.write_text(json.dumps(document), encoding="utf-8")
    return profile_path


def _assert_failure(profile_path: Path, expected_code: str) -> None:
    with pytest.raises(ProfileValidationError) as captured:
        load_runtime_host_profile(profile_path)

    assert captured.value.code == expected_code
    assert str(profile_path) not in str(captured.value)


def test_loads_immutable_profile_without_parsing_dummy_credentials(
    tmp_path: Path,
) -> None:
    profile_path = _write_document(tmp_path, _valid_document(tmp_path))

    profile = load_runtime_host_profile(profile_path)

    assert isinstance(profile, RuntimeHostProfile)
    assert profile.format_version == 1
    assert profile.address == "https://192.0.2.10:50443"
    assert profile.client_certificate_chain_path.is_absolute()
    assert profile.client_private_key_path.is_absolute()
    assert profile.trusted_server_certificate_path.is_absolute()

    with pytest.raises(FrozenInstanceError):
        profile.address = "https://192.0.2.11:50443"  # type: ignore[misc]


def test_rejects_relative_profile_path(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    profile_path = _write_document(tmp_path, _valid_document(tmp_path))
    monkeypatch.chdir(tmp_path)

    _assert_failure(Path(profile_path.name), "profile-path-invalid")


def test_rejects_missing_profile_file(tmp_path: Path) -> None:
    _assert_failure(tmp_path / "missing.json", "profile-file-unavailable")


def test_rejects_profile_directory(tmp_path: Path) -> None:
    _assert_failure(tmp_path, "profile-file-unavailable")


def test_rejects_profile_larger_than_64_kib(tmp_path: Path) -> None:
    profile_path = tmp_path / "large.json"
    profile_path.write_bytes(b" " * ((64 * 1024) + 1))

    _assert_failure(profile_path, "profile-file-too-large")


def test_rejects_invalid_utf8(tmp_path: Path) -> None:
    profile_path = tmp_path / "invalid-utf8.json"
    profile_path.write_bytes(b"\xff")

    _assert_failure(profile_path, "profile-encoding-invalid")


@pytest.mark.parametrize(
    "content",
    [
        "{",
        '{"formatVersion":1,"formatVersion":1}',
        '{"outer":{"value":1,"value":2}}',
    ],
)
def test_rejects_invalid_or_duplicate_json(tmp_path: Path, content: str) -> None:
    profile_path = tmp_path / "invalid.json"
    profile_path.write_text(content, encoding="utf-8")

    _assert_failure(profile_path, "profile-json-invalid")


@pytest.mark.parametrize(
    "mutation",
    [
        lambda document: document.update({"unknown": True}),
        lambda document: document.pop("address"),
        lambda document: document.update({"clientCertificate": []}),
        lambda document: document["clientCertificate"].update(
            {"unknown": True}  # type: ignore[union-attr]
        ),
        lambda document: document["trustedServerCertificate"].clear(),  # type: ignore[union-attr]
    ],
)
def test_rejects_invalid_document_shape(tmp_path: Path, mutation: object) -> None:
    document = _valid_document(tmp_path)
    mutation(document)  # type: ignore[operator]
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "profile-shape-invalid")


@pytest.mark.parametrize("format_version", [True, 0, 2, "1", 1.0])
def test_rejects_unsupported_format_version(
    tmp_path: Path,
    format_version: object,
) -> None:
    document = _valid_document(tmp_path)
    document["formatVersion"] = format_version
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "profile-format-unsupported")


@pytest.mark.parametrize(
    "address",
    [
        "http://192.0.2.10:50443",
        "https://runtime-host.example:50443",
        "https://192.0.2.10",
        "https://user@192.0.2.10:50443",
        "https://192.0.2.10:50443/",
        "https://192.0.2.10:50443/path",
        "https://192.0.2.10:50443?query=true",
        "https://192.0.2.10:50443#fragment",
        " https://192.0.2.10:50443",
        "https://192.0.2.10:70000",
    ],
)
def test_rejects_invalid_address(tmp_path: Path, address: str) -> None:
    document = _valid_document(tmp_path)
    document["address"] = address
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "profile-address-invalid")


def test_accepts_bracketed_ipv6_address(tmp_path: Path) -> None:
    document = _valid_document(tmp_path)
    document["address"] = "https://[2001:db8::10]:50443"
    profile_path = _write_document(tmp_path, document)

    assert load_runtime_host_profile(profile_path).address == (
        "https://[2001:db8::10]:50443"
    )


def test_rejects_relative_credential_path(tmp_path: Path) -> None:
    document = _valid_document(tmp_path)
    client_certificate = document["clientCertificate"]
    assert isinstance(client_certificate, dict)
    client_certificate["privateKeyPath"] = "client-key.pem"
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "credential-path-invalid")


def test_rejects_missing_credential_file(tmp_path: Path) -> None:
    document = _valid_document(tmp_path)
    client_certificate = document["clientCertificate"]
    assert isinstance(client_certificate, dict)
    client_certificate["privateKeyPath"] = str(tmp_path / "missing.pem")
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "credential-file-unavailable")


def test_rejects_credential_directory(tmp_path: Path) -> None:
    document = _valid_document(tmp_path)
    client_certificate = document["clientCertificate"]
    assert isinstance(client_certificate, dict)
    client_certificate["privateKeyPath"] = str(tmp_path)
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "credential-file-unavailable")


def test_rejects_non_distinct_credential_files(tmp_path: Path) -> None:
    document = _valid_document(tmp_path)
    client_certificate = document["clientCertificate"]
    trusted_server = document["trustedServerCertificate"]
    assert isinstance(client_certificate, dict)
    assert isinstance(trusted_server, dict)
    trusted_server["certificatePath"] = client_certificate["certificateChainPath"]
    profile_path = _write_document(tmp_path, document)

    _assert_failure(profile_path, "credential-files-not-distinct")

