"""Async mutual-TLS channel lifecycle for one HASE Runtime Host."""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
import math
from pathlib import Path
import ssl
from typing import Final
from urllib.parse import urlsplit

import grpc

from hase.profile import RuntimeHostProfile


_MAXIMUM_CERTIFICATE_BYTES: Final = 256 * 1024
_MAXIMUM_PRIVATE_KEY_BYTES: Final = 128 * 1024


class RuntimeHostChannelError(RuntimeError):
    """A sanitized Runtime Host channel failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host channel failed: {code}.")


def _target(address: str) -> str:
    parsed = urlsplit(address)
    if parsed.scheme != "https" or not parsed.netloc:
        raise RuntimeHostChannelError("channel-profile-invalid")
    return parsed.netloc


def _read_bounded(path: Path, maximum_bytes: int) -> bytes:
    try:
        if not path.is_absolute() or path.is_symlink() or not path.is_file():
            raise RuntimeHostChannelError("credential-file-unavailable")
        before = path.stat()
        if before.st_size <= 0 or before.st_size > maximum_bytes:
            raise RuntimeHostChannelError("credential-file-size-invalid")
        contents = path.read_bytes()
        after = path.stat()
        identity_before = (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_mtime_ns,
        )
        identity_after = (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        )
        if len(contents) != before.st_size or identity_before != identity_after:
            raise RuntimeHostChannelError("credential-file-changed")
        return contents
    except RuntimeHostChannelError:
        raise
    except OSError:
        raise RuntimeHostChannelError("credential-file-unavailable") from None


def _trusted_roots(certificate: bytes) -> bytes:
    if certificate.startswith(b"-----BEGIN CERTIFICATE-----"):
        return certificate
    try:
        return ssl.DER_cert_to_PEM_cert(certificate).encode("ascii")
    except (ValueError, ssl.SSLError, UnicodeError):
        raise RuntimeHostChannelError("trusted-certificate-invalid") from None


@dataclass(slots=True)
class RuntimeHostChannel:
    """An opened async gRPC channel with deterministic close semantics."""

    _channel: grpc.aio.Channel
    _close_task: asyncio.Task[None] | None = None

    @property
    def grpc_channel(self) -> grpc.aio.Channel:
        """Return the channel for package-provided Runtime Host API clients."""

        return self._channel

    async def _close_once(self) -> None:
        try:
            await self._channel.close()
        except asyncio.CancelledError:
            raise
        except Exception:
            raise RuntimeHostChannelError("channel-close-failed") from None

    async def close(self) -> None:
        """Close once; concurrent and repeated calls share the same operation."""

        if self._close_task is None:
            self._close_task = asyncio.create_task(self._close_once())
        await asyncio.shield(self._close_task)

    async def __aenter__(self) -> RuntimeHostChannel:
        return self

    async def __aexit__(self, *unused: object) -> None:
        await self.close()


async def _close_failed_open(channel: grpc.aio.Channel) -> None:
    try:
        await channel.close()
    except Exception:
        pass


async def open_runtime_host_channel(
    profile: RuntimeHostProfile,
    *,
    readiness_timeout: float = 10.0,
) -> RuntimeHostChannel:
    """Open one mutual-TLS channel without retrying or invoking an RPC."""

    if not isinstance(profile, RuntimeHostProfile):
        raise RuntimeHostChannelError("channel-profile-invalid")
    if (
        isinstance(readiness_timeout, bool)
        or not isinstance(readiness_timeout, (int, float))
        or not math.isfinite(readiness_timeout)
        or readiness_timeout <= 0
    ):
        raise RuntimeHostChannelError("readiness-timeout-invalid")

    certificate_chain = _read_bounded(
        profile.client_certificate_chain_path,
        _MAXIMUM_CERTIFICATE_BYTES,
    )
    private_key = _read_bounded(
        profile.client_private_key_path,
        _MAXIMUM_PRIVATE_KEY_BYTES,
    )
    trusted_server_certificate = _read_bounded(
        profile.trusted_server_certificate_path,
        _MAXIMUM_CERTIFICATE_BYTES,
    )

    try:
        credentials = grpc.ssl_channel_credentials(
            root_certificates=_trusted_roots(trusted_server_certificate),
            private_key=private_key,
            certificate_chain=certificate_chain,
        )
    except Exception:
        raise RuntimeHostChannelError("channel-credentials-invalid") from None

    try:
        channel = grpc.aio.secure_channel(
            _target(profile.address),
            credentials,
            options=(("grpc.enable_retries", 0),),
        )
    except Exception:
        raise RuntimeHostChannelError("channel-create-failed") from None

    try:
        await asyncio.wait_for(
            channel.channel_ready(),
            timeout=float(readiness_timeout),
        )
    except asyncio.CancelledError:
        await _close_failed_open(channel)
        raise
    except TimeoutError:
        await _close_failed_open(channel)
        raise RuntimeHostChannelError("channel-readiness-timeout") from None
    except Exception:
        await _close_failed_open(channel)
        raise RuntimeHostChannelError("channel-readiness-failed") from None

    return RuntimeHostChannel(channel)


__all__ = [
    "RuntimeHostChannel",
    "RuntimeHostChannelError",
    "open_runtime_host_channel",
]
