from __future__ import annotations
import asyncio, sys
from dataclasses import dataclass
from datetime import datetime,timedelta,timezone
from importlib.util import module_from_spec,spec_from_file_location
from pathlib import Path
import pytest
from hase import (EndpointConnectionState,EndpointConnectionStatus,EndpointDescriptor,InstrumentDescriptor,
 NumericDataDescriptor,PropertyAccessMode,PropertyDescriptor,PropertyOperationResult,PropertyOperationStatus,
 PropertyQuality,PropertyValue,Quantity,RuntimeEndpointSnapshot,RuntimeHostApiVersion,RuntimeHostProfile,
 RuntimeHostSnapshot,Unit,ValueRange)
PATH=Path(__file__).parents[1]/"examples"/"sample_property.py"
SPEC=spec_from_file_location("hase_example_sample_property",PATH); assert SPEC and SPEC.loader
example=module_from_spec(SPEC); sys.modules[SPEC.name]=example; SPEC.loader.exec_module(example)
def snap(state=EndpointConnectionState.READY,access=PropertyAccessMode.READ):
 q=Quantity("voltage","Voltage"); d=PropertyDescriptor("analog-input-voltage",("Analog","Voltage"),"Analog Input Voltage",None,access,NumericDataDescriptor(q,Unit("volt","Volt","V",q),ValueRange(0,5),0.00488759))
 i=InstrumentDescriptor("arduino-uno-controller-01","Arduino Uno GPIO Controller","controller","Arduino","Uno",None,None,None,None,(d,),(),())
 e=RuntimeEndpointSnapshot("arduino-uno-01","generation-52c",EndpointDescriptor("arduino-uno-01",None,None,(i,)),EndpointConnectionStatus(state,None,None))
 return RuntimeHostSnapshot("hidden",RuntimeHostApiVersion(1,0),(e,))
@pytest.mark.parametrize("value",["0","1001","-1","abc"])
def test_count_rejects_invalid(value):
 with pytest.raises(Exception): example._bounded_count(value)
@pytest.mark.parametrize("value",["0","0.09","3601","nan","inf","abc"])
def test_interval_rejects_invalid(value):
 with pytest.raises(Exception): example._bounded_interval(value)
def test_parser_requires_interval_and_count():
 with pytest.raises(SystemExit): example._parser().parse_args(["--registry",r"C:\x\t.json","--target","minipc-runtime-host","--endpoint","arduino-uno-01","--instrument","arduino-uno-controller-01","--property","analog-input-voltage"])
def test_resolve_uses_snapshot_generation():
 t,d=example.resolve_property(snap(),"arduino-uno-01","arduino-uno-controller-01","analog-input-voltage"); assert t.attachment_generation=="generation-52c" and d.display_name=="Analog Input Voltage"
@dataclass
class Target: display_name:str; profile:RuntimeHostProfile
class Registry:
 def __init__(self,t): self.t=t; self.calls=[]
 def resolve(self,x): self.calls.append(x); return self.t
class Channel:
 def __init__(self): self.entered=0; self.exited=0
 async def __aenter__(self): self.entered+=1; return self
 async def __aexit__(self,*u): self.exited+=1
class Client:
 instances=[]; fail_at=None
 def __init__(self,u): self.snapshot_calls=0; self.read_targets=[]; type(self).instances.append(self)
 async def get_snapshot(self): self.snapshot_calls+=1; return snap()
 async def read_authoritative_property(self,target):
  self.read_targets.append(target); n=len(self.read_targets)
  if self.fail_at==n: return PropertyOperationResult(PropertyOperationStatus.ATTACHMENT_NOT_CURRENT,None,None)
  ts=datetime(2026,8,11,6,0,tzinfo=timezone.utc)+timedelta(seconds=n-1)
  return PropertyOperationResult(PropertyOperationStatus.SUCCESS,PropertyValue(3+n/1000,ts,PropertyQuality.GOOD),None)
def profile(tmp_path):
 paths=[tmp_path/x for x in ("client.pem","client.key","server.cer")]
 for x in paths: x.write_bytes(b"x")
 return RuntimeHostProfile(1,"https://192.0.2.11:50443",*paths)
class Clock:
 def __init__(self): self.now=100.; self.sleeps=[]
 def monotonic(self): return self.now
 async def sleep(self,d): self.sleeps.append(d); self.now+=d
def setup(monkeypatch,tmp_path,client=Client):
 r=Registry(Target("MiniPC Runtime Host",profile(tmp_path))); c=Channel()
 monkeypatch.setattr(example,"load_automation_target_registry",lambda u:r)
 async def op(u): return c
 monkeypatch.setattr(example,"open_runtime_host_channel",op); monkeypatch.setattr(example,"RuntimeHostClient",client)
 client.instances.clear(); client.fail_at=None
 return r,c
def test_success_one_snapshot_exact_count_and_schedule(tmp_path,monkeypatch):
 r,c=setup(monkeypatch,tmp_path); clock=Clock()
 out=asyncio.run(example.sample_property(tmp_path/"t.json","minipc-runtime-host","arduino-uno-01","arduino-uno-controller-01","analog-input-voltage",1.0,5,monotonic=clock.monotonic,sleep=clock.sleep))
 cl=Client.instances[0]; assert r.calls==["minipc-runtime-host"] and cl.snapshot_calls==1 and len(cl.read_targets)==5
 assert {x.attachment_generation for x in cl.read_targets}=={"generation-52c"}; assert clock.sleeps==[1.,1.,1.,1.]
 assert c.entered==1 and c.exited==1 and out.count("good")==5 and "3.001 V" in out and "3.005 V" in out and "192.0.2.11" not in out and "generation-52c" not in out
def test_failure_stops_at_sample_without_refresh(tmp_path,monkeypatch):
 r,c=setup(monkeypatch,tmp_path); clock=Clock(); Client.fail_at=3
 with pytest.raises(example.ExampleSampleError) as x: asyncio.run(example.sample_property(tmp_path/"t.json","minipc-runtime-host","arduino-uno-01","arduino-uno-controller-01","analog-input-voltage",1.0,5,monotonic=clock.monotonic,sleep=clock.sleep))
 Client.fail_at=None; cl=Client.instances[0]; assert x.value.sample_number==3 and cl.snapshot_calls==1 and len(cl.read_targets)==3 and c.exited==1
def test_slow_reads_do_not_overlap_or_add_sleep(tmp_path,monkeypatch):
 clock=Clock()
 class SlowClient(Client):
  async def read_authoritative_property(self,target):
   result=await super().read_authoritative_property(target); clock.now+=1.3; return result
 r,c=setup(monkeypatch,tmp_path,SlowClient)
 asyncio.run(example.sample_property(tmp_path/"t.json","minipc-runtime-host","arduino-uno-01","arduino-uno-controller-01","analog-input-voltage",1.0,3,monotonic=clock.monotonic,sleep=clock.sleep))
 assert clock.sleeps==[] and len(SlowClient.instances[0].read_targets)==3
def test_source_has_only_snapshot_and_authoritative_read():
 s=PATH.read_text(encoding="utf-8"); assert "hase._" not in s and s.count(".get_snapshot()")==1 and s.count(".read_authoritative_property(")==1
 for x in (".read_cached_property(",".write_property(",".execute_command(",".observe(",".observe_diagnostics(","asyncio.gather("): assert x not in s
