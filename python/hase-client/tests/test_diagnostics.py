import asyncio
from functools import wraps
import grpc, pytest
import hase.client as client_module
from hase import (DiagnosticCategory, DiagnosticDirection, DiagnosticLevel,
    DiagnosticProjectionError, RuntimeHostChannel, RuntimeHostClient,
    RuntimeHostClientError, project_diagnostic_observation)
from hase._generated import runtime_host_remote_api_v1_pb2 as contract

def async_test(function):
    @wraps(function)
    def run(*args, **kwargs): return asyncio.run(function(*args, **kwargs))
    return run

def record(sequence=1, source_sequence=10, level=contract.RUNTIME_DIAGNOSTIC_LEVEL_BYTES):
    message=contract.ProjectedDiagnosticObservation(sequence=sequence)
    item=message.record; item.runtime_host_id="desktop-runtime-host"
    item.source_sequence=source_sequence; item.timestamp_utc.FromJsonString("2026-08-09T12:00:00Z")
    item.level=level; item.category=contract.RUNTIME_DIAGNOSTIC_CATEGORY_TRANSPORT_BYTES
    item.event_name="TransportBytesReceived"; item.severity=contract.RUNTIME_DIAGNOSTIC_SEVERITY_TRACE
    item.endpoint_id="endpoint-01"; item.attachment_generation="generation-1"
    item.direction=contract.RUNTIME_DIAGNOSTIC_DIRECTION_INBOUND; item.operation_id="operation-1"
    item.details["correlationId"]="42"; item.byte_snapshot.original_byte_count=2
    item.byte_snapshot.captured_bytes=b"\x31\x0a"; item.byte_snapshot.is_truncated=False
    return message

def test_projection_preserves_scope_bytes_and_details():
    value=project_diagnostic_observation(record())
    assert value.sequence==1 and value.record.source_sequence==10
    assert value.record.level is DiagnosticLevel.BYTES
    assert value.record.category is DiagnosticCategory.TRANSPORT_BYTES
    assert value.record.direction is DiagnosticDirection.INBOUND
    assert value.record.byte_snapshot.captured_bytes==b"\x31\x0a"
    assert value.record.details==(("correlationId","42"),)

@pytest.mark.parametrize("change,code", [
    (lambda x:setattr(x,"sequence",0),"diagnostic-observation-shape-invalid"),
    (lambda x:x.record.ClearField("timestamp_utc"),"diagnostic-record-shape-invalid"),
    (lambda x:(x.record.ClearField("endpoint_id")),"diagnostic-scope-incomplete"),
    (lambda x:x.record.ClearField("byte_snapshot"),"diagnostic-bytes-missing"),
    (lambda x:setattr(x.record.byte_snapshot,"original_byte_count",1),"diagnostic-bytes-invalid")])
def test_projection_rejects_invalid_shapes(change,code):
    source=record(); change(source)
    with pytest.raises(DiagnosticProjectionError) as failure:
        project_diagnostic_observation(source)
    assert failure.value.code==code

def test_projection_allows_endpoint_scope_without_generation():
    source=record();source.record.ClearField("attachment_generation")
    value=project_diagnostic_observation(source)
    assert value.record.endpoint_id=="endpoint-01"
    assert value.record.attachment_generation is None

class Channel:
    async def close(self): pass
class Stream:
    def __init__(self,messages,failure=None): self.messages=messages; self.failure=failure; self.cancelled=False
    def __aiter__(self): return self
    async def __anext__(self):
        if self.messages:return self.messages.pop(0)
        if self.failure: failure,self.failure=self.failure,None; raise failure
        raise StopAsyncIteration
    def cancel(self):self.cancelled=True
class Stub:
    def __init__(self,stream):self.stream=stream;self.calls=0
    def ObserveDiagnostics(self,request):self.calls+=1;return self.stream
def make_client(monkeypatch,stream):
    stub=Stub(stream);monkeypatch.setattr(client_module.services,"RuntimeHostRemoteApiStub",lambda channel:stub)
    return RuntimeHostClient(RuntimeHostChannel(Channel())),stub
class Failure(grpc.RpcError):
    def __init__(self,code):self.status=code
    def code(self):return self.status

@async_test
async def test_client_streams_once_and_cancels(monkeypatch):
    stream=Stream([record(7),record(8,11)]); value,stub=make_client(monkeypatch,stream)
    items=[item async for item in value.observe_diagnostics()]
    assert [x.sequence for x in items]==[7,8] and stub.calls==1 and stream.cancelled

@async_test
async def test_client_rejects_gap_without_resubscription(monkeypatch):
    stream=Stream([record(7),record(9)]); value,stub=make_client(monkeypatch,stream)
    with pytest.raises(RuntimeHostClientError) as failure:
        _=[item async for item in value.observe_diagnostics()]
    assert failure.value.code=="diagnostics-sequence-gap" and stub.calls==1 and stream.cancelled

@pytest.mark.parametrize("status,code",[(grpc.StatusCode.PERMISSION_DENIED,"rpc-permission-denied"),
    (grpc.StatusCode.DATA_LOSS,"diagnostics-sequence-gap")])
@async_test
async def test_client_sanitizes_stream_failure(monkeypatch,status,code):
    stream=Stream([],Failure(status)); value,stub=make_client(monkeypatch,stream)
    with pytest.raises(RuntimeHostClientError) as failure:
        _=[item async for item in value.observe_diagnostics()]
    assert failure.value.code==code and stub.calls==1 and stream.cancelled

@async_test
async def test_client_close_cancels(monkeypatch):
    stream=Stream([record(1),record(2,11)]); value,stub=make_client(monkeypatch,stream)
    iterator=value.observe_diagnostics(); await anext(iterator); await iterator.aclose()
    assert stub.calls==1 and stream.cancelled
