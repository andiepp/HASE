"""Physical validation for initial snapshot plus one pushbutton observation."""
from __future__ import annotations
import asyncio, sys
from collections.abc import Sequence
from hase import (EndpointConnectionState, EventOccurred, ObservationInitialSnapshot,
    ObservationKind, RuntimeHostClient, RuntimeHostObservation,
    load_runtime_host_profile, open_runtime_host_channel)

class ValidationError(RuntimeError):
    def __init__(self, code): self.code = code; super().__init__(code)

async def validate(path: str) -> None:
    channel = await open_runtime_host_channel(load_runtime_host_profile(path),
        readiness_timeout=10.0)
    stream = None
    try:
        client = RuntimeHostClient(channel); stream = client.observe()
        initial = await asyncio.wait_for(anext(stream), 10.0)
        if not isinstance(initial, ObservationInitialSnapshot):
            raise ValidationError("initial-snapshot-invalid")
        kel_ready = any(endpoint.connection_status.state is EndpointConnectionState.READY
            and any(instrument.instrument_id == "electronic-load-01"
                for instrument in endpoint.descriptor.instruments)
            for endpoint in initial.snapshot.endpoints)
        if not kel_ready: raise ValidationError("kel103-not-ready")
        print("Press one existing Desktop endpoint pushbutton now.", flush=True)
        while True:
            item = await asyncio.wait_for(anext(stream), 30.0)
            if (isinstance(item, RuntimeHostObservation)
                and item.kind is ObservationKind.EVENT_OCCURRED
                and isinstance(item.payload, EventOccurred)
                and item.payload.event_path_segments[-1] == "ButtonPressed"):
                break
    finally:
        if stream is not None: await stream.aclose()
        await channel.close()

def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    try:
        if len(supplied) != 1: raise ValidationError("arguments-invalid")
        asyncio.run(validate(supplied[0]))
    except (KeyboardInterrupt, SystemExit): raise
    except asyncio.TimeoutError:
        print("Python physical observation validation failed: observation-timeout.", file=sys.stderr); return 1
    except Exception as failure:
        print(f"Python physical observation validation failed: {getattr(failure, 'code', 'unexpected-failure')}.", file=sys.stderr); return 1
    print("Profile loaded              : True")
    print("Initial snapshot received   : True")
    print("KEL-103 Ready verified      : True")
    print("Pushbutton event received   : True")
    print("Stream closed               : True")
    print("Validation succeeded        : True")
    return 0
if __name__ == "__main__": raise SystemExit(main())
