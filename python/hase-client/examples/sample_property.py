"""Sample one explicitly selected HASE Property at a bounded interval."""
from __future__ import annotations
import argparse, asyncio, math, sys, time
from collections.abc import Awaitable, Callable, Sequence
from pathlib import Path
from hase import (AutomationTargetRegistryError, EndpointConnectionState, NumericDataDescriptor,
    PropertyAccessMode, PropertyOperationStatus, PropertyTarget, RuntimeHostChannelError,
    RuntimeHostClient, RuntimeHostClientError, load_automation_target_registry,
    open_runtime_host_channel)
_MIN_INTERVAL_SECONDS=0.1
_MAX_INTERVAL_SECONDS=3600.0
_MIN_COUNT=1
_MAX_COUNT=1000
class ExampleSampleError(RuntimeError):
    def __init__(self, code: str, sample_number: int|None=None)->None:
        self.code=code; self.sample_number=sample_number; super().__init__(code)
def _bounded_interval(value: str)->float:
    try: interval=float(value)
    except ValueError: raise argparse.ArgumentTypeError("interval must be numeric") from None
    if not math.isfinite(interval) or interval<_MIN_INTERVAL_SECONDS or interval>_MAX_INTERVAL_SECONDS:
        raise argparse.ArgumentTypeError("interval must be between 0.1 and 3600 seconds")
    return interval
def _bounded_count(value: str)->int:
    try: count=int(value)
    except ValueError: raise argparse.ArgumentTypeError("count must be an integer") from None
    if count<_MIN_COUNT or count>_MAX_COUNT: raise argparse.ArgumentTypeError("count must be between 1 and 1000")
    return count
def resolve_property(snapshot,endpoint_id:str,instrument_id:str,property_id:str):
    endpoints=tuple(x for x in snapshot.endpoints if x.endpoint_id==endpoint_id)
    if len(endpoints)!=1: raise ExampleSampleError("endpoint-not-found")
    endpoint=endpoints[0]
    if endpoint.connection_status.state is not EndpointConnectionState.READY: raise ExampleSampleError("endpoint-not-ready")
    instruments=tuple(x for x in endpoint.descriptor.instruments if x.instrument_id==instrument_id)
    if len(instruments)!=1: raise ExampleSampleError("instrument-not-found")
    instrument=instruments[0]
    properties=tuple(x for x in instrument.properties if x.property_id==property_id)
    if len(properties)!=1: raise ExampleSampleError("property-not-found")
    descriptor=properties[0]
    if descriptor.access_mode not in (PropertyAccessMode.READ,PropertyAccessMode.READ_WRITE): raise ExampleSampleError("property-not-readable")
    return PropertyTarget(endpoint.endpoint_id,endpoint.attachment_generation,instrument.instrument_id,descriptor.property_id),descriptor
def _format_value(value:object,descriptor:object)->str:
    if isinstance(descriptor.data,NumericDataDescriptor):
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(float(value)): raise ExampleSampleError("numeric-value-invalid")
        return f"{float(value):g} {descriptor.data.native_unit.symbol}"
    return "<absent>" if value is None else str(value)
async def sample_property(registry_path:Path,target_id:str,endpoint_id:str,instrument_id:str,property_id:str,interval_seconds:float,count:int,*,monotonic:Callable[[],float]=time.monotonic,sleep:Callable[[float],Awaitable[None]]=asyncio.sleep)->str:
    registry=load_automation_target_registry(registry_path); target=registry.resolve(target_id)
    channel=await open_runtime_host_channel(target.profile)
    lines=[f"Target: {target.display_name}",f"Property target: {endpoint_id}/{instrument_id}/{property_id}",f"Interval: {interval_seconds:g} s",f"Count: {count}","","Sample  Timestamp UTC                     Value        Quality"]
    async with channel:
        client=RuntimeHostClient(channel); snapshot=await client.get_snapshot()
        property_target,descriptor=resolve_property(snapshot,endpoint_id,instrument_id,property_id)
        start=monotonic()
        for index in range(count):
            if index:
                delay=(start+index*interval_seconds)-monotonic()
                if delay>0: await sleep(delay)
            sample_number=index+1
            result=await client.read_authoritative_property(property_target)
            if result.status is not PropertyOperationStatus.SUCCESS or result.confirmed_value is None:
                raise ExampleSampleError(f"property-read-{result.status.value}",sample_number)
            confirmed=result.confirmed_value
            lines.append(f"{sample_number:<7} {confirmed.timestamp_utc.isoformat():<33} {_format_value(confirmed.value,descriptor):<12} {confirmed.quality.value}")
    return "\n".join(lines)
def _parser()->argparse.ArgumentParser:
    parser=argparse.ArgumentParser(description="Sample one explicitly selected HASE Property repeatedly.")
    parser.add_argument("--registry",required=True,type=Path)
    parser.add_argument("--target",required=True,choices=("desktop-runtime-host","minipc-runtime-host"))
    parser.add_argument("--endpoint",required=True); parser.add_argument("--instrument",required=True); parser.add_argument("--property",required=True)
    parser.add_argument("--interval",required=True,type=_bounded_interval); parser.add_argument("--count",required=True,type=_bounded_count)
    return parser
def main(argv:Sequence[str]|None=None)->int:
    args=_parser().parse_args(argv)
    try: output=asyncio.run(sample_property(args.registry,args.target,args.endpoint,args.instrument,args.property,args.interval,args.count))
    except AutomationTargetRegistryError as f: print(f"ERROR: target registry is invalid ({f.code})",file=sys.stderr); return 2
    except RuntimeHostChannelError as f: print(f"ERROR: Runtime Host channel could not be opened ({f.code})",file=sys.stderr); return 3
    except RuntimeHostClientError as f: print(f"ERROR: Runtime Host operation failed ({f.code})",file=sys.stderr); return 4
    except ExampleSampleError as f:
        message=f"ERROR: repeated Property sampling failed ({f.code})" if f.sample_number is None else f"ERROR: sample {f.sample_number} failed ({f.code})"
        print(message,file=sys.stderr); return 5
    print(output); return 0
if __name__=="__main__": raise SystemExit(main())
