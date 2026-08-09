import asyncio
from functools import wraps
import math
from typing import Any, Callable, Coroutine, TypeVar

import grpc
import pytest

import hase.client as client_module
from hase import RuntimeHostChannel
from hase import RuntimeHostClient
from hase import RuntimeHostClientError
from hase import SnapshotProjectionError
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


_T = TypeVar("_T")


def _async_test(
    function: Callable[..., Coroutine[Any, Any, _T]],
) -> Callable[..., _T]:
    @wraps(function)
    def run(*args: object, **kwargs: object) -> _T:
        return asyncio.run(function(*args, **kwargs))

    return run


class _UnderlyingChannel:
    def __init__(self) -> None:
        self.close_calls = 0

    async def close(self) -> None:
        self.close_calls += 1


class _RpcFailure(grpc.RpcError):
    def __init__(self, status: grpc.StatusCode) -> None:
        super().__init__()
        self._status = status

    def code(self) -> grpc.StatusCode:
        return self._status

    def details(self) -> str:
        return "secret server and deployment details"


class _GetSnapshot:
    def __init__(self, response: object) -> None:
        self.response = response
        self.calls: list[tuple[object, float]] = []
        self.gate: asyncio.Event | None = None

    async def __call__(self, request: object, *, timeout: float) -> object:
        self.calls.append((request, timeout))
        if self.gate is not None:
            await self.gate.wait()
        if isinstance(self.response, BaseException):
            raise self.response
        return self.response


class _Stub:
    def __init__(self, rpc: _GetSnapshot) -> None:
        self.GetSnapshot = rpc


def _response() -> contract.GetSnapshotResponse:
    response = contract.GetSnapshotResponse(runtime_host_id="desktop-runtime-host")
    response.api_version.major = 1
    return response


def _client(
    monkeypatch: pytest.MonkeyPatch,
    rpc: _GetSnapshot,
) -> tuple[RuntimeHostClient, _UnderlyingChannel, list[object]]:
    constructed_with: list[object] = []

    def create_stub(channel: object) -> _Stub:
        constructed_with.append(channel)
        return _Stub(rpc)

    monkeypatch.setattr(
        client_module.services,
        "RuntimeHostRemoteApiStub",
        create_stub,
    )
    underlying = _UnderlyingChannel()
    channel = RuntimeHostChannel(underlying)  # type: ignore[arg-type]
    return RuntimeHostClient(channel), underlying, constructed_with


@_async_test
async def test_get_snapshot_invokes_once_and_projects_response(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    rpc = _GetSnapshot(_response())
    client, underlying, constructed_with = _client(monkeypatch, rpc)

    result = await client.get_snapshot(timeout=2.5)

    assert result.runtime_host_id == "desktop-runtime-host"
    assert result.api_version.major == 1
    assert result.endpoints == ()
    assert len(rpc.calls) == 1
    assert isinstance(rpc.calls[0][0], contract.GetSnapshotRequest)
    assert rpc.calls[0][1] == 2.5
    assert constructed_with == [underlying]
    assert underlying.close_calls == 0


@_async_test
async def test_get_snapshot_uses_bounded_default_timeout(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    rpc = _GetSnapshot(_response())
    client, _, _ = _client(monkeypatch, rpc)

    await client.get_snapshot()

    assert rpc.calls[0][1] == 10.0


@pytest.mark.parametrize(
    "value",
    [0, -1, True, "10", math.inf, -math.inf, math.nan, None],
)
@_async_test
async def test_get_snapshot_rejects_invalid_timeout_before_rpc(
    monkeypatch: pytest.MonkeyPatch,
    value: object,
) -> None:
    rpc = _GetSnapshot(_response())
    client, _, _ = _client(monkeypatch, rpc)

    with pytest.raises(RuntimeHostClientError) as failure:
        await client.get_snapshot(timeout=value)  # type: ignore[arg-type]
    assert failure.value.code == "rpc-timeout-invalid"
    assert rpc.calls == []


@pytest.mark.parametrize(
    ("status", "code"),
    [
        (grpc.StatusCode.UNAUTHENTICATED, "rpc-unauthenticated"),
        (grpc.StatusCode.PERMISSION_DENIED, "rpc-permission-denied"),
        (grpc.StatusCode.DEADLINE_EXCEEDED, "rpc-deadline-exceeded"),
        (grpc.StatusCode.UNAVAILABLE, "rpc-unavailable"),
        (grpc.StatusCode.CANCELLED, "rpc-cancelled"),
        (grpc.StatusCode.INTERNAL, "rpc-failed"),
    ],
)
@_async_test
async def test_get_snapshot_maps_rpc_status_without_details(
    monkeypatch: pytest.MonkeyPatch,
    status: grpc.StatusCode,
    code: str,
) -> None:
    client, _, _ = _client(monkeypatch, _GetSnapshot(_RpcFailure(status)))

    with pytest.raises(RuntimeHostClientError) as failure:
        await client.get_snapshot()
    assert failure.value.code == code
    assert "secret" not in str(failure.value)


@_async_test
async def test_get_snapshot_maps_unexpected_call_failure_without_details(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    client, _, _ = _client(
        monkeypatch,
        _GetSnapshot(RuntimeError("secret local transport detail")),
    )

    with pytest.raises(RuntimeHostClientError) as failure:
        await client.get_snapshot()
    assert failure.value.code == "rpc-failed"
    assert "secret" not in str(failure.value)


@_async_test
async def test_get_snapshot_propagates_caller_cancellation(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    rpc = _GetSnapshot(_response())
    rpc.gate = asyncio.Event()
    client, _, _ = _client(monkeypatch, rpc)
    operation = asyncio.create_task(client.get_snapshot())
    while not rpc.calls:
        await asyncio.sleep(0)

    operation.cancel()

    with pytest.raises(asyncio.CancelledError):
        await operation
    assert len(rpc.calls) == 1


@_async_test
async def test_get_snapshot_preserves_sanitized_projection_failure(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    client, _, _ = _client(monkeypatch, _GetSnapshot(contract.GetSnapshotResponse()))

    with pytest.raises(SnapshotProjectionError) as failure:
        await client.get_snapshot()
    assert failure.value.code == "snapshot-message-missing"


def test_client_rejects_non_channel_without_constructing_stub(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    constructed = False

    def create_stub(unused: object) -> object:
        nonlocal constructed
        constructed = True
        return object()

    monkeypatch.setattr(
        client_module.services,
        "RuntimeHostRemoteApiStub",
        create_stub,
    )
    with pytest.raises(RuntimeHostClientError) as failure:
        RuntimeHostClient(object())  # type: ignore[arg-type]
    assert failure.value.code == "client-channel-invalid"
    assert not constructed
