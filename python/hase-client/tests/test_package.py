import hase


def test_package_exposes_only_the_initial_version() -> None:
    assert hase.__version__ == "0.1.0"
    assert hase.__all__ == ["__version__"]

