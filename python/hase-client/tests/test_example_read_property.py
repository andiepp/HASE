from __future__ import annotations
import asyncio, sys
from dataclasses import dataclass
from datetime import datetime, timezone
from importlib.util import module_from_spec,spec_from_file_location
from pathlib import Path
import pytest
from hase import (EndpointConnectionState,EndpointConnectionStatus,EndpointDescriptor,
 InstrumentDescriptor,NumericDataDescriptor,PropertyAccessMode,PropertyDescriptor,
 PropertyOperationResult,PropertyOperationStatus,PropertyQuality,PropertyValue,Quantity,
 RuntimeEndpointSnapshot,RuntimeHostApiVersion,RuntimeHostProfile,RuntimeHostSnapshot,Unit,ValueRange)
PATH=Path(__file__).parents[1]/"examples"/"read_property.py"
SPEC=spec_from_file_location("hase_example_read_property",PATH); assert SPEC and SPEC.loader
example=module_from_spec(SPEC); sys.modules[SPEC.name]=example; SPEC.loader.exec_module(example)

def snap(state=EndpointConnectionState.READY,access=PropertyAccessMode.READ):
 q=Quantity("voltage","Voltage"); d=PropertyDescriptor("analog-input-voltage",("Analog","Voltage"),
 "Analog Input Voltage",None,access,NumericDataDescriptor(q,Unit("volt","Volt","V",q),ValueRange(0,5),0.00488759))
 i=InstrumentDescriptor("arduino-uno-controller-01","Arduino Uno GPIO Controller","controller","Arduino","Uno",None,None,None,None,(d,),(),())
 e=RuntimeEndpointSnapshot("arduino-uno-01","generation-52b",EndpointDescriptor("arduino-uno-01",None,None,(i,)),EndpointConnectionStatus(state,None,None))
 return RuntimeHostSnapshot("hidden",RuntimeHostApiVersion(1,0),(e,))

def test_resolve_uses_current_generation():
 t,d=example.resolve_property(snap(),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage")
 assert (t.endpoint_id,t.attachment_generation,t.instrument_id,t.property_id)==("arduino-uno-01","generation-52b","arduino-uno-controller-01","analog-input-voltage")
 assert d.display_name=="Analog Input Voltage"

@pytest.mark.parametrize("s,e,i,p,c",[
 (snap(),"missing","arduino-uno-controller-01","analog-input-voltage","endpoint-not-found"),
 (snap(EndpointConnectionState.RECONNECTING),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage","endpoint-not-ready"),
 (snap(),"arduino-uno-01","missing","analog-input-voltage","instrument-not-found"),
 (snap(),"arduino-uno-01","arduino-uno-controller-01","missing","property-not-found"),
 (snap(access=PropertyAccessMode.WRITE),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage","property-not-readable")])
def test_resolution_failures(s,e,i,p,c):
 with pytest.raises(example.ExampleReadError) as x: example.resolve_property(s,e,i,p)
 assert x.value.code==c

def test_parser_requires_all_selectors():
 with pytest.raises(SystemExit): example._parser().parse_args([])

def test_formats_numeric_unit_quality_timestamp():
 r=PropertyOperationResult(PropertyOperationStatus.SUCCESS,PropertyValue(2.5,datetime(2026,8,11,5,tzinfo=timezone.utc),PropertyQuality.GOOD),None)
 _,d=example.resolve_property(snap(),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage")
 out=example.format_result("MiniPC Runtime Host",d,r)
 assert "Value: 2.5 V" in out and "Quality: good" in out and "2026-08-11T05:00:00+00:00" in out

def test_attachment_not_current_is_not_success():
 r=PropertyOperationResult(PropertyOperationStatus.ATTACHMENT_NOT_CURRENT,None,None)
 _,d=example.resolve_property(snap(),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage")
 with pytest.raises(example.ExampleReadError) as x: example.format_result("MiniPC",d,r)
 assert x.value.code=="property-read-attachment-not-current"

def test_source_uses_only_snapshot_and_authoritative_read():
 s=PATH.read_text(encoding="utf-8")
 assert "hase._" not in s and s.count(".get_snapshot()")==1 and s.count(".read_authoritative_property(")==1
 for x in (".read_cached_property(",".write_property(",".execute_command(",".observe(",".observe_diagnostics("): assert x not in s
