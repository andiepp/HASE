import asyncio
from functools import wraps
import pytest
import hase.client as client_module
from hase import (EndpointConnectionState, PropertyOperationStatus,
    PropertyQuality, PropertyTarget, RuntimeHostChannel, RuntimeHostClient)
from hase._generated import runtime_host_remote_api_v1_pb2 as contract

def async_test(fn):
    @wraps(fn)
    def run(*a, **k): return asyncio.run(fn(*a, **k))
    return run
class Channel:
    async def close(self): pass
class Call:
    def __init__(self, response): self.response=response; self.calls=[]
    async def __call__(self, request, *, timeout):
        self.calls.append((request, timeout)); return self.response
class Stub:
    def __init__(self, call): self.ReadCachedProperty=call
def client(monkeypatch, call):
    monkeypatch.setattr(client_module.services, "RuntimeHostRemoteApiStub",
        lambda channel: Stub(call))
    return RuntimeHostClient(RuntimeHostChannel(Channel()))  # type: ignore[arg-type]
def target(): return PropertyTarget("kel-103", "generation-1",
    "electronic-load-01", "target-current")
def success():
    result=contract.CachedPropertyResult(status=contract.PROPERTY_OPERATION_STATUS_SUCCESS)
    item=result.snapshot; item.target.endpoint_id="kel-103"; item.target.attachment_generation="generation-1"
    item.target.instrument_id="electronic-load-01"; item.target.property_id="target-current"
    item.descriptor.property_id="target-current"; item.descriptor.path_segments.extend(["Target","Current"])
    item.descriptor.display_name="Target current"; item.descriptor.access_mode=contract.PROPERTY_ACCESS_MODE_READ_WRITE
    item.descriptor.data.numeric.quantity.id="electric-current"; item.descriptor.data.numeric.quantity.display_name="Current"
    item.descriptor.data.numeric.native_unit.id="ampere"; item.descriptor.data.numeric.native_unit.display_name="Ampere"
    item.descriptor.data.numeric.native_unit.symbol="A"; item.descriptor.data.numeric.native_unit.quantity.CopyFrom(item.descriptor.data.numeric.quantity)
    item.descriptor.data.numeric.range.minimum=0; item.descriptor.data.numeric.range.maximum=30
    item.descriptor.data.numeric.resolution.value=.0001
    item.connection_status.state=contract.ENDPOINT_CONNECTION_STATE_READY
    item.current_value.value.numeric_value=.1
    item.current_value.timestamp_utc.FromJsonString("2026-08-09T10:00:00Z")
    item.current_value.quality=contract.PROPERTY_QUALITY_GOOD
    return result

@async_test
async def test_cached_read_projects_exact_snapshot_once(monkeypatch):
    call=Call(success()); result=await client(monkeypatch,call).read_cached_property(target(),timeout=2.5)
    assert result.status is PropertyOperationStatus.SUCCESS and result.snapshot is not None
    assert result.snapshot.target == target(); assert result.snapshot.current_value.value == .1
    assert result.snapshot.current_value.quality is PropertyQuality.GOOD
    assert result.snapshot.connection_status.state is EndpointConnectionState.READY
    assert len(call.calls)==1 and call.calls[0][1]==2.5

def test_cached_failure_rejects_snapshot():
    from hase import PropertyProjectionError, project_cached_property_result
    response=success(); response.status=contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND
    with pytest.raises(PropertyProjectionError): project_cached_property_result(response)
