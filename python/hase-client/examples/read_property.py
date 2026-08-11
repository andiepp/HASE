"""Read one explicitly selected HASE Property authoritatively."""
from __future__ import annotations
import argparse, asyncio, math, sys
from collections.abc import Sequence
from pathlib import Path
from hase import (AutomationTargetRegistryError, EndpointConnectionState,
    NumericDataDescriptor, PropertyAccessMode, PropertyOperationStatus,
    PropertyQuality, PropertyTarget, RuntimeHostChannelError, RuntimeHostClient,
    RuntimeHostClientError, load_automation_target_registry,
    open_runtime_host_channel)

class ExampleReadError(RuntimeError):
    def __init__(self, code: str) -> None:
        self.code=code
        super().__init__(code)

def resolve_property(snapshot, endpoint_id: str, instrument_id: str, property_id: str):
    endpoints=tuple(x for x in snapshot.endpoints if x.endpoint_id==endpoint_id)
    if len(endpoints)!=1: raise ExampleReadError("endpoint-not-found")
    endpoint=endpoints[0]
    if endpoint.connection_status.state is not EndpointConnectionState.READY:
        raise ExampleReadError("endpoint-not-ready")
    instruments=tuple(x for x in endpoint.descriptor.instruments if x.instrument_id==instrument_id)
    if len(instruments)!=1: raise ExampleReadError("instrument-not-found")
    instrument=instruments[0]
    properties=tuple(x for x in instrument.properties if x.property_id==property_id)
    if len(properties)!=1: raise ExampleReadError("property-not-found")
    descriptor=properties[0]
    if descriptor.access_mode not in (PropertyAccessMode.READ, PropertyAccessMode.READ_WRITE):
        raise ExampleReadError("property-not-readable")
    return PropertyTarget(endpoint.endpoint_id, endpoint.attachment_generation,
        instrument.instrument_id, descriptor.property_id), descriptor

def format_result(target_name, descriptor, result):
    if result.status is not PropertyOperationStatus.SUCCESS or result.confirmed_value is None:
        raise ExampleReadError(f"property-read-{result.status.value}")
    item=result.confirmed_value
    if isinstance(descriptor.data, NumericDataDescriptor):
        if isinstance(item.value,bool) or not isinstance(item.value,(int,float)) or not math.isfinite(float(item.value)):
            raise ExampleReadError("numeric-value-invalid")
        value=f"{float(item.value):g} {descriptor.data.native_unit.symbol}"
    else:
        value="<absent>" if item.value is None else str(item.value)
    return "\n".join((f"Target: {target_name}",f"Property: {descriptor.display_name}",
        f"Value: {value}",f"Quality: {item.quality.value}",
        f"Timestamp UTC: {item.timestamp_utc.isoformat()}"))

async def read_property(registry_path: Path,target_id: str,endpoint_id: str,instrument_id: str,property_id: str):
    registry=load_automation_target_registry(registry_path)
    target=registry.resolve(target_id)
    channel=await open_runtime_host_channel(target.profile)
    async with channel:
        client=RuntimeHostClient(channel)
        snapshot=await client.get_snapshot()
        property_target,descriptor=resolve_property(snapshot,endpoint_id,instrument_id,property_id)
        result=await client.read_authoritative_property(property_target)
    return format_result(target.display_name,descriptor,result)

def _parser():
    p=argparse.ArgumentParser(description="Read one explicitly selected HASE Property authoritatively.")
    p.add_argument("--registry",required=True,type=Path)
    p.add_argument("--target",required=True,choices=("desktop-runtime-host","minipc-runtime-host"))
    p.add_argument("--endpoint",required=True); p.add_argument("--instrument",required=True); p.add_argument("--property",required=True)
    return p

def main(argv: Sequence[str]|None=None)->int:
    a=_parser().parse_args(argv)
    try: output=asyncio.run(read_property(a.registry,a.target,a.endpoint,a.instrument,a.property))
    except AutomationTargetRegistryError as e: print(f"ERROR: target registry is invalid ({e.code})",file=sys.stderr); return 2
    except RuntimeHostChannelError as e: print(f"ERROR: Runtime Host channel could not be opened ({e.code})",file=sys.stderr); return 3
    except RuntimeHostClientError as e: print(f"ERROR: Runtime Host operation failed ({e.code})",file=sys.stderr); return 4
    except ExampleReadError as e: print(f"ERROR: authoritative Property read failed ({e.code})",file=sys.stderr); return 5
    print(output); return 0
if __name__=="__main__": raise SystemExit(main())
