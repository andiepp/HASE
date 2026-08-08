import json
from pathlib import Path

import pytest

from hase._credential_readiness import _ReadinessDocumentError
from hase._credential_readiness import _load_configuration


def _write_enrollment(tmp_path: Path) -> Path:
    path = tmp_path / "client-enrollments.json"
    path.write_text(
        json.dumps(
            {
                "formatVersion": 1,
                "enrollments": [
                    {
                        "credentialId": "x509-sha256:" + ("a" * 64),
                        "principalId": "validation-client",
                        "trustPolicyId": "private-network-validation-v1",
                    }
                ],
            }
        ),
        encoding="utf-8",
    )
    return path


def _document(tmp_path: Path) -> dict[str, object]:
    enrollment = _write_enrollment(tmp_path)
    return {
        "formatVersion": 1,
        "binding": {"address": "192.0.2.10", "port": 50443},
        "serverCertificate": {
            "storeName": "My",
            "storeLocation": "CurrentUser",
            "thumbprint": "A" * 40,
        },
        "clientEnrollmentFilePath": str(enrollment),
    }


def _write_configuration(tmp_path: Path, document: object) -> Path:
    path = tmp_path / "desktop-private-network.json"
    path.write_text(json.dumps(document), encoding="utf-8")
    return path


def test_loads_strict_configuration_and_enrollment(tmp_path: Path) -> None:
    path = _write_configuration(tmp_path, _document(tmp_path))

    thumbprint, enrollment_path = _load_configuration(path)

    assert thumbprint == "A" * 40
    assert enrollment_path == (tmp_path / "client-enrollments.json").resolve()


@pytest.mark.parametrize(
    "mutation",
    [
        lambda document: document.update({"unknown": True}),
        lambda document: document.pop("binding"),
        lambda document: document.update({"formatVersion": 2}),
        lambda document: document["binding"].update(  # type: ignore[union-attr]
            {"address": "runtime-host.example"}
        ),
        lambda document: document["binding"].update(  # type: ignore[union-attr]
            {"port": 0}
        ),
        lambda document: document["serverCertificate"].update(  # type: ignore[union-attr]
            {"storeName": "Root"}
        ),
        lambda document: document["serverCertificate"].update(  # type: ignore[union-attr]
            {"thumbprint": "invalid"}
        ),
    ],
)
def test_rejects_invalid_configuration(tmp_path: Path, mutation: object) -> None:
    document = _document(tmp_path)
    mutation(document)  # type: ignore[operator]
    path = _write_configuration(tmp_path, document)

    with pytest.raises(_ReadinessDocumentError):
        _load_configuration(path)


def test_rejects_duplicate_configuration_property(tmp_path: Path) -> None:
    path = tmp_path / "desktop-private-network.json"
    path.write_text('{"formatVersion":1,"formatVersion":1}', encoding="utf-8")

    with pytest.raises(_ReadinessDocumentError):
        _load_configuration(path)


def test_rejects_invalid_enrollment(tmp_path: Path) -> None:
    document = _document(tmp_path)
    enrollment_path = Path(str(document["clientEnrollmentFilePath"]))
    enrollment_path.write_text(
        json.dumps({"formatVersion": 1, "enrollments": []}),
        encoding="utf-8",
    )
    path = _write_configuration(tmp_path, document)

    with pytest.raises(_ReadinessDocumentError):
        _load_configuration(path)

