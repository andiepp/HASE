from pathlib import Path

import hase
from hase import _installed_package_validation as validation


def test_expected_rpc_set_covers_all_version_one_operations() -> None:
    assert validation._EXPECTED_RPCS == {
        "ExecuteCommand",
        "GetSnapshot",
        "ObserveDiagnostics",
        "Observe",
        "ReadCachedProperty",
        "ReadAuthoritativeProperty",
        "WriteProperty",
    }


def test_installed_validator_accepts_current_package(monkeypatch, capsys) -> None:
    monkeypatch.setattr(validation, "_is_inside", lambda path, directory: True)
    assert validation.main() == 0
    output = capsys.readouterr().out
    assert "Public API is complete: True" in output
    assert "All version-1 RPCs included: True" in output


def test_is_inside_accepts_file_below_directory(tmp_path) -> None:
    package_directory = tmp_path / "environment"
    package_directory.mkdir()
    package_file = package_directory / "hase.py"
    package_file.touch()
    assert validation._is_inside(package_file, package_directory)


def test_package_file_is_not_inside_unrelated_directory(tmp_path) -> None:
    assert not validation._is_inside(Path(hase.__file__), tmp_path)
