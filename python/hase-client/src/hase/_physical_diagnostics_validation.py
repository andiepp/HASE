from __future__ import annotations
import asyncio,sys
from hase import *

async def run(path: str) -> None:
    channel=await open_runtime_host_channel(load_runtime_host_profile(path),readiness_timeout=10)
    try:
        client=RuntimeHostClient(channel); snapshot=await client.get_snapshot()
        targets=[]
        for endpoint in snapshot.endpoints:
            if endpoint.connection_status.state is EndpointConnectionState.READY:
                for instrument in endpoint.descriptor.instruments:
                    if instrument.instrument_id=="electronic-load-01":
                        targets.append(PropertyTarget(endpoint.endpoint_id,endpoint.attachment_generation,instrument.instrument_id,"target-current"))
        if len(targets)!=1: raise RuntimeError("target")
        records=[]; ready=asyncio.Event()
        async def collect():
            async for item in client.observe_diagnostics():
                records.append(item); ready.set()
                byte_records=[x.record for x in records if x.record.level is DiagnosticLevel.BYTES and x.record.endpoint_id==targets[0].endpoint_id]
                outbound=[x for x in byte_records if x.direction is DiagnosticDirection.OUTBOUND and dict(x.details).get("correlationId") not in (None,"none") and x.byte_snapshot and x.byte_snapshot.captured_bytes.endswith(b"\x0d")]
                correlated=any(any(y.direction is DiagnosticDirection.INBOUND and dict(y.details).get("correlationId")==dict(x.details).get("correlationId") and y.byte_snapshot and y.byte_snapshot.captured_bytes.endswith(b"\x0a") for y in byte_records) for x in outbound)
                if correlated and any(x.level is DiagnosticLevel.PROTOCOL for x in (r.record for r in records)) and any(x.level is DiagnosticLevel.OPERATIONAL for x in (r.record for r in records)): return
        task=asyncio.create_task(collect())
        try:
            async with asyncio.timeout(15):
                await asyncio.sleep(.25)
                result=await client.read_authoritative_property(targets[0])
                if not result.is_success: raise RuntimeError("read")
                await task
        finally:
            if not task.done(): task.cancel()
            try: await task
            except asyncio.CancelledError: pass
        scoped=[x.record for x in records if x.record.endpoint_id==targets[0].endpoint_id]
        if not scoped or any(x.attachment_generation not in (None,targets[0].attachment_generation) for x in scoped): raise RuntimeError("scope")
        byte_records=[x for x in scoped if x.level is DiagnosticLevel.BYTES and x.byte_snapshot]
        sent=[x for x in byte_records if x.direction is DiagnosticDirection.OUTBOUND and dict(x.details).get("correlationId") not in (None,"none") and x.byte_snapshot.captured_bytes.endswith(b"\x0d")]
        pairs=[(x,y) for x in sent for y in byte_records if y.direction is DiagnosticDirection.INBOUND and dict(y.details).get("correlationId")==dict(x.details).get("correlationId") and y.byte_snapshot and y.byte_snapshot.captured_bytes.endswith(b"\x0a")]
        if not pairs: raise RuntimeError("correlation")
    finally: await channel.close()

def main() -> int:
    try: asyncio.run(run(sys.argv[1]))
    except RuntimeHostClientError as failure:
        print(f"Python physical diagnostic validation failed: {failure.code}.",file=sys.stderr);return 1
    except TimeoutError:
        print("Python physical diagnostic validation failed: evidence-timeout.",file=sys.stderr);return 1
    except RuntimeError as failure:
        code=str(failure) if str(failure) else "evidence-invalid"
        print(f"Python physical diagnostic validation failed: {code}.",file=sys.stderr);return 1
    except Exception:
        print("Python physical diagnostic validation failed: unexpected-failure.",file=sys.stderr);return 1
    print("Profile loaded                : True");print("Diagnostic stream authorized  : True")
    print("Operational record received   : True");print("Protocol record received      : True")
    print("Byte fragments preserved      : True");print("Operation correlation exact   : True");print("Request ended in 0D           : True")
    print("Final response ended in 0A    : True");print("Runtime scope exact           : True")
    print("Stream closed                 : True");print("Validation succeeded          : True");return 0
if __name__=="__main__":raise SystemExit(main())
