import asyncio
from functools import wraps
import grpc, pytest
import hase.client as client_module
from hase import (EventOccurred, ObservationInitialSnapshot, ObservationKind,
    RuntimeHostChannel, RuntimeHostClient, RuntimeHostClientError,
    RuntimeHostObservation)
from hase._generated import runtime_host_remote_api_v1_pb2 as contract

def async_test(function):
    @wraps(function)
    def run(*args, **kwargs): return asyncio.run(function(*args, **kwargs))
    return run

class Channel:
    async def close(self): pass

class Stream:
    def __init__(self, messages, failure=None):
        self.messages = messages; self.failure = failure; self.cancelled = False
    def __aiter__(self): return self
    async def __anext__(self):
        if self.messages: return self.messages.pop(0)
        if self.failure: failure, self.failure = self.failure, None; raise failure
        raise StopAsyncIteration
    def cancel(self): self.cancelled = True

class Stub:
    def __init__(self, stream): self.stream = stream; self.calls = 0
    def Observe(self, request): self.calls += 1; return self.stream

def make_client(monkeypatch, stream):
    stub = Stub(stream)
    monkeypatch.setattr(client_module.services, "RuntimeHostRemoteApiStub",
        lambda channel: stub)
    return RuntimeHostClient(RuntimeHostChannel(Channel())), stub  # type: ignore[arg-type]

def initial(sequence=5):
    response = contract.ObserveResponse()
    response.initial_snapshot.snapshot.runtime_host_id = "desktop-runtime-host"
    response.initial_snapshot.snapshot.api_version.major = 1
    response.initial_snapshot.snapshot.api_version.minor = 0
    response.initial_snapshot.snapshot_sequence = sequence
    return response

def event(sequence=6):
    response = contract.ObserveResponse()
    item = response.observation
    item.sequence = sequence; item.endpoint_id = "arduino-uno-01"
    item.attachment_generation = "generation-1"
    item.kind = contract.RUNTIME_HOST_OBSERVATION_KIND_EVENT_OCCURRED
    item.event_occurred.instrument_id = "controller-01"
    item.event_occurred.event_path_segments.append("Controller")
    item.event_occurred.event_path_segments.append("ButtonPressed")
    item.event_occurred.occurred_at_utc.FromJsonString("2026-08-09T10:00:00Z")
    item.event_occurred.value.boolean_value = True
    return response

@async_test
async def test_observe_projects_initial_and_event_and_cancels(monkeypatch):
    stream = Stream([initial(), event()])
    client, stub = make_client(monkeypatch, stream)
    values = [item async for item in client.observe()]
    assert isinstance(values[0], ObservationInitialSnapshot)
    assert values[0].snapshot_sequence == 5
    assert isinstance(values[1], RuntimeHostObservation)
    assert values[1].kind is ObservationKind.EVENT_OCCURRED
    assert isinstance(values[1].payload, EventOccurred)
    assert values[1].payload.event_path_segments == ("Controller", "ButtonPressed")
    assert stub.calls == 1 and stream.cancelled

@pytest.mark.parametrize("messages,code", [
    ([event()], "observation-initial-snapshot-missing"),
    ([initial(), initial()], "observation-initial-snapshot-repeated"),
    ([initial(), event(7)], "observation-sequence-gap")])
@async_test
async def test_observe_rejects_stream_shape(monkeypatch, messages, code):
    stream = Stream(messages)
    client, stub = make_client(monkeypatch, stream)
    with pytest.raises(RuntimeHostClientError) as captured:
        _ = [item async for item in client.observe()]
    assert captured.value.code == code
    assert stub.calls == 1 and stream.cancelled

class RpcFailure(grpc.RpcError):
    def __init__(self, code): self._code = code
    def code(self): return self._code

@async_test
async def test_observe_maps_gap_without_resubscribe(monkeypatch):
    stream = Stream([initial()], RpcFailure(grpc.StatusCode.DATA_LOSS))
    client, stub = make_client(monkeypatch, stream)
    with pytest.raises(RuntimeHostClientError) as captured:
        _ = [item async for item in client.observe()]
    assert captured.value.code == "observation-gap"
    assert stub.calls == 1 and stream.cancelled

@async_test
async def test_observe_generator_close_cancels_without_resubscribe(monkeypatch):
    stream = Stream([initial(), event()])
    client, stub = make_client(monkeypatch, stream)
    iterator = client.observe()
    assert isinstance(await anext(iterator), ObservationInitialSnapshot)
    await iterator.aclose()
    assert stub.calls == 1 and stream.cancelled
