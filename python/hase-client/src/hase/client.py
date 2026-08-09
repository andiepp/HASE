"""Bounded asyncio client operations for the HASE Runtime Host."""

from __future__ import annotations

import asyncio
import math
from typing import Any, Final

import grpc

from hase._generated import runtime_host_remote_api_v1_pb2 as contract
from hase._generated import runtime_host_remote_api_v1_pb2_grpc as services
from hase.channel import RuntimeHostChannel
from hase.mutation import MutationValue
from hase.mutation import _encode_mutation_value
from hase.mutation import _invoke_mutation_once
from hase.mutation import _mutation_timeout
from hase.mutation import _not_sent
from hase.mutation import _project_property_mutation_result
from hase.property import PropertyOperationResult
from hase.property import PropertyTarget
from hase.property import project_property_operation_result
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

    async def _invoke(
        self,
        operation: Any,
        request: Any,
        timeout: float,
    ) -> Any:
        try:
            return await operation(request, timeout=timeout)
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

    async def get_snapshot(
        self,
        *,
        timeout: float = _DEFAULT_RPC_TIMEOUT_SECONDS,
    ) -> RuntimeHostSnapshot:
        """Invoke GetSnapshot exactly once and return its immutable projection."""

        rpc_timeout = _timeout(timeout)
        response = await self._invoke(
            self._stub.GetSnapshot,
            contract.GetSnapshotRequest(),
            rpc_timeout,
        )

        return project_runtime_host_snapshot(response)

    async def read_authoritative_property(
        self,
        target: PropertyTarget,
        *,
        timeout: float = _DEFAULT_RPC_TIMEOUT_SECONDS,
    ) -> PropertyOperationResult:
        """Read one authoritative Property exactly once without retrying."""

        if not isinstance(target, PropertyTarget):
            raise RuntimeHostClientError("property-target-invalid")
        rpc_timeout = _timeout(timeout)
        request = contract.ReadAuthoritativePropertyRequest(
            target=contract.PropertyTarget(
                endpoint_id=target.endpoint_id,
                attachment_generation=target.attachment_generation,
                instrument_id=target.instrument_id,
                property_id=target.property_id,
            )
        )
        response = await self._invoke(
            self._stub.ReadAuthoritativeProperty,
            request,
            rpc_timeout,
        )
        return project_property_operation_result(response)

    async def write_property(
        self,
        target: PropertyTarget,
        requested_value: MutationValue,
        *,
        timeout: float = _DEFAULT_RPC_TIMEOUT_SECONDS,
    ) -> PropertyOperationResult:
        """Write one Property exactly once and return its confirmed result."""

        if not isinstance(target, PropertyTarget):
            raise _not_sent("mutation-property-target-invalid")
        rpc_timeout = _mutation_timeout(timeout)
        requested_remote_value = _encode_mutation_value(requested_value)
        try:
            request = contract.WritePropertyRequest(
                target=contract.PropertyTarget(
                    endpoint_id=target.endpoint_id,
                    attachment_generation=target.attachment_generation,
                    instrument_id=target.instrument_id,
                    property_id=target.property_id,
                ),
                requested_value=requested_remote_value,
            )
        except Exception:
            raise _not_sent("mutation-property-request-invalid") from None

        response = await _invoke_mutation_once(
            self._stub.WriteProperty,
            request,
            rpc_timeout,
        )
        return _project_property_mutation_result(response)


__all__ = [
    "RuntimeHostClient",
    "RuntimeHostClientError",
]
