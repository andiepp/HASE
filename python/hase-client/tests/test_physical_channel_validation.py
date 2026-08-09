from pathlib import Path

import pytest

import hase._physical_channel_validation as validation_module
from hase import ProfileValidationError, RuntimeHostChannelError
from hase import RuntimeHostProfile


class _FakeChannel:
    def __init__(self, close_failure: Exception | None = None) -> None:
        self.close_calls = 0
        self.close_failure = close_failure

    async def close(self) -> None:
        self.close_calls += 1
        if self.close_failure is not None:
            raise self.close_failure


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    return RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        tmp_path / "client-chain.pem",
        tmp_path / "client-key.pem",
        tmp_path / "trusted-server.cer",
    )


def test_main_opens_and_closes_once_with_fixed_output(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    profile = _profile(tmp_path)
    fake = _FakeChannel()
    captured: dict[str, object] = {}

    def load(path: str) -> RuntimeHostProfile:
        captured["profile_path"] = path
        return profile

    async def open_channel(
        supplied: RuntimeHostProfile,
        *,
        readiness_timeout: float,
    ) -> _FakeChannel:
        captured["profile"] = supplied
        captured["readiness_timeout"] = readiness_timeout
        return fake

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", load)
    monkeypatch.setattr(validation_module, "open_runtime_host_channel", open_channel)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 0

    output = capsys.readouterr()
    assert output.out.splitlines() == [
        "Profile loaded       : True",
        "Channel ready        : True",
        "Channel closed       : True",
        "Validation succeeded : True",
    ]
    assert output.err == ""
    assert captured == {
        "profile_path": str(tmp_path / "profile.json"),
        "profile": profile,
        "readiness_timeout": 10.0,
    }
    assert fake.close_calls == 1


@pytest.mark.parametrize(
    ("failure", "code"),
    [
        (ProfileValidationError("profile-file-unavailable"),
         "profile-file-unavailable"),
        (RuntimeHostChannelError("channel-readiness-timeout"),
         "channel-readiness-timeout"),
    ],
)
def test_main_reports_only_sanitized_known_failure(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    failure: Exception,
    code: str,
) -> None:
    secret_path = str(tmp_path / "secret-profile.json")

    def fail(unused: str) -> RuntimeHostProfile:
        raise failure

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", fail)

    assert validation_module.main((secret_path,)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        f"Python physical channel validation failed: {code}.\n"
    )
    assert secret_path not in output.err


def test_main_rejects_arguments_without_echoing_them(
    capsys: pytest.CaptureFixture[str],
) -> None:
    secret = "C:\\secret\\profile.json"

    assert validation_module.main(()) == 1
    assert validation_module.main((secret, "extra")) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err.splitlines() == [
        "Python physical channel validation failed: arguments-invalid.",
        "Python physical channel validation failed: arguments-invalid.",
    ]
    assert secret not in output.err


def test_main_sanitizes_unexpected_failure(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    def fail(unused: str) -> RuntimeHostProfile:
        raise RuntimeError("secret deployment detail")

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", fail)

    assert validation_module.main(("C:\\secret\\profile.json",)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical channel validation failed: unexpected-failure.\n"
    )
    assert "secret" not in output.err

