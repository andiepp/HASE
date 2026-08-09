from __future__ import annotations
import asyncio,sys
from hase import *
async def run(path):
    channel=await open_runtime_host_channel(load_runtime_host_profile(path),readiness_timeout=10)
    try:
        client=RuntimeHostClient(channel); snapshot=await client.get_snapshot()
        matches=[]
        for endpoint in snapshot.endpoints:
            if endpoint.connection_status.state is EndpointConnectionState.READY:
                for instrument in endpoint.descriptor.instruments:
                    if instrument.instrument_id=="electronic-load-01":
                        matches.append(PropertyTarget(endpoint.endpoint_id,endpoint.attachment_generation,instrument.instrument_id,"target-current"))
        if len(matches)!=1: raise RuntimeError()
        cached=await client.read_cached_property(matches[0]); authoritative=await client.read_authoritative_property(matches[0])
        if cached.snapshot is None or cached.snapshot.current_value is None or authoritative.confirmed_value is None: raise RuntimeError()
        if cached.snapshot.target!=matches[0] or cached.snapshot.current_value.value!=authoritative.confirmed_value.value: raise RuntimeError()
        if cached.snapshot.current_value.quality is not PropertyQuality.GOOD: raise RuntimeError()
    finally: await channel.close()
def main():
    try: asyncio.run(run(sys.argv[1]));
    except Exception: print("Python physical cached Property validation failed.",file=sys.stderr); return 1
    print("Profile loaded             : True"); print("Cached read completed      : True")
    print("Authoritative comparison   : True"); print("Identity and value matched : True")
    print("Channel closed             : True"); print("Validation succeeded       : True"); return 0
if __name__=="__main__": raise SystemExit(main())
