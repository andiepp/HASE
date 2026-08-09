"""Bounded asyncio client operations for the HASE Runtime Host."""

from __future__ import annotations

import asyncio
import math
from typing import Final

import grpc

from hase._generated import runtime_host_remote_api_v1_pb2 as contract
from hase._generated import runtime_host_remote_api_v1_pb2_grpc as services
from hase.channel import RuntimeHostChannel
from hase.snapshot import RuntimeHostSnapshot
from hase.snapshot import project_runtime_host_snapshot


_DEFAULT_RPC_TIMEOUT_SECONDS: Final = 10.0


class RuntimeHostClientError(RuntimeError):
    """A sanitized Runtime Host client operation failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host client operation failed: {code}.")


_RPC_ERROR_CODES = {
    grpc.StatusCode.UNAUTHENTICATED: "rpc-unauthenticated",
    grpc.StatusCode.PERMISSION_DENIED: "rpc-permission-denied",
    grpc.StatusCode.DEADLINE_EXCEEDED: "rpc-deadline-exceeded",
    grpc.StatusCode.UNAVAILABLE: "rpc-unavailable",
    grpc.StatusCode.CANCELLED: "rpc-cancelled",
}


def _timeout(value: float) -> float:
    if (
        isinstance(value, bool)
        or not isinstance(value, (int, float))
        or not math.isfinite(value)
        or value <= 0
    ):
        raise RuntimeHostClientError("rpc-timeout-invalid")
    return float(value)


class RuntimeHostClient:
    """An asyncio API client over one caller-owned Runtime Host channel."""

    __slots__ = ("_stub",)

    def __init__(self, channel: RuntimeHostChannel) -> None:
        if not isinstance(channel, RuntimeHostChannel):
            raise RuntimeHostClientError("client-channel-invalid")
        self._stub = services.RuntimeHostRemoteApiStub(channel.grpc_channel)

    async def get_snapshot(
        self,
        *,
        timeout: float = _DEFAULT_RPC_TIMEOUT_SECONDS,
    ) -> RuntimeHostSnapshot:
        """Invoke GetSnapshot exactly once and return its immutable projection."""

        rpc_timeout = _timeout(timeout)
        try:
            response = await self._stub.GetSnapshot(
                contract.GetSnapshotRequest(),
                timeout=rpc_timeout,
            )
        except asyncio.CancelledError:
            raise
        except grpc.RpcError as failure:
            try:
                code = failure.code()
            except Exception:
                code = None
            raise RuntimeHostClientError(
                _RPC_ERROR_CODES.get(code, "rpc-failed")
            ) from None
        except Exception:
            raise RuntimeHostClientError("rpc-failed") from None

        return project_runtime_host_snapshot(response)


__all__ = [
    "RuntimeHostClient",
    "RuntimeHostClientError",
]
