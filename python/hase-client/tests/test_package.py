import hase


def test_package_exposes_the_approved_profile_surface() -> None:
    assert hase.__version__ == "0.1.0"
    assert hase.__all__ == [
        "ProfileValidationError",
        "RuntimeHostChannel",
        "RuntimeHostChannelError",
        "RuntimeHostProfile",
        "__version__",
        "load_runtime_host_profile",
        "open_runtime_host_channel",
    ]
